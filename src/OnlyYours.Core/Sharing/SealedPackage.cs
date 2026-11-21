using System.Security.Cryptography;
using OnlyYours.Core.Security.Crypto;
using OnlyYours.Core.Security.Memory;
using OnlyYours.Core.Vault;
using OnlyYours.Core.Vault.Format;
using OnlyYours.Core.Vault.Model;

namespace OnlyYours.Core.Sharing;

public enum PackageMode : byte
{
    Password = 1,
    Recipient = 2,
}

/// <summary>What can be read from a package before opening it.</summary>
public sealed record PackageInfo(PackageMode Mode, byte[]? RecipientFingerprint, PublicIdentity? Sender, KdfParameters? Kdf);

public sealed class PackageField : IDisposable
{
    public FieldKind Kind { get; init; }

    public string? Label { get; init; }

    public string? Text { get; init; }

    public SecureBuffer? Secret { get; init; }

    public FieldFlags Flags { get; init; }

    public void Dispose() => Secret?.Dispose();
}

public sealed class PackageEntry : IDisposable
{
    public EntryKind Kind { get; init; }

    public string Title { get; init; } = string.Empty;

    public bool Favorite { get; init; }

    public List<string> Tags { get; init; } = [];

    public DateTimeOffset Created { get; init; }

    public DateTimeOffset Modified { get; init; }

    public DateTimeOffset? Expires { get; init; }

    public List<PackageField> Fields { get; init; } = [];

    public void Dispose()
    {
        foreach (var field in Fields)
        {
            field.Dispose();
        }
    }
}

/// <summary>A decrypted, verified package.</summary>
public sealed class OpenedPackage : IDisposable
{
    public required List<PackageEntry> Entries { get; init; }

    public required DateTimeOffset Created { get; init; }

    public string? SenderName { get; init; }

    public string? Message { get; init; }

    /// <summary>Set for recipient packages: the verified signer.</summary>
    public PublicIdentity? Sender { get; init; }

    public void Dispose()
    {
        foreach (var entry in Entries)
        {
            entry.Dispose();
        }
    }
}

/// <summary>
/// Sealed packages (<c>.oypkg</c>): selected entries exported for safe transport.
/// <list type="bullet">
/// <item><b>Password mode</b> — Argon2id-derived key, same AEAD cascade and commitment as the vault.</item>
/// <item><b>Recipient mode</b> — hybrid KEM: an ephemeral P-384 ECDH agreement and an ML-KEM-1024
/// encapsulation are combined with HKDF-SHA-512 (both ciphertexts and both recipient keys are
/// bound in), then the package is signed with ML-DSA-87 <em>and</em> ECDSA-P384/SHA-384.
/// Opening requires both signatures to verify.</item>
/// </list>
/// </summary>
public static class SealedPackage
{
    private static ReadOnlySpan<byte> Magic => [0x89, 0x4F, 0x59, 0x50, 0x4B, 0x0D, 0x0A, 0x1A];
    private static ReadOnlySpan<byte> KemLabel => "OnlyYours/v1/package/hybrid-kem"u8;
    private static ReadOnlySpan<byte> PasswordLabel => "OnlyYours/v1/package/password"u8;
    private static ReadOnlySpan<byte> SignatureContext => "OnlyYours/v1/package/signature"u8;
    private const ushort Version = 1;

    // ------------------------------------------------------------- creation

    public static byte[] CreateWithPassword(VaultSession session, IEnumerable<Entry> entries, SecretDraft password, KdfParameters kdf, string? senderName, string? message)
    {
        var packageId = Guid.NewGuid();
        var salt = RandomNumberGenerator.GetBytes(32);
        var header = new BinaryOut();
        WritePrefix(header, packageId, PackageMode.Password);
        header.Byte((byte)kdf.Algorithm);
        header.U32(kdf.MemoryKiB);
        header.U32(kdf.Iterations);
        header.U32(kdf.Parallelism);
        header.Raw(salt);

        using var kek = SecureBuffer.Allocate(32);
        using (var utf8 = password.ToUtf8())
        {
            Argon2.DeriveId(utf8.Span, salt, kdf.MemoryKiB, kdf.Iterations, kdf.Parallelism, kek.Span, associatedData: PasswordLabel);
        }

        using var plain = SerializeEntries(session, entries, senderName, message);
        return Seal(header, packageId, kek.Span, plain.Span, signer: null);
    }

    public static byte[] CreateForRecipient(VaultSession session, IdentityKeys sender, PublicIdentity recipient, IEnumerable<Entry> entries, string? message)
    {
        var packageId = Guid.NewGuid();
        using var ephemeral = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP384);
        var ephemeralPublic = ephemeral.ExportSubjectPublicKeyInfo();

        using var recipientEcdh = ECDiffieHellman.Create();
        recipientEcdh.ImportSubjectPublicKeyInfo(recipient.EcdhPublic, out _);
        using var recipientKem = MLKem.ImportEncapsulationKey(MLKemAlgorithm.MLKem1024, recipient.KemPublic);

        var kemCiphertext = new byte[MLKemAlgorithm.MLKem1024.CiphertextSizeInBytes];
        using var sharedKem = SecureBuffer.Allocate(MLKemAlgorithm.MLKem1024.SharedSecretSizeInBytes);
        recipientKem.Encapsulate(kemCiphertext, sharedKem.Span);
        var sharedEcdh = ephemeral.DeriveRawSecretAgreement(recipientEcdh.PublicKey);

        var header = new BinaryOut();
        WritePrefix(header, packageId, PackageMode.Recipient);
        header.Raw(Sha256(recipient.Fingerprint));
        header.U16((ushort)ephemeralPublic.Length);
        header.Raw(ephemeralPublic);
        header.U16((ushort)kemCiphertext.Length);
        header.Raw(kemCiphertext);
        header.U32((uint)sender.Public.Bundle.Length);
        header.Raw(sender.Public.Bundle);

        using var kek = SecureBuffer.Allocate(32);
        try
        {
            CombineKem(sharedEcdh, sharedKem.Span, ephemeralPublic, kemCiphertext, recipient, packageId, kek.Span);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(sharedEcdh);
        }

        using var plain = SerializeEntries(session, entries, sender.Public.DisplayName, message);
        return Seal(header, packageId, kek.Span, plain.Span, sender);
    }

    private static void WritePrefix(BinaryOut header, Guid packageId, PackageMode mode)
    {
        header.Raw(Magic);
        header.U16(Version);
        header.Byte((byte)mode);
        header.Guid(packageId);
    }

    private static byte[] Seal(BinaryOut header, Guid packageId, ReadOnlySpan<byte> kek, ReadOnlySpan<byte> plaintext, IdentityKeys? signer)
    {
        var payloadSalt = RandomNumberGenerator.GetBytes(32);
        var chachaNonce = RandomNumberGenerator.GetBytes(12);
        var aesNonce = RandomNumberGenerator.GetBytes(12);
        header.Raw(payloadSalt);
        header.Raw(chachaNonce);
        header.Raw(aesNonce);
        var headerBytes = header.ToArray();

        var padded = checked((int)Padme.PaddedLength(4L + plaintext.Length));
        using var framed = SecureBuffer.Allocate(padded);
        framed.Span.Clear();
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(framed.Span, (uint)plaintext.Length);
        plaintext.CopyTo(framed.Span[4..]);

        var output = new BinaryOut();
        output.Raw(headerBytes);
        var ciphertext = new byte[padded + CascadeAead.Overhead];
        Span<byte> keys = stackalloc byte[128];
        try
        {
            VaultCodec.DerivePayloadKeys(kek, packageId, payloadSalt, keys);
            CascadeAead.Encrypt(keys[..32], keys.Slice(32, 32), chachaNonce, aesNonce, framed.Span, headerBytes, ciphertext);
            output.U32((uint)ciphertext.Length);
            output.Raw(ciphertext);
            var mac = HMACSHA512.HashData(keys[64..], output.Written);
            output.Raw(mac);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(keys);
        }

        if (signer is not null)
        {
            var digest = SignatureDigest(output.Written);
            var pqSignature = signer.Dsa.SignData(digest, SignatureContext.ToArray());
            var classicalSignature = signer.Ecdsa.SignData(digest, HashAlgorithmName.SHA384);
            output.U32((uint)pqSignature.Length);
            output.Raw(pqSignature);
            output.U16((ushort)classicalSignature.Length);
            output.Raw(classicalSignature);
        }

        return output.ToArray();
    }

    // -------------------------------------------------------------- opening

    public static bool LooksLikePackage(ReadOnlySpan<byte> data) => data.Length > 8 && data[..8].SequenceEqual(Magic);

    public static PackageInfo Inspect(ReadOnlySpan<byte> package)
    {
        var parsed = ParseHeader(package);
        return new PackageInfo(parsed.Mode, parsed.RecipientDigest, parsed.Sender, parsed.Kdf);
    }

    public static OpenedPackage OpenWithPassword(ReadOnlySpan<byte> package, SecretDraft password)
    {
        var parsed = ParseHeader(package);
        if (parsed.Mode != PackageMode.Password || parsed.Kdf is null || parsed.Salt is null)
        {
            throw new VaultException(VaultError.NotAVault, "This package is not password protected.");
        }

        if (!parsed.Kdf.IsWithinBounds)
        {
            throw new VaultException(VaultError.UnsafeParameters, "The package asks for unsafe key-derivation parameters.");
        }

        using var kek = SecureBuffer.Allocate(32);
        using (var utf8 = password.ToUtf8())
        {
            Argon2.DeriveId(utf8.Span, parsed.Salt, parsed.Kdf.MemoryKiB, parsed.Kdf.Iterations, parsed.Kdf.Parallelism, kek.Span, associatedData: PasswordLabel);
        }

        using var plain = Decrypt(package, parsed, kek.Span, VaultError.InvalidPassword);
        return DeserializeEntries(plain.Span, sender: null);
    }

    public static OpenedPackage OpenForRecipient(ReadOnlySpan<byte> package, IdentityKeys me)
    {
        var parsed = ParseHeader(package);
        if (parsed.Mode != PackageMode.Recipient || parsed.Sender is null || parsed.EphemeralPublic is null || parsed.KemCiphertext is null)
        {
            throw new VaultException(VaultError.NotAVault, "This package is not addressed to an identity.");
        }

        if (!CryptographicOperations.FixedTimeEquals(parsed.RecipientDigest, Sha256(me.Public.Fingerprint)))
        {
            throw new VaultException(VaultError.InvalidRecoveryKey, "This package is addressed to someone else.");
        }

        // Both signatures must verify before anything is decrypted.
        VerifySignatures(package, parsed);

        using var ephemeral = ECDiffieHellman.Create();
        ephemeral.ImportSubjectPublicKeyInfo(parsed.EphemeralPublic, out _);
        var sharedEcdh = me.Ecdh.DeriveRawSecretAgreement(ephemeral.PublicKey);
        using var sharedKem = SecureBuffer.Allocate(MLKemAlgorithm.MLKem1024.SharedSecretSizeInBytes);
        using var kek = SecureBuffer.Allocate(32);
        try
        {
            me.Kem.Decapsulate(parsed.KemCiphertext, sharedKem.Span);
            CombineKem(sharedEcdh, sharedKem.Span, parsed.EphemeralPublic, parsed.KemCiphertext, me.Public, parsed.PackageId, kek.Span);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(sharedEcdh);
        }

        using var plain = Decrypt(package, parsed, kek.Span, VaultError.IntegrityFailure);
        return DeserializeEntries(plain.Span, parsed.Sender);
    }

    /// <summary>Adds the package's entries to <paramref name="session"/>, re-sealed for that vault.</summary>
    public static List<Entry> ImportInto(OpenedPackage package, VaultSession session, IEnumerable<int>? selection = null)
    {
        var chosen = selection?.ToHashSet();
        var result = new List<Entry>();
        var now = DateTimeOffset.Now;
        for (var i = 0; i < package.Entries.Count; i++)
        {
            if (chosen is not null && !chosen.Contains(i))
            {
                continue;
            }

            var source = package.Entries[i];
            var entry = new Entry
            {
                Kind = source.Kind,
                Title = source.Title,
                Favorite = source.Favorite,
                Tags = VaultSession.NormalizeTags(source.Tags),
                Created = source.Created == default ? now : source.Created,
                Modified = now,
                Expires = source.Expires,
            };

            foreach (var field in source.Fields)
            {
                var target = new Field { Kind = field.Kind, Label = field.Label, Flags = field.Flags, Modified = now };
                if (field.Secret is { } secret)
                {
                    target.Sealed = secret.Length == 0 ? null : session.Fields.Seal(secret.Span, entry.Id, target.Id);
                    target.Flags |= FieldFlags.Sensitive;
                }
                else
                {
                    target.Text = field.Text;
                }

                entry.Fields.Add(target);
            }

            result.Add(entry);
        }

        if (result.Count > 0)
        {
            session.AddEntries(result);
            session.Document.AddAudit(package.Sender is null ? AuditKind.Imported : AuditKind.PackageReceived, package.SenderName);
        }

        return result;
    }

    // ------------------------------------------------------------ internals

    private sealed class ParsedHeader
    {
        public PackageMode Mode { get; init; }

        public Guid PackageId { get; init; }

        public int HeaderLength { get; init; }

        public KdfParameters? Kdf { get; init; }

        public byte[]? Salt { get; init; }

        public byte[] RecipientDigest { get; init; } = [];

        public byte[]? EphemeralPublic { get; init; }

        public byte[]? KemCiphertext { get; init; }

        public PublicIdentity? Sender { get; init; }

        public byte[] PayloadSalt { get; init; } = [];

        public byte[] ChaChaNonce { get; init; } = [];

        public byte[] AesNonce { get; init; } = [];
    }

    private static ParsedHeader ParseHeader(ReadOnlySpan<byte> package)
    {
        if (!LooksLikePackage(package))
        {
            throw new VaultException(VaultError.NotAVault, "This is not a OnlyYours package.");
        }

        var input = new BinaryIn(package);
        input.Raw(8);
        if (input.U16() != Version)
        {
            throw new VaultException(VaultError.UnsupportedVersion, "Unsupported package version.");
        }

        var mode = (PackageMode)input.U8();
        var packageId = input.Guid();
        KdfParameters? kdf = null;
        byte[]? salt = null, recipient = null, ephemeral = null, ciphertext = null;
        PublicIdentity? sender = null;
        switch (mode)
        {
            case PackageMode.Password:
                kdf = new KdfParameters((KdfAlgorithm)input.U8(), input.U32(), input.U32(), input.U32());
                salt = input.Raw(32).ToArray();
                break;
            case PackageMode.Recipient:
                recipient = input.Raw(32).ToArray();
                ephemeral = input.Raw(input.U16()).ToArray();
                ciphertext = input.Raw(input.U16()).ToArray();
                sender = PublicIdentity.Parse(input.Long(64 * 1024));
                break;
            default:
                throw new VaultException(VaultError.UnsupportedVersion, "Unknown package mode.");
        }

        var payloadSalt = input.Raw(32).ToArray();
        var chacha = input.Raw(12).ToArray();
        var aes = input.Raw(12).ToArray();
        return new ParsedHeader
        {
            Mode = mode,
            PackageId = packageId,
            HeaderLength = input.Position,
            Kdf = kdf,
            Salt = salt,
            RecipientDigest = recipient ?? [],
            EphemeralPublic = ephemeral,
            KemCiphertext = ciphertext,
            Sender = sender,
            PayloadSalt = payloadSalt,
            ChaChaNonce = chacha,
            AesNonce = aes,
        };
    }

    private static SecureBuffer Decrypt(ReadOnlySpan<byte> package, ParsedHeader parsed, ReadOnlySpan<byte> kek, VaultError failure)
    {
        var input = new BinaryIn(package);
        input.Raw(parsed.HeaderLength);
        var ciphertext = input.Long(256 * 1024 * 1024);
        var macStart = input.Position;
        var mac = input.Raw(64);

        Span<byte> keys = stackalloc byte[128];
        Span<byte> expected = stackalloc byte[64];
        try
        {
            VaultCodec.DerivePayloadKeys(kek, parsed.PackageId, parsed.PayloadSalt, keys);
            HMACSHA512.HashData(keys[64..], package[..macStart], expected);
            if (!CryptographicOperations.FixedTimeEquals(expected, mac))
            {
                throw new VaultException(failure, "The package could not be authenticated.");
            }

            using var framed = SecureBuffer.Allocate(ciphertext.Length - CascadeAead.Overhead);
            if (!CascadeAead.TryDecrypt(keys[..32], keys.Slice(32, 32), parsed.ChaChaNonce, parsed.AesNonce, ciphertext, package[..parsed.HeaderLength], framed.Span))
            {
                throw new VaultException(failure, "The package could not be decrypted.");
            }

            var length = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(framed.Span);
            if (length > (uint)(framed.Length - 4))
            {
                throw new VaultException(VaultError.Corrupted, "The package payload is malformed.");
            }

            return SecureBuffer.CopyOf(framed.Span.Slice(4, (int)length));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(keys);
        }
    }

    private static void VerifySignatures(ReadOnlySpan<byte> package, ParsedHeader parsed)
    {
        var input = new BinaryIn(package);
        input.Raw(parsed.HeaderLength);
        input.Long(256 * 1024 * 1024);
        input.Raw(64);
        var signedLength = input.Position;
        var pq = input.Long(16 * 1024);
        var classical = input.Raw(input.U16());

        var digest = SignatureDigest(package[..signedLength]);
        using var dsa = MLDsa.ImportMLDsaPublicKey(MLDsaAlgorithm.MLDsa87, parsed.Sender!.DsaPublic);
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportSubjectPublicKeyInfo(parsed.Sender.EcdsaPublic, out _);
        var pqValid = dsa.VerifyData(digest, pq, SignatureContext);
        var classicalValid = ecdsa.VerifyData(digest, classical, HashAlgorithmName.SHA384);
        if (!pqValid || !classicalValid)
        {
            throw new VaultException(VaultError.IntegrityFailure, "The package signature is not valid.");
        }
    }

    private static byte[] SignatureDigest(ReadOnlySpan<byte> signed)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA512);
        hash.AppendData(SignatureContext);
        hash.AppendData(signed);
        return hash.GetHashAndReset();
    }

    private static void CombineKem(ReadOnlySpan<byte> sharedEcdh, ReadOnlySpan<byte> sharedKem, ReadOnlySpan<byte> ephemeralPublic, ReadOnlySpan<byte> kemCiphertext, PublicIdentity recipient, Guid packageId, Span<byte> kek)
    {
        using var ikm = SecureBuffer.Allocate(sharedEcdh.Length + sharedKem.Length);
        sharedEcdh.CopyTo(ikm.Span);
        sharedKem.CopyTo(ikm.Span[sharedEcdh.Length..]);

        using var transcript = IncrementalHash.CreateHash(HashAlgorithmName.SHA512);
        transcript.AppendData(ephemeralPublic);
        transcript.AppendData(kemCiphertext);
        transcript.AppendData(recipient.EcdhPublic);
        transcript.AppendData(recipient.KemPublic);
        var salt = transcript.GetHashAndReset();
        VaultCodec.Derive(ikm.Span, salt, KemLabel, packageId, kek);
    }

    private static byte[] Sha256(byte[] data) => SHA256.HashData(data);

    // ------------------------------------------------------- serialization

    private const int DocCreated = 1;
    private const int DocSender = 2;
    private const int DocEntry = 3;
    private const int DocMessage = 4;
    private const int EntryKindTag = 1;
    private const int EntryTitle = 2;
    private const int EntryFavorite = 3;
    private const int EntryTag = 4;
    private const int EntryCreated = 5;
    private const int EntryModified = 6;
    private const int EntryExpires = 7;
    private const int EntryField = 8;
    private const int FieldKindTag = 1;
    private const int FieldLabel = 2;
    private const int FieldText = 3;
    private const int FieldSecret = 4;
    private const int FieldFlagsTag = 5;

    private static SecureBuffer SerializeEntries(VaultSession session, IEnumerable<Entry> entries, string? senderName, string? message)
    {
        using var writer = new TlvWriter(16 * 1024);
        writer.WriteTimestamp(DocCreated, DateTimeOffset.Now);
        writer.WriteString(DocSender, senderName);
        writer.WriteString(DocMessage, message);
        foreach (var entry in entries)
        {
            var at = writer.BeginMessage(DocEntry);
            writer.WriteVarint(EntryKindTag, (ulong)entry.Kind);
            writer.WriteString(EntryTitle, entry.Title);
            writer.WriteBool(EntryFavorite, entry.Favorite);
            foreach (var tag in entry.Tags)
            {
                writer.WriteString(EntryTag, tag);
            }

            writer.WriteTimestamp(EntryCreated, entry.Created);
            writer.WriteTimestamp(EntryModified, entry.Modified);
            writer.WriteTimestamp(EntryExpires, entry.Expires);
            foreach (var field in entry.Fields)
            {
                var fieldAt = writer.BeginMessage(EntryField);
                writer.WriteVarint(FieldKindTag, (ulong)field.Kind);
                writer.WriteString(FieldLabel, field.Label);
                if (field.Sealed is { } sealedValue)
                {
                    using var secret = session.Fields.Open(sealedValue, entry.Id, field.Id);
                    writer.WriteBytes(FieldSecret, secret.Span);
                }
                else
                {
                    writer.WriteString(FieldText, field.Text);
                }

                writer.WriteVarint(FieldFlagsTag, (ulong)field.Flags);
                writer.EndMessage(fieldAt);
            }

            writer.EndMessage(at);
        }

        return writer.ToSecureBuffer();
    }

    private static OpenedPackage DeserializeEntries(ReadOnlySpan<byte> data, PublicIdentity? sender)
    {
        var entries = new List<PackageEntry>();
        DateTimeOffset created = default;
        string? senderName = null, message = null;
        var reader = new TlvReader(data);
        while (reader.TryReadField(out var field, out var type))
        {
            switch (field)
            {
                case DocCreated when type == WireType.Fixed64:
                    created = reader.ReadTimestamp();
                    break;
                case DocSender when type == WireType.Bytes:
                    senderName = reader.ReadString();
                    break;
                case DocMessage when type == WireType.Bytes:
                    message = reader.ReadString();
                    break;
                case DocEntry when type == WireType.Bytes:
                    entries.Add(ReadEntry(reader.ReadBytes()));
                    break;
                default:
                    reader.Skip(type);
                    break;
            }
        }

        return new OpenedPackage { Entries = entries, Created = created, SenderName = senderName, Message = message, Sender = sender };
    }

    private static PackageEntry ReadEntry(ReadOnlySpan<byte> data)
    {
        EntryKind kind = EntryKind.Login;
        string title = string.Empty;
        var favorite = false;
        var tags = new List<string>();
        DateTimeOffset created = default, modified = default;
        DateTimeOffset? expires = null;
        var fields = new List<PackageField>();
        var reader = new TlvReader(data);
        while (reader.TryReadField(out var field, out var type))
        {
            switch (field)
            {
                case EntryKindTag when type == WireType.Varint:
                    kind = (EntryKind)reader.ReadInt32();
                    break;
                case EntryTitle when type == WireType.Bytes:
                    title = reader.ReadString();
                    break;
                case EntryFavorite when type == WireType.Varint:
                    favorite = reader.ReadBool();
                    break;
                case EntryTag when type == WireType.Bytes:
                    tags.Add(reader.ReadString());
                    break;
                case EntryCreated when type == WireType.Fixed64:
                    created = reader.ReadTimestamp();
                    break;
                case EntryModified when type == WireType.Fixed64:
                    modified = reader.ReadTimestamp();
                    break;
                case EntryExpires when type == WireType.Fixed64:
                    expires = reader.ReadTimestamp();
                    break;
                case EntryField when type == WireType.Bytes:
                    fields.Add(ReadField(reader.ReadBytes()));
                    break;
                default:
                    reader.Skip(type);
                    break;
            }
        }

        return new PackageEntry { Kind = kind, Title = title, Favorite = favorite, Tags = tags, Created = created, Modified = modified, Expires = expires, Fields = fields };
    }

    private static PackageField ReadField(ReadOnlySpan<byte> data)
    {
        FieldKind kind = FieldKind.CustomText;
        string? label = null, text = null;
        SecureBuffer? secret = null;
        var flags = FieldFlags.None;
        var reader = new TlvReader(data);
        while (reader.TryReadField(out var field, out var type))
        {
            switch (field)
            {
                case FieldKindTag when type == WireType.Varint:
                    kind = (FieldKind)reader.ReadInt32();
                    break;
                case FieldLabel when type == WireType.Bytes:
                    label = reader.ReadString();
                    break;
                case FieldText when type == WireType.Bytes:
                    text = reader.ReadString();
                    break;
                case FieldSecret when type == WireType.Bytes:
                    secret?.Dispose();
                    secret = SecureBuffer.CopyOf(reader.ReadBytes());
                    break;
                case FieldFlagsTag when type == WireType.Varint:
                    flags = (FieldFlags)reader.ReadInt32();
                    break;
                default:
                    reader.Skip(type);
                    break;
            }
        }

        return new PackageField { Kind = kind, Label = label, Text = text, Secret = secret, Flags = flags };
    }
}
