using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OnlyYours.Core.Otp;
using OnlyYours.Core.Security.Memory;
using OnlyYours.Core.Vault;
using OnlyYours.Core.Vault.Model;

namespace OnlyYours.Core.Legacy;

/// <summary>What the migration found and did.</summary>
public sealed class MigrationReport
{
    public int Entries { get; set; }

    public int Logins { get; set; }

    public int Notes { get; set; }

    public int Favorites { get; set; }

    public int TotpMigrated { get; set; }

    public int TotpFailed { get; set; }

    public int HistoryItems { get; set; }

    public int CustomFields { get; set; }

    public int Tags { get; set; }

    public int PlainNoteEntries { get; set; }

    public bool RecoveredFromBackup { get; set; }

    public string SourceDescription { get; set; } = string.Empty;

    public List<string> Warnings { get; } = [];
}

/// <summary>
/// Converts a decrypted BetterOnlyYours payload into a OnlyYours document.
/// <para>
/// The payload is a JSON object <c>title → value</c>. A value is either free text (the
/// earliest builds: a note) or <c>BOY1:{json}</c> (a structured entry). The reserved key
/// <c>__betteronlyyours_meta__</c> carries usage history and the content key of the
/// sealed two-factor tokens (<c>BOYTOTP1:salt.nonce.box</c>, HKDF-SHA256 + AES-256-GCM).
/// </para>
/// Every secret — password, history, notes, secret custom fields, TOTP — is copied as
/// UTF-8 bytes from locked memory straight into a sealed field: nothing sensitive is
/// ever materialised as a managed string.
/// </summary>
public sealed class LegacyMigrator
{
    public const string MetaKey = "__betteronlyyours_meta__";
    private static ReadOnlySpan<byte> StructuredPrefix => "BOY1:"u8;
    private static ReadOnlySpan<byte> MetaPrefix => "BOYMETA1:"u8;
    private static ReadOnlySpan<byte> TotpPrefix => "BOYTOTP1:"u8;
    private static ReadOnlySpan<byte> TotpAad => "betteronlyyours/totp/v1"u8;
    private static ReadOnlySpan<byte> TotpInfo => "betteronlyyours/totp/v1/entry-key"u8;

    private static readonly JsonReaderOptions ReaderOptions = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip, MaxDepth = 64 };

    private readonly Dictionary<string, DateTimeOffset> _lastUsed = new(StringComparer.Ordinal);
    private SecureBuffer? _contentKey;

    public MigrationReport Report { get; } = new();

    /// <summary>Builds the document, sealing every secret with <paramref name="cipher"/>.</summary>
    public VaultDocument Build(LegacyPayload payload, FieldCipher cipher, string vaultName)
    {
        var now = DateTimeOffset.Now;
        var document = new VaultDocument { Name = vaultName, Created = now, Modified = now };
        Report.RecoveredFromBackup = payload.RecoveredFromBackup;
        Report.SourceDescription = payload.Header.Describe();

        try
        {
            ReadMeta(payload.Json.Span);
            var reader = new Utf8JsonReader(payload.Json.Span, ReaderOptions);
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                throw new VaultException(VaultError.Corrupted, "The legacy payload is not a JSON object.");
            }

            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                var title = reader.GetString() ?? string.Empty;
                if (!reader.Read())
                {
                    break;
                }

                if (title == MetaKey)
                {
                    reader.Skip();
                    continue;
                }

                if (reader.TokenType == JsonTokenType.String)
                {
                    using var value = CopyString(ref reader);
                    var entry = ConvertEntry(title, value.Span, cipher, now);
                    document.Entries.Add(entry);
                }
                else
                {
                    // decodePayload() stored non-string values as their JSON text.
                    using var json = JsonDocument.ParseValue(ref reader);
                    var raw = Encoding.UTF8.GetBytes(json.RootElement.GetRawText());
                    var entry = ConvertEntry(title, raw, cipher, now);
                    CryptographicOperations.ZeroMemory(raw);
                    document.Entries.Add(entry);
                }
            }
        }
        catch (JsonException error)
        {
            throw new VaultException(VaultError.Corrupted, "The legacy payload could not be parsed.", error);
        }
        finally
        {
            _contentKey?.Dispose();
            _contentKey = null;
        }

        Report.Entries = document.Entries.Count;
        Report.Tags = document.Entries.SelectMany(e => e.Tags).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        document.Entries.Sort((a, b) => string.Compare(a.Title, b.Title, StringComparison.CurrentCultureIgnoreCase));
        document.AddAudit(AuditKind.Migrated, Report.SourceDescription, now);
        return document;
    }

    private void ReadMeta(ReadOnlySpan<byte> json)
    {
        var reader = new Utf8JsonReader(json, ReaderOptions);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            return;
        }

        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            var isMeta = reader.ValueTextEquals(MetaKey);
            if (!reader.Read())
            {
                return;
            }

            if (!isMeta || reader.TokenType != JsonTokenType.String)
            {
                reader.Skip();
                continue;
            }

            using var value = CopyString(ref reader);
            var span = value.Span;
            if (span.StartsWith(MetaPrefix))
            {
                span = span[MetaPrefix.Length..];
            }

            ParseMeta(span);
            return;
        }
    }

    private void ParseMeta(ReadOnlySpan<byte> json)
    {
        try
        {
            var reader = new Utf8JsonReader(json, ReaderOptions);
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                return;
            }

            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                if (reader.ValueTextEquals("lastUsed"))
                {
                    reader.Read();
                    if (reader.TokenType != JsonTokenType.StartObject)
                    {
                        reader.Skip();
                        continue;
                    }

                    while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
                    {
                        var title = reader.GetString() ?? string.Empty;
                        reader.Read();
                        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt64(out var millis))
                        {
                            _lastUsed[title] = FromMillis(millis);
                        }
                        else
                        {
                            reader.Skip();
                        }
                    }
                }
                else if (reader.ValueTextEquals("secretKey"))
                {
                    reader.Read();
                    if (reader.TokenType == JsonTokenType.String)
                    {
                        using var encoded = CopyString(ref reader);
                        var key = SecureBuffer.Allocate(Base64.GetMaxDecodedFromUtf8Length(encoded.Length));
                        if (Base64.DecodeFromUtf8(encoded.Span, key.Span, out _, out var written) == System.Buffers.OperationStatus.Done && written == 32)
                        {
                            key.SetLength(written);
                            _contentKey = key;
                        }
                        else
                        {
                            key.Dispose();
                        }
                    }
                    else
                    {
                        reader.Skip();
                    }
                }
                else
                {
                    reader.Read();
                    reader.Skip();
                }
            }
        }
        catch (JsonException)
        {
            Report.Warnings.Add("meta-unreadable");
        }
    }

    private Entry ConvertEntry(string title, ReadOnlySpan<byte> value, FieldCipher cipher, DateTimeOffset now)
    {
        if (value.StartsWith(StructuredPrefix))
        {
            try
            {
                return ConvertStructured(title, value[StructuredPrefix.Length..], cipher, now);
            }
            catch (JsonException)
            {
                // Mirrors VaultEntry.fromStorage: unparsable structure is kept as note text.
                Report.Warnings.Add($"structured-fallback:{title}");
            }
        }

        Report.PlainNoteEntries++;
        Report.Notes++;
        var entry = new Entry { Kind = EntryKind.SecureNote, Title = title, Created = now, Modified = now, LastUsed = LastUsedOf(title) };
        var notes = new Field { Kind = FieldKind.Notes, Flags = FieldFlags.Sensitive | FieldFlags.Multiline, Modified = now };
        if (!value.IsEmpty)
        {
            notes.Sealed = cipher.Seal(value, entry.Id, notes.Id);
        }

        entry.Fields.Add(notes);
        return entry;
    }

    private Entry ConvertStructured(string title, ReadOnlySpan<byte> json, FieldCipher cipher, DateTimeOffset now)
    {
        var entry = new Entry { Title = title, Created = now, Modified = now, LastUsed = LastUsedOf(title) };
        var username = new Field { Kind = FieldKind.Username, Modified = now };
        var password = new Field { Kind = FieldKind.Password, Flags = FieldFlags.Sensitive, Modified = now };
        var url = new Field { Kind = FieldKind.Url, Modified = now };
        var notes = new Field { Kind = FieldKind.Notes, Flags = FieldFlags.Sensitive | FieldFlags.Multiline, Modified = now };
        Field? totp = null;
        var custom = new List<Field>();
        var history = new List<(SecureBuffer Value, DateTimeOffset At)>();
        DateTimeOffset? created = null, updated = null;
        var extras = new List<(string Key, string Raw)>();

        try
        {
            var reader = new Utf8JsonReader(json, ReaderOptions);
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("Structured entry is not an object.");
            }

            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                var key = reader.GetString() ?? string.Empty;
                reader.Read();
                switch (key)
                {
                    case "title":
                        reader.Skip();
                        break;
                    case "username":
                        username.Text = ReadPlainString(ref reader);
                        break;
                    case "url":
                        url.Text = ReadPlainString(ref reader);
                        break;
                    case "password":
                        if (reader.TokenType == JsonTokenType.String)
                        {
                            using var secret = CopyString(ref reader);
                            if (secret.Length > 0)
                            {
                                password.Sealed = cipher.Seal(secret.Span, entry.Id, password.Id);
                            }
                        }
                        else
                        {
                            reader.Skip();
                        }

                        break;
                    case "notes":
                        if (reader.TokenType == JsonTokenType.String)
                        {
                            using var secret = CopyString(ref reader);
                            if (secret.Length > 0)
                            {
                                notes.Sealed = cipher.Seal(secret.Span, entry.Id, notes.Id);
                            }
                        }
                        else
                        {
                            reader.Skip();
                        }

                        break;
                    case "tags":
                        entry.Tags = ReadStringArray(ref reader);
                        break;
                    case "favorite":
                        entry.Favorite = reader.TokenType == JsonTokenType.True;
                        break;
                    case "createdAt":
                        created = ReadTime(ref reader);
                        break;
                    case "updatedAt":
                        updated = ReadTime(ref reader);
                        break;
                    case "fields":
                        ReadCustomFields(ref reader, entry, custom, cipher, now);
                        break;
                    case "history":
                        ReadHistory(ref reader, history);
                        break;
                    case "totp":
                        if (reader.TokenType == JsonTokenType.String)
                        {
                            using var sealedToken = CopyString(ref reader);
                            totp = MigrateTotp(sealedToken.Span, entry, cipher, now);
                        }
                        else
                        {
                            reader.Skip();
                        }

                        break;
                    default:
                        if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
                        {
                            using var doc = JsonDocument.ParseValue(ref reader);
                            extras.Add((key, doc.RootElement.GetRawText()));
                        }
                        else
                        {
                            using var doc = JsonDocument.ParseValue(ref reader);
                            extras.Add((key, doc.RootElement.ValueKind == JsonValueKind.String ? doc.RootElement.GetString() ?? string.Empty : doc.RootElement.GetRawText()));
                        }

                        break;
                }
            }

            var isLogin = username.Text is not null || password.Sealed is not null || url.Text is not null || totp is not null || history.Count > 0;
            entry.Kind = isLogin ? EntryKind.Login : EntryKind.SecureNote;
            if (isLogin)
            {
                entry.Fields.Add(username);
                entry.Fields.Add(password);
                entry.Fields.Add(url);
                if (totp is not null)
                {
                    entry.Fields.Add(totp);
                }

                Report.Logins++;
            }
            else
            {
                Report.Notes++;
            }

            entry.Fields.AddRange(custom);
            foreach (var (extraKey, extraValue) in extras)
            {
                entry.Fields.Add(new Field { Kind = FieldKind.CustomText, Label = extraKey, Text = extraValue, Modified = now });
            }

            entry.Fields.Add(notes);

            foreach (var (value, at) in history)
            {
                var item = new HistoryItem { FieldId = password.Id, ReplacedAt = at, Value = new SealedValue([]) };
                item.Value = cipher.Seal(value.Span, entry.Id, item.Id);
                entry.History.Add(item);
            }

            Report.HistoryItems += entry.History.Count;
            if (entry.Favorite)
            {
                Report.Favorites++;
            }

            entry.Tags = VaultSession.NormalizeTags(entry.Tags);
            entry.Created = created ?? updated ?? now;
            entry.Modified = updated ?? created ?? now;
            foreach (var field in entry.Fields)
            {
                field.Modified = entry.Modified;
            }

            return entry;
        }
        finally
        {
            foreach (var (value, _) in history)
            {
                value.Dispose();
            }
        }
    }

    private void ReadCustomFields(ref Utf8JsonReader reader, Entry entry, List<Field> custom, FieldCipher cipher, DateTimeOffset now)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            reader.Skip();
            return;
        }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                reader.Skip();
                continue;
            }

            string? label = null;
            var secret = false;
            SecureBuffer? value = null;
            try
            {
                while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
                {
                    var key = reader.GetString();
                    reader.Read();
                    switch (key)
                    {
                        case "label":
                            label = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
                            if (reader.TokenType != JsonTokenType.String)
                            {
                                reader.Skip();
                            }

                            break;
                        case "value":
                            if (reader.TokenType == JsonTokenType.String)
                            {
                                value?.Dispose();
                                value = CopyString(ref reader);
                            }
                            else
                            {
                                using var doc = JsonDocument.ParseValue(ref reader);
                                value?.Dispose();
                                value = SecureBuffer.CopyOf(Encoding.UTF8.GetBytes(doc.RootElement.GetRawText()));
                            }

                            break;
                        case "secret":
                            secret = reader.TokenType == JsonTokenType.True;
                            break;
                        default:
                            reader.Skip();
                            break;
                    }
                }

                if (label is null)
                {
                    continue;
                }

                var field = new Field { Label = label, Modified = now };
                if (secret)
                {
                    field.Kind = FieldKind.CustomSecret;
                    field.Flags = FieldFlags.Sensitive;
                    if (value is { Length: > 0 })
                    {
                        field.Sealed = cipher.Seal(value.Span, entry.Id, field.Id);
                    }
                }
                else
                {
                    field.Kind = FieldKind.CustomText;
                    field.Text = value is null ? null : Encoding.UTF8.GetString(value.Span);
                }

                custom.Add(field);
                Report.CustomFields++;
            }
            finally
            {
                value?.Dispose();
            }
        }
    }

    private static void ReadHistory(ref Utf8JsonReader reader, List<(SecureBuffer, DateTimeOffset)> history)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            reader.Skip();
            return;
        }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                reader.Skip();
                continue;
            }

            SecureBuffer? value = null;
            var at = DateTimeOffset.FromUnixTimeMilliseconds(0);
            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                var isPassword = reader.ValueTextEquals("password");
                var isReplaced = reader.ValueTextEquals("replacedAt");
                reader.Read();
                if (isPassword && reader.TokenType == JsonTokenType.String)
                {
                    value?.Dispose();
                    value = CopyString(ref reader);
                }
                else if (isReplaced && reader.TokenType == JsonTokenType.Number && reader.TryGetInt64(out var millis))
                {
                    at = FromMillis(millis);
                }
                else
                {
                    reader.Skip();
                }
            }

            if (value is { Length: > 0 } && history.Count < Entry.MaxHistory)
            {
                history.Add((value, at));
            }
            else
            {
                value?.Dispose();
            }
        }
    }

    private Field? MigrateTotp(ReadOnlySpan<byte> token, Entry entry, FieldCipher cipher, DateTimeOffset now)
    {
        if (!token.StartsWith(TotpPrefix) || token.Length == TotpPrefix.Length)
        {
            return null;
        }

        if (_contentKey is null)
        {
            Report.TotpFailed++;
            Report.Warnings.Add($"totp-no-key:{entry.Title}");
            return null;
        }

        var body = token[TotpPrefix.Length..];
        var firstDot = body.IndexOf((byte)'.');
        var lastDot = body.LastIndexOf((byte)'.');
        if (firstDot <= 0 || lastDot <= firstDot)
        {
            Report.TotpFailed++;
            return null;
        }

        var salt = DecodeBase64Url(body[..firstDot]);
        var nonce = DecodeBase64Url(body[(firstDot + 1)..lastDot]);
        var boxed = DecodeBase64Url(body[(lastDot + 1)..]);
        if (salt is not { Length: 16 } || nonce is not { Length: 12 } || boxed is not { Length: > 16 })
        {
            Report.TotpFailed++;
            return null;
        }

        using var key = SecureBuffer.Allocate(32);
        HKDF.DeriveKey(HashAlgorithmName.SHA256, _contentKey.Span, key.Span, salt, TotpInfo);
        using var plain = SecureBuffer.Allocate(boxed.Length - 16);
        try
        {
            using var aes = new AesGcm(key.Span, 16);
            aes.Decrypt(nonce, boxed.AsSpan(0, boxed.Length - 16), boxed.AsSpan(boxed.Length - 16), plain.Span, TotpAad);
        }
        catch (CryptographicException)
        {
            Report.TotpFailed++;
            Report.Warnings.Add($"totp-undecryptable:{entry.Title}");
            return null;
        }

        try
        {
            using var setup = ParseTotpPayload(plain.Span);
            if (setup is null)
            {
                Report.TotpFailed++;
                return null;
            }

            var field = new Field { Kind = FieldKind.Totp, Flags = FieldFlags.Sensitive, Modified = now };
            using var payload = TotpPayload.Encode(setup.Secret.Span, setup.Config);
            field.Sealed = cipher.Seal(payload.Span, entry.Id, field.Id);
            Report.TotpMigrated++;
            return field;
        }
        catch (JsonException)
        {
            Report.TotpFailed++;
            return null;
        }
    }

    private static OtpSetup? ParseTotpPayload(ReadOnlySpan<byte> json)
    {
        var reader = new Utf8JsonReader(json, ReaderOptions);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            return null;
        }

        SecureBuffer? secret = null;
        string? algorithm = null, kind = null, issuer = null, account = null;
        int? digits = null, period = null;
        try
        {
            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                var key = reader.GetString();
                reader.Read();
                switch (key)
                {
                    case "s" when reader.TokenType == JsonTokenType.String:
                    {
                        using var encoded = CopyString(ref reader);
                        var decoded = SecureBuffer.Allocate(Base64.GetMaxDecodedFromUtf8Length(encoded.Length));
                        if (Base64.DecodeFromUtf8(encoded.Span, decoded.Span, out _, out var written) != System.Buffers.OperationStatus.Done || written == 0)
                        {
                            decoded.Dispose();
                            return null;
                        }

                        decoded.SetLength(written);
                        secret?.Dispose();
                        secret = decoded;
                        break;
                    }

                    case "alg" when reader.TokenType == JsonTokenType.String:
                        algorithm = reader.GetString();
                        break;
                    case "kind" when reader.TokenType == JsonTokenType.String:
                        kind = reader.GetString();
                        break;
                    case "issuer" when reader.TokenType == JsonTokenType.String:
                        issuer = reader.GetString();
                        break;
                    case "account" when reader.TokenType == JsonTokenType.String:
                        account = reader.GetString();
                        break;
                    case "digits" when reader.TokenType == JsonTokenType.Number:
                        digits = reader.TryGetInt32(out var d) ? d : null;
                        break;
                    case "period" when reader.TokenType == JsonTokenType.Number:
                        period = reader.TryGetInt32(out var p) ? p : null;
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            if (secret is null)
            {
                return null;
            }

            var config = new TotpConfig
            {
                Algorithm = TotpConfig.ParseAlgorithm(algorithm),
                Digits = digits ?? TotpConfig.DefaultDigits,
                Period = period ?? TotpConfig.DefaultPeriod,
                Kind = string.Equals(kind, "steam", StringComparison.OrdinalIgnoreCase) ? TotpKind.Steam : TotpKind.Standard,
                Issuer = issuer ?? string.Empty,
                Account = account ?? string.Empty,
            }.Normalized();
            var result = new OtpSetup(secret, config);
            secret = null;
            return result;
        }
        finally
        {
            secret?.Dispose();
        }
    }

    private DateTimeOffset? LastUsedOf(string title) => _lastUsed.TryGetValue(title, out var at) ? at : null;

    private static SecureBuffer CopyString(ref Utf8JsonReader reader)
    {
        var max = reader.HasValueSequence ? checked((int)reader.ValueSequence.Length) : reader.ValueSpan.Length;
        var buffer = SecureBuffer.Allocate(max);
        var written = reader.CopyString(buffer.Span);
        buffer.SetLength(written);
        return buffer;
    }

    private static string? ReadPlainString(ref Utf8JsonReader reader)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString();
            return string.IsNullOrEmpty(value) ? null : value;
        }

        reader.Skip();
        return null;
    }

    private static List<string> ReadStringArray(ref Utf8JsonReader reader)
    {
        var result = new List<string>();
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            reader.Skip();
            return result;
        }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var value = reader.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    result.Add(value);
                }
            }
            else
            {
                reader.Skip();
            }
        }

        return result;
    }

    private static DateTimeOffset? ReadTime(ref Utf8JsonReader reader)
    {
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt64(out var millis))
        {
            return FromMillis(millis);
        }

        if (reader.TokenType == JsonTokenType.String && DateTimeOffset.TryParse(reader.GetString(), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeLocal, out var parsed))
        {
            return parsed;
        }

        reader.Skip();
        return null;
    }

    private static DateTimeOffset FromMillis(long millis) =>
        DateTimeOffset.FromUnixTimeMilliseconds(Math.Clamp(millis, 0, 253_402_300_799_999L)).ToLocalTime();

    private static byte[]? DecodeBase64Url(ReadOnlySpan<byte> text)
    {
        Span<byte> normalized = stackalloc byte[text.Length + 3];
        var length = 0;
        foreach (var b in text)
        {
            normalized[length++] = b switch
            {
                (byte)'-' => (byte)'+',
                (byte)'_' => (byte)'/',
                _ => b,
            };
        }

        while (length % 4 != 0)
        {
            normalized[length++] = (byte)'=';
        }

        var output = new byte[Base64.GetMaxDecodedFromUtf8Length(length)];
        if (Base64.DecodeFromUtf8(normalized[..length], output, out _, out var written) != System.Buffers.OperationStatus.Done)
        {
            return null;
        }

        return output[..written];
    }
}
