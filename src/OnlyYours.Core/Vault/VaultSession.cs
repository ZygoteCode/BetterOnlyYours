using System.Security.Cryptography;
using System.Text;
using OnlyYours.Core.Otp;
using OnlyYours.Core.Security.Crypto;
using OnlyYours.Core.Security.Memory;
using OnlyYours.Core.Vault.Format;
using OnlyYours.Core.Vault.Model;

namespace OnlyYours.Core.Vault;

/// <summary>
/// An unlocked vault: the keys, the decrypted document and the background writer.
/// <list type="bullet">
/// <item>the VMK, the field key and the index key are <see cref="ProtectedBuffer"/>s (kernel-encrypted, no-access pages between uses);</item>
/// <item>sensitive values stay sealed inside the document; <see cref="Reveal"/> opens one into a wiped-on-dispose buffer;</item>
/// <item>saves serialize a snapshot under a lock and encrypt/write it on a background thread, coalescing bursts, so the UI never waits on the disk.</item>
/// </list>
/// Mutations are expected from a single (UI) thread.
/// </summary>
public sealed class VaultSession : IDisposable
{
    private readonly VaultStorage _storage;
    private readonly Lock _gate = new();
    private readonly Lock _saveGate = new();
    private VaultHeader _header;
    private ProtectedBuffer _vmk;
    private FieldCipher _fields;
    private ProtectedBuffer _indexKey;
    private (SecureBuffer Snapshot, VaultHeader Header, bool ForceBackup, ProtectedBuffer Key)? _pending;
    private Task _writer = Task.CompletedTask;
    private bool _writerRunning;
    private Timer? _debounce;
    private bool _disposed;
    private long _revision;

    private VaultSession(VaultStorage storage, VaultHeader header, ReadOnlySpan<byte> vmk, VaultDocument document, UnlockMethod method)
    {
        _storage = storage;
        _header = header;
        _vmk = ProtectedBuffer.Create(vmk);
        _fields = FieldCipher.FromVmk(vmk, header.VaultId);
        _indexKey = CreateIndexKey(vmk, header.VaultId);
        Document = document;
        UnlockedWith = method;
    }

    public VaultDocument Document { get; }

    public UnlockMethod UnlockedWith { get; }

    public VaultStorage Storage => _storage;

    public string Path => _storage.VaultPath;

    public Guid VaultId => _header.VaultId;

    public VaultHeader Header => _header;

    public KdfParameters? PasswordKdf => _header.PasswordSlot?.Kdf;

    public bool HasRecoveryKey => _header.RecoverySlot is not null;

    public bool HasHello => _header.HelloSlot is not null;

    public ulong WriteCounter => _header.WriteCounter;

    public FieldCipher Fields => _fields;

    public SaveState SaveState { get; private set; } = SaveState.Idle;

    public DateTimeOffset? LastSaved { get; private set; }

    public VaultException? LastSaveError { get; private set; }

    public bool IsDisposed => _disposed;

    /// <summary>Bumped on every mutation; cheap change detection for caches.</summary>
    public long Revision => Interlocked.Read(ref _revision);

    /// <summary>Raised on the writer thread after each save attempt.</summary>
    public event EventHandler<SaveResult>? SaveCompleted;

    /// <summary>Raised (on the mutating thread) after any change to the document.</summary>
    public event EventHandler? Changed;

    public bool KeepPasswordHistory { get; set; } = true;

    // ================================================================ opening

    public static async Task<(VaultSession Session, SecureCharBuffer RecoveryCode)> CreateAsync(
        VaultStorage storage,
        string name,
        SecretDraft password,
        KdfParameters kdf,
        CancellationToken cancellationToken = default)
    {
        if (password.IsEmpty)
        {
            throw new ArgumentException("The master password is empty.", nameof(password));
        }

        var vaultId = Guid.NewGuid();
        using var vmk = SecureBuffer.Allocate(VaultCodec.VmkBytes);
        RandomNumberGenerator.Fill(vmk.Span);
        using var passwordUtf8 = password.ToUtf8();
        var passwordSlot = await Task.Run(() => VaultCodec.CreatePasswordSlot(vaultId, passwordUtf8.Span, vmk.Span, kdf, cancellationToken), cancellationToken).ConfigureAwait(false);

        var code = RecoveryKey.Generate(out var recoveryKey);
        RecoverySlot recoverySlot;
        using (recoveryKey)
        {
            recoverySlot = VaultCodec.CreateRecoverySlot(vaultId, recoveryKey.Span, vmk.Span);
        }

        var header = new VaultHeader { VaultId = vaultId, WriteCounter = 0, Slots = [passwordSlot, recoverySlot] };
        var now = DateTimeOffset.Now;
        var document = new VaultDocument { Name = name.Trim(), Created = now, Modified = now };
        document.AddAudit(AuditKind.VaultCreated, at: now);

        var session = new VaultSession(storage, header, vmk.Span, document, UnlockMethod.Created);
        try
        {
            await session.SaveNowAsync(forceBackup: false).ConfigureAwait(false);
        }
        catch
        {
            session.Dispose();
            code.Dispose();
            throw;
        }

        return (session, code);
    }

    /// <summary>Creates a vault from an already populated document (migration, import).</summary>
    public static async Task<(VaultSession Session, SecureCharBuffer RecoveryCode)> CreateFromDocumentAsync(
        VaultStorage storage,
        VaultDocument document,
        Func<FieldCipher, Guid, VaultDocument>? sealDocument,
        SecretDraft password,
        KdfParameters kdf,
        CancellationToken cancellationToken = default)
    {
        var vaultId = Guid.NewGuid();
        using var vmk = SecureBuffer.Allocate(VaultCodec.VmkBytes);
        RandomNumberGenerator.Fill(vmk.Span);
        using var passwordUtf8 = password.ToUtf8();
        var passwordSlot = await Task.Run(() => VaultCodec.CreatePasswordSlot(vaultId, passwordUtf8.Span, vmk.Span, kdf, cancellationToken), cancellationToken).ConfigureAwait(false);

        var code = RecoveryKey.Generate(out var recoveryKey);
        RecoverySlot recoverySlot;
        using (recoveryKey)
        {
            recoverySlot = VaultCodec.CreateRecoverySlot(vaultId, recoveryKey.Span, vmk.Span);
        }

        var header = new VaultHeader { VaultId = vaultId, WriteCounter = 0, Slots = [passwordSlot, recoverySlot] };
        using var cipher = FieldCipher.FromVmk(vmk.Span, vaultId);
        var finalDocument = sealDocument is null ? document : sealDocument(cipher, vaultId);
        var session = new VaultSession(storage, header, vmk.Span, finalDocument, UnlockMethod.Created);
        try
        {
            await session.SaveNowAsync(forceBackup: false).ConfigureAwait(false);
        }
        catch
        {
            session.Dispose();
            code.Dispose();
            throw;
        }

        return (session, code);
    }

    public static async Task<VaultSession> OpenAsync(VaultStorage storage, SecretDraft password, CancellationToken cancellationToken = default)
    {
        var file = await Task.Run(storage.ReadAll, cancellationToken).ConfigureAwait(false);
        var header = VaultHeader.Parse(file);
        var slot = header.PasswordSlot ?? throw new VaultException(VaultError.InvalidPassword, "The vault has no password slot.");
        using var passwordUtf8 = password.ToUtf8();
        using var vmk = SecureBuffer.Allocate(VaultCodec.VmkBytes);
        var unlocked = await Task.Run(
            () =>
            {
                Span<byte> kek = stackalloc byte[32];
                try
                {
                    VaultCodec.DerivePasswordKek(header.VaultId, slot, passwordUtf8.Span, kek, cancellationToken);
                    return VaultCodec.TryUnwrap(slot, header.VaultId, kek, vmk.Span);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(kek);
                }
            },
            cancellationToken).ConfigureAwait(false);

        if (!unlocked)
        {
            throw new VaultException(VaultError.InvalidPassword, "The master password is not correct.");
        }

        return await Task.Run(() => FromFile(storage, file, header, vmk, UnlockMethod.Password), cancellationToken).ConfigureAwait(false);
    }

    public static async Task<VaultSession> OpenWithRecoveryAsync(VaultStorage storage, SecretDraft code, CancellationToken cancellationToken = default)
    {
        var file = await Task.Run(storage.ReadAll, cancellationToken).ConfigureAwait(false);
        var header = VaultHeader.Parse(file);
        var slot = header.RecoverySlot ?? throw new VaultException(VaultError.InvalidRecoveryKey, "The vault has no recovery slot.");

        SecureBuffer? key;
        using (var lease = code.Read())
        {
            if (!RecoveryKey.TryParse(lease.Chars, out key))
            {
                throw new VaultException(VaultError.InvalidRecoveryKey, "The recovery key is not valid.");
            }
        }

        using (key)
        using (var vmk = SecureBuffer.Allocate(VaultCodec.VmkBytes))
        {
            Span<byte> kek = stackalloc byte[32];
            try
            {
                VaultCodec.DeriveRecoveryKek(header.VaultId, slot, key!.Span, kek);
                if (!VaultCodec.TryUnwrap(slot, header.VaultId, kek, vmk.Span))
                {
                    throw new VaultException(VaultError.InvalidRecoveryKey, "The recovery key does not open this vault.");
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(kek);
            }

            var session = FromFile(storage, file, header, vmk, UnlockMethod.RecoveryKey);
            session.Document.AddAudit(AuditKind.UnlockedWithRecovery);
            return session;
        }
    }

    public static async Task<VaultSession> OpenWithHelloAsync(VaultStorage storage, IHelloProvider hello, CancellationToken cancellationToken = default)
    {
        var file = await Task.Run(storage.ReadAll, cancellationToken).ConfigureAwait(false);
        var header = VaultHeader.Parse(file);
        var slot = header.HelloSlot ?? throw new VaultException(VaultError.HelloUnavailable, "Windows Hello is not enabled for this vault.");
        var signature = await hello.SignAsync(slot.CredentialName, slot.Challenge, createIfMissing: false, cancellationToken).ConfigureAwait(false)
            ?? throw new VaultException(VaultError.Cancelled, "Windows Hello verification was cancelled.");

        try
        {
            using var vmk = SecureBuffer.Allocate(VaultCodec.VmkBytes);
            Span<byte> kek = stackalloc byte[32];
            try
            {
                VaultCodec.DeriveHelloKek(header.VaultId, slot, signature, kek);
                if (!VaultCodec.TryUnwrap(slot, header.VaultId, kek, vmk.Span))
                {
                    throw new VaultException(VaultError.HelloFailed, "Windows Hello did not produce the right key.");
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(kek);
            }

            var session = FromFile(storage, file, header, vmk, UnlockMethod.WindowsHello);
            return session;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(signature);
        }
    }

    private static VaultSession FromFile(VaultStorage storage, byte[] file, VaultHeader header, SecureBuffer vmk, UnlockMethod method)
    {
        using var plaintext = VaultCodec.Open(file, header, vmk.Span);
        var document = DocumentSerializer.Deserialize(plaintext.Span);
        return new VaultSession(storage, header, vmk.Span, document, method);
    }

    /// <summary>Checks <paramref name="password"/> against the password slot without touching the session.</summary>
    public async Task<bool> VerifyPasswordAsync(SecretDraft password, CancellationToken cancellationToken = default)
    {
        var slot = _header.PasswordSlot;
        if (slot is null)
        {
            return false;
        }

        using var passwordUtf8 = password.ToUtf8();
        using var candidate = SecureBuffer.Allocate(VaultCodec.VmkBytes);
        var vaultId = VaultId;
        var unwrapped = await Task.Run(
            () =>
            {
                Span<byte> kek = stackalloc byte[32];
                try
                {
                    VaultCodec.DerivePasswordKek(vaultId, slot, passwordUtf8.Span, kek, cancellationToken);
                    return VaultCodec.TryUnwrap(slot, vaultId, kek, candidate.Span);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(kek);
                }
            },
            cancellationToken).ConfigureAwait(false);

        return unwrapped && _vmk.FixedTimeEquals(candidate.Span);
    }

    // ============================================================== entries

    public IEnumerable<Entry> Entries => Document.Entries;

    public Entry? Find(Guid id) => Document.Find(id);

    /// <summary>A new (not yet stored) entry with the template fields of its kind.</summary>
    public static Entry NewEntry(EntryKind kind, string title)
    {
        var now = DateTimeOffset.Now;
        var entry = new Entry { Kind = kind, Title = title, Created = now, Modified = now };
        foreach (var fieldKind in FieldKindInfo.Template(kind))
        {
            entry.Fields.Add(new Field
            {
                Kind = fieldKind,
                Flags = (fieldKind.IsSensitive() ? FieldFlags.Sensitive : FieldFlags.None) | (fieldKind.IsMultiline() ? FieldFlags.Multiline : FieldFlags.None),
                Modified = now,
            });
        }

        return entry;
    }

    /// <summary>Applies an editor submission: re-seals changed secrets, records history, persists.</summary>
    public Entry Apply(EntryEdit edit)
    {
        ThrowIfDisposed();
        var now = DateTimeOffset.Now;
        Entry entry;
        var isNew = edit.EntryId is null;
        lock (_gate)
        {
            entry = isNew ? new Entry { Created = now } : Document.Entries.FirstOrDefault(e => e.Id == edit.EntryId) ?? Document.Trash.FirstOrDefault(e => e.Id == edit.EntryId) ?? throw new InvalidOperationException("The entry no longer exists.");
            var previous = entry.Fields.ToDictionary(f => f.Id);
            var fields = new List<Field>(edit.Fields.Count);
            foreach (var fieldEdit in edit.Fields)
            {
                var field = previous.TryGetValue(fieldEdit.Id, out var existing) ? existing : new Field { Id = fieldEdit.Id, Modified = now };
                field.Kind = fieldEdit.Kind;
                field.Label = string.IsNullOrWhiteSpace(fieldEdit.Label) ? null : fieldEdit.Label.Trim();
                var sensitive = fieldEdit.IsSensitive;
                field.Flags = (fieldEdit.Flags & ~FieldFlags.Sensitive) | (sensitive ? FieldFlags.Sensitive : FieldFlags.None);
                if (fieldEdit.Kind.IsMultiline())
                {
                    field.Flags |= FieldFlags.Multiline;
                }

                if (sensitive)
                {
                    if (fieldEdit.SecretChanged && fieldEdit.Secret is { } draft)
                    {
                        ReplaceSecret(entry, field, draft, now);
                    }
                    else if (field.Sealed is null && !string.IsNullOrEmpty(field.Text))
                    {
                        // A field switched to "sensitive": seal its former plain value.
                        field.Sealed = _fields.SealChars(field.Text, entry.Id, field.Id);
                        field.Modified = now;
                    }

                    field.Text = null;
                }
                else
                {
                    var text = fieldEdit.Text?.TrimEnd();
                    if (field.Sealed is not null && text is null)
                    {
                        // Switched from sensitive to plain without new text: keep the old value visible.
                        using var plain = _fields.OpenChars(field.Sealed, entry.Id, field.Id);
                        text = plain.Span.ToString();
                    }

                    if (!string.Equals(field.Text, text, StringComparison.Ordinal))
                    {
                        field.Modified = now;
                    }

                    field.Text = string.IsNullOrEmpty(text) ? null : text;
                    field.Sealed = null;
                }

                fields.Add(field);
            }

            entry.Fields = fields;
            entry.Kind = edit.Kind;
            entry.Title = edit.Title.Trim();
            entry.Favorite = edit.Favorite;
            entry.Tags = NormalizeTags(edit.Tags);
            entry.Expires = edit.Expires;
            entry.AutoType = string.IsNullOrWhiteSpace(edit.AutoType) ? null : edit.AutoType.Trim();
            entry.Modified = now;
            var liveIds = fields.Select(f => f.Id).ToHashSet();
            entry.History.RemoveAll(h => !liveIds.Contains(h.FieldId));
            if (isNew)
            {
                Document.Entries.Add(entry);
            }
        }

        Commit();
        return entry;
    }

    private void ReplaceSecret(Entry entry, Field field, SecretDraft draft, DateTimeOffset now)
    {
        var old = field.Sealed;
        if (old is not null)
        {
            using var oldPlain = _fields.OpenChars(old, entry.Id, field.Id);
            using var lease = draft.Read();
            if (oldPlain.ContentEquals(lease.Chars))
            {
                return; // unchanged
            }

            if (KeepPasswordHistory && field.Kind.KeepsHistory() && oldPlain.Length > 0)
            {
                var item = new HistoryItem
                {
                    FieldId = field.Id,
                    ReplacedAt = now,
                    Value = new SealedValue([]),
                };
                item.Value = _fields.SealChars(oldPlain.Span, entry.Id, item.Id);
                entry.History.Insert(0, item);
            }
        }

        // A password that comes back is no longer "history".
        using (var current = draft.ToCharBuffer())
        {
            entry.History.RemoveAll(item =>
            {
                if (item.FieldId != field.Id)
                {
                    return false;
                }

                if (!_fields.TryOpen(item.Value, entry.Id, item.Id, out var plain))
                {
                    return false;
                }

                using (plain)
                using (var chars = SecureCharBuffer.FromUtf8(plain!.Span))
                {
                    return chars.ContentEquals(current.Span);
                }
            });
        }

        if (entry.History.Count > Entry.MaxHistory)
        {
            entry.History.RemoveRange(Entry.MaxHistory, entry.History.Count - Entry.MaxHistory);
        }

        field.Sealed = draft.IsEmpty ? null : _fields.SealDraft(draft, entry.Id, field.Id);
        field.Modified = now;
    }

    /// <summary>Seals plain UTF-8 (import/migration paths) into a field.</summary>
    public void SetFieldSecret(Entry entry, Field field, ReadOnlySpan<byte> utf8)
    {
        lock (_gate)
        {
            field.Sealed = utf8.IsEmpty ? null : _fields.Seal(utf8, entry.Id, field.Id);
            field.Flags |= FieldFlags.Sensitive;
            field.Text = null;
        }
    }

    public void AddEntry(Entry entry)
    {
        lock (_gate)
        {
            Document.Entries.Add(entry);
        }

        Commit();
    }

    public void AddEntries(IEnumerable<Entry> entries)
    {
        lock (_gate)
        {
            Document.Entries.AddRange(entries);
        }

        Commit();
    }

    public bool MoveToTrash(Guid id)
    {
        lock (_gate)
        {
            var entry = Document.Entries.FirstOrDefault(e => e.Id == id);
            if (entry is null)
            {
                return false;
            }

            Document.Entries.Remove(entry);
            entry.Deleted = DateTimeOffset.Now;
            Document.Trash.Insert(0, entry);
        }

        Commit();
        return true;
    }

    public bool RestoreFromTrash(Guid id)
    {
        lock (_gate)
        {
            var entry = Document.Trash.FirstOrDefault(e => e.Id == id);
            if (entry is null)
            {
                return false;
            }

            Document.Trash.Remove(entry);
            entry.Deleted = null;
            Document.Entries.Add(entry);
        }

        Commit();
        return true;
    }

    public bool Purge(Guid id)
    {
        lock (_gate)
        {
            if (Document.Trash.RemoveAll(e => e.Id == id) == 0)
            {
                return false;
            }
        }

        Commit();
        return true;
    }

    public int EmptyTrash()
    {
        int count;
        lock (_gate)
        {
            count = Document.Trash.Count;
            Document.Trash.Clear();
            if (count > 0)
            {
                Document.AddAudit(AuditKind.TrashEmptied, count.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        if (count > 0)
        {
            Commit();
        }

        return count;
    }

    /// <summary>Permanently removes trash older than <paramref name="retention"/>.</summary>
    public int PurgeExpiredTrash(TimeSpan retention)
    {
        int removed;
        var cutoff = DateTimeOffset.Now - retention;
        lock (_gate)
        {
            removed = Document.Trash.RemoveAll(e => e.Deleted is { } deleted && deleted < cutoff);
        }

        if (removed > 0)
        {
            Commit();
        }

        return removed;
    }

    public Entry? Duplicate(Guid id, string copySuffix)
    {
        Entry copy;
        lock (_gate)
        {
            var source = Document.Entries.FirstOrDefault(e => e.Id == id);
            if (source is null)
            {
                return null;
            }

            var now = DateTimeOffset.Now;
            copy = new Entry
            {
                Kind = source.Kind,
                Title = $"{source.Title} {copySuffix}".Trim(),
                Favorite = false,
                Tags = [.. source.Tags],
                Created = now,
                Modified = now,
                Expires = source.Expires,
                AutoType = source.AutoType,
            };

            foreach (var field in source.Fields)
            {
                if (field.Kind == FieldKind.Totp)
                {
                    // Two entries silently producing the same codes would be impossible to tell apart.
                    continue;
                }

                var clone = new Field
                {
                    Kind = field.Kind,
                    Label = field.Label,
                    Text = field.Text,
                    Flags = field.Flags,
                    Modified = now,
                };
                if (field.Sealed is { } value)
                {
                    clone.Sealed = _fields.Reseal(value, source.Id, field.Id, copy.Id, clone.Id);
                }

                copy.Fields.Add(clone);
            }

            var index = Document.Entries.IndexOf(source);
            Document.Entries.Insert(index + 1, copy);
        }

        Commit();
        return copy;
    }

    public void SetFavorite(Guid id, bool favorite)
    {
        lock (_gate)
        {
            if (Document.Entries.FirstOrDefault(e => e.Id == id) is not { } entry || entry.Favorite == favorite)
            {
                return;
            }

            entry.Favorite = favorite;
            entry.Modified = DateTimeOffset.Now;
        }

        Commit();
    }

    /// <summary>Records that an entry was used (copied, opened, typed). Persisted lazily.</summary>
    public void RecordUse(Guid id)
    {
        lock (_gate)
        {
            if (Document.Entries.FirstOrDefault(e => e.Id == id) is not { } entry)
            {
                return;
            }

            entry.LastUsed = DateTimeOffset.Now;
            entry.UseCount++;
        }

        Interlocked.Increment(ref _revision);
        Changed?.Invoke(this, EventArgs.Empty);
        RequestSaveDebounced(TimeSpan.FromSeconds(4));
    }

    public void RenameTag(string from, string to)
    {
        to = to.Trim();
        if (to.Length == 0)
        {
            return;
        }

        lock (_gate)
        {
            foreach (var entry in Document.Entries.Concat(Document.Trash))
            {
                if (entry.Tags.RemoveAll(t => string.Equals(t, from, StringComparison.OrdinalIgnoreCase)) > 0)
                {
                    entry.Tags.Add(to);
                    entry.Tags = NormalizeTags(entry.Tags);
                }
            }

            foreach (var info in Document.TagInfos.Where(t => string.Equals(t.Name, from, StringComparison.OrdinalIgnoreCase)))
            {
                info.Name = to;
            }
        }

        Commit();
    }

    public void DeleteTag(string tag)
    {
        lock (_gate)
        {
            foreach (var entry in Document.Entries.Concat(Document.Trash))
            {
                entry.Tags.RemoveAll(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase));
            }

            Document.TagInfos.RemoveAll(t => string.Equals(t.Name, tag, StringComparison.OrdinalIgnoreCase));
        }

        Commit();
    }

    public void Rename(string name)
    {
        lock (_gate)
        {
            Document.Name = name.Trim();
        }

        Commit();
    }

    public static List<string> NormalizeTags(IEnumerable<string> tags)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (var raw in tags)
        {
            var tag = raw.Trim();
            if (tag.Length > 0 && seen.Add(tag))
            {
                result.Add(tag);
            }
        }

        result.Sort(StringComparer.CurrentCultureIgnoreCase);
        return result;
    }

    // ================================================================ secrets

    /// <summary>Opens a sensitive field. The caller owns (and must dispose) the plaintext.</summary>
    public SecureCharBuffer Reveal(Entry entry, Field field)
    {
        ThrowIfDisposed();
        if (field.Sealed is { } value)
        {
            return _fields.OpenChars(value, entry.Id, field.Id);
        }

        return SecureCharBuffer.CopyOf(field.Text ?? string.Empty);
    }

    public SecureCharBuffer RevealHistory(Entry entry, HistoryItem item)
    {
        ThrowIfDisposed();
        return _fields.OpenChars(item.Value, entry.Id, item.Id);
    }

    public bool TryReveal(Entry entry, Field field, out SecureCharBuffer? plain)
    {
        try
        {
            plain = Reveal(entry, field);
            return true;
        }
        catch (VaultException)
        {
            plain = null;
            return false;
        }
    }

    /// <summary>Puts a former password back in use; the current one becomes history.</summary>
    public bool RestoreHistory(Guid entryId, Guid historyId)
    {
        lock (_gate)
        {
            var entry = Document.Entries.FirstOrDefault(e => e.Id == entryId);
            var item = entry?.History.FirstOrDefault(h => h.Id == historyId);
            var field = item is null ? null : entry!.Fields.FirstOrDefault(f => f.Id == item.FieldId);
            if (entry is null || item is null || field is null)
            {
                return false;
            }

            using var old = _fields.OpenChars(item.Value, entry.Id, item.Id);
            using var draft = SecretDraft.From(old.Span);
            ReplaceSecret(entry, field, draft, DateTimeOffset.Now);
            entry.Modified = DateTimeOffset.Now;
        }

        Commit();
        return true;
    }

    public bool ForgetHistory(Guid entryId, Guid historyId)
    {
        lock (_gate)
        {
            if (Document.Entries.FirstOrDefault(e => e.Id == entryId) is not { } entry || entry.History.RemoveAll(h => h.Id == historyId) == 0)
            {
                return false;
            }
        }

        Commit();
        return true;
    }

    public int ClearHistory(Guid? entryId = null)
    {
        var removed = 0;
        lock (_gate)
        {
            foreach (var entry in Document.Entries.Where(e => entryId is null || e.Id == entryId))
            {
                removed += entry.History.Count;
                entry.History.Clear();
            }
        }

        if (removed > 0)
        {
            Commit();
        }

        return removed;
    }

    public int HistoryCount => Document.Entries.Sum(e => e.History.Count);

    // ============================================================ two-factor

    public void SetTotp(Guid entryId, OtpSetup setup)
    {
        lock (_gate)
        {
            var entry = Document.Entries.FirstOrDefault(e => e.Id == entryId) ?? throw new InvalidOperationException("Unknown entry.");
            var field = entry.Totp;
            if (field is null)
            {
                field = new Field { Kind = FieldKind.Totp, Flags = FieldFlags.Sensitive };
                var notesIndex = entry.Fields.FindIndex(f => f.Kind == FieldKind.Notes);
                entry.Fields.Insert(notesIndex >= 0 ? notesIndex : entry.Fields.Count, field);
            }

            using var payload = TotpPayload.Encode(setup.Secret.Span, setup.Config);
            field.Sealed = _fields.Seal(payload.Span, entry.Id, field.Id);
            field.Modified = DateTimeOffset.Now;
            entry.Modified = field.Modified;
        }

        Commit();
    }

    /// <summary>Seals a token payload into a (possibly new) field of an entry being created.</summary>
    public SealedValue SealTotp(Guid entryId, Guid fieldId, OtpSetup setup)
    {
        using var payload = TotpPayload.Encode(setup.Secret.Span, setup.Config);
        return _fields.Seal(payload.Span, entryId, fieldId);
    }

    public bool RemoveTotp(Guid entryId)
    {
        lock (_gate)
        {
            if (Document.Entries.FirstOrDefault(e => e.Id == entryId) is not { } entry || entry.Fields.RemoveAll(f => f.Kind == FieldKind.Totp) == 0)
            {
                return false;
            }

            entry.Modified = DateTimeOffset.Now;
        }

        Commit();
        return true;
    }

    /// <summary>The code valid at <paramref name="at"/>. The secret is unsealed, used and wiped within this call.</summary>
    public TotpCode? GenerateTotp(Entry entry, Field field, DateTimeOffset at)
    {
        if (_disposed || field.Sealed is null || !_fields.TryOpen(field.Sealed, entry.Id, field.Id, out var payload))
        {
            return null;
        }

        using (payload)
        {
            if (!TotpPayload.TryDecode(payload!.Span, out var config, out var offset))
            {
                return null;
            }

            return Totp.Generate(payload.Span[offset..], config, at);
        }
    }

    public TotpConfig? TotpConfigOf(Entry entry, Field field)
    {
        if (_disposed || field.Sealed is null || !_fields.TryOpen(field.Sealed, entry.Id, field.Id, out var payload))
        {
            return null;
        }

        using (payload)
        {
            return TotpPayload.TryDecode(payload!.Span, out var config, out _) ? config : null;
        }
    }

    /// <summary>Re-creates the otpauth:// link (export only).</summary>
    public SecureCharBuffer? ExportTotpUri(Entry entry, Field field)
    {
        if (field.Sealed is null || !_fields.TryOpen(field.Sealed, entry.Id, field.Id, out var payload))
        {
            return null;
        }

        using (payload)
        {
            if (!TotpPayload.TryDecode(payload!.Span, out var config, out var offset))
            {
                return null;
            }

            var output = new SecureCharBuffer(128);
            OtpAuthUri.Format(payload.Span[offset..], config, output);
            return output;
        }
    }

    // ============================================================== analysis

    /// <summary>Keyed digest of a secret, for reuse detection without keeping plaintext around.</summary>
    public byte[] IndexDigest(ReadOnlySpan<char> secret)
    {
        var count = Encoding.UTF8.GetByteCount(secret);
        using var utf8 = SecureBuffer.Allocate(count);
        Encoding.UTF8.GetBytes(secret, utf8.Span);
        using var lease = _indexKey.Unprotect();
        return HMACSHA256.HashData(lease.Span, utf8.Span);
    }

    // ============================================================== security

    /// <summary>
    /// Changes the master password. With <paramref name="rotateKey"/> a brand-new VMK is
    /// generated and every sealed value is re-encrypted, so a copy of the old file plus
    /// the old password reveals nothing about future data. Rotation also issues a new
    /// recovery key (returned) and drops the Windows Hello slot, which must be re-enrolled.
    /// </summary>
    public async Task<SecureCharBuffer?> ChangeMasterPasswordAsync(SecretDraft newPassword, KdfParameters kdf, bool rotateKey, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        using var passwordUtf8 = newPassword.ToUtf8();
        if (!rotateKey)
        {
            using var vmkCopy = CopyVmk();
            var slot = await Task.Run(() => VaultCodec.CreatePasswordSlot(VaultId, passwordUtf8.Span, vmkCopy.Span, kdf, cancellationToken), cancellationToken).ConfigureAwait(true);

            lock (_gate)
            {
                ReplaceSlot(slot);
                Document.AddAudit(AuditKind.MasterPasswordChanged);
            }

            await SaveNowAsync(forceBackup: true).ConfigureAwait(true);
            return null;
        }

        using var newVmk = SecureBuffer.Allocate(VaultCodec.VmkBytes);
        RandomNumberGenerator.Fill(newVmk.Span);
        var passwordSlot = await Task.Run(() => VaultCodec.CreatePasswordSlot(VaultId, passwordUtf8.Span, newVmk.Span, kdf, cancellationToken), cancellationToken).ConfigureAwait(true);
        var code = RecoveryKey.Generate(out var recoveryKey);
        RecoverySlot recoverySlot;
        using (recoveryKey)
        {
            recoverySlot = VaultCodec.CreateRecoverySlot(VaultId, recoveryKey.Span, newVmk.Span);
        }

        await FlushAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
        lock (_gate)
        {
            var newCipher = FieldCipher.FromVmk(newVmk.Span, VaultId);
            DocumentRekey.Rewrap(Document, _fields, newCipher);
            var oldCipher = _fields;
            var oldVmk = _vmk;
            var oldIndex = _indexKey;
            _fields = newCipher;
            _vmk = ProtectedBuffer.Create(newVmk.Span);
            _indexKey = CreateIndexKey(newVmk.Span, VaultId);
            oldCipher.Dispose();
            oldVmk.Dispose();
            oldIndex.Dispose();

            _header = new VaultHeader
            {
                VaultId = VaultId,
                WriteCounter = _header.WriteCounter,
                Flags = _header.Flags,
                Slots = [passwordSlot, recoverySlot],
            };
            Document.AddAudit(AuditKind.MasterPasswordChanged, "rotated");
        }

        await SaveNowAsync(forceBackup: false).ConfigureAwait(true);
        return code;
    }

    public async Task UpgradeKdfAsync(SecretDraft password, KdfParameters kdf, CancellationToken cancellationToken = default)
    {
        if (!await VerifyPasswordAsync(password, cancellationToken).ConfigureAwait(true))
        {
            throw new VaultException(VaultError.InvalidPassword, "The master password is not correct.");
        }

        using var passwordUtf8 = password.ToUtf8();
        using var vmkCopy = CopyVmk();
        var slot = await Task.Run(() => VaultCodec.CreatePasswordSlot(VaultId, passwordUtf8.Span, vmkCopy.Span, kdf, cancellationToken), cancellationToken).ConfigureAwait(true);
        lock (_gate)
        {
            ReplaceSlot(slot);
            Document.AddAudit(AuditKind.KdfUpgraded, kdf.Describe());
        }

        await SaveNowAsync(forceBackup: true).ConfigureAwait(true);
    }

    public async Task<SecureCharBuffer> RegenerateRecoveryKeyAsync()
    {
        var code = RecoveryKey.Generate(out var key);
        using (key)
        using (var vmkCopy = CopyVmk())
        {
            var slot = VaultCodec.CreateRecoverySlot(VaultId, key.Span, vmkCopy.Span);
            lock (_gate)
            {
                ReplaceSlot(slot);
                Document.AddAudit(AuditKind.RecoveryKeyRegenerated);
            }
        }

        await SaveNowAsync(forceBackup: true).ConfigureAwait(true);
        return code;
    }

    public async Task EnableHelloAsync(IHelloProvider hello, CancellationToken cancellationToken = default)
    {
        var name = $"OnlyYours-{VaultId:N}";
        var challenge = RandomNumberGenerator.GetBytes(32);
        var signature = await hello.SignAsync(name, challenge, createIfMissing: true, cancellationToken).ConfigureAwait(true)
            ?? throw new VaultException(VaultError.Cancelled, "Windows Hello enrollment was cancelled.");
        try
        {
            using var vmkCopy = CopyVmk();
            var slot = VaultCodec.CreateHelloSlot(VaultId, name, challenge, signature, vmkCopy.Span);
            lock (_gate)
            {
                ReplaceSlot(slot);
                Document.AddAudit(AuditKind.HelloEnabled);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(signature);
        }

        await SaveNowAsync(forceBackup: false).ConfigureAwait(true);
    }

    public async Task DisableHelloAsync(IHelloProvider? hello)
    {
        var slot = _header.HelloSlot;
        if (slot is null)
        {
            return;
        }

        lock (_gate)
        {
            _header.Slots.Remove(slot);
            Document.AddAudit(AuditKind.HelloDisabled);
        }

        await SaveNowAsync(forceBackup: false).ConfigureAwait(true);
        if (hello is not null)
        {
            try
            {
                await hello.DeleteAsync(slot.CredentialName).ConfigureAwait(true);
            }
            catch
            {
                // The credential may already be gone; the slot is what matters.
            }
        }
    }

    private void ReplaceSlot(KeySlot slot)
    {
        _header.Slots.RemoveAll(s => s.Kind == slot.Kind);
        _header.Slots.Add(slot);
        _header.Slots.Sort((a, b) => ((byte)a.Kind).CompareTo((byte)b.Kind));
    }

    private SecureBuffer CopyVmk()
    {
        using var lease = _vmk.Unprotect();
        return SecureBuffer.CopyOf(lease.Span);
    }

    private static ProtectedBuffer CreateIndexKey(ReadOnlySpan<byte> vmk, Guid vaultId)
    {
        Span<byte> key = stackalloc byte[32];
        try
        {
            VaultCodec.DeriveIndexKey(vmk, vaultId, key);
            return ProtectedBuffer.Create(key);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    // ================================================================ backups

    public IReadOnlyList<BackupInfo> ListBackups() => _storage.ListBackups(VaultId);

    /// <summary>
    /// Replaces the current content with a backup. Backups encrypted with the current
    /// key open directly; older ones (before a key rotation) need their password.
    /// </summary>
    public async Task RestoreBackupAsync(BackupInfo backup, SecretDraft? password, CancellationToken cancellationToken = default)
    {
        var file = await Task.Run(() => VaultStorage.ReadFile(backup.Path), cancellationToken).ConfigureAwait(true);
        var header = VaultHeader.Parse(file);
        if (header.VaultId != VaultId)
        {
            throw new VaultException(VaultError.NotAVault, "The backup belongs to a different vault.");
        }

        VaultDocument restored;
        try
        {
            using var vmk = _vmk.Unprotect();
            using var plain = VaultCodec.Open(file, header, vmk.Span);
            restored = DocumentSerializer.Deserialize(plain.Span);
        }
        catch (VaultException) when (password is not null && header.PasswordSlot is { } slot)
        {
            using var passwordUtf8 = password.ToUtf8();
            using var oldVmk = SecureBuffer.Allocate(VaultCodec.VmkBytes);
            var ok = await Task.Run(
                () =>
                {
                    Span<byte> kek = stackalloc byte[32];
                    try
                    {
                        VaultCodec.DerivePasswordKek(header.VaultId, slot, passwordUtf8.Span, kek, cancellationToken);
                        return VaultCodec.TryUnwrap(slot, header.VaultId, kek, oldVmk.Span);
                    }
                    finally
                    {
                        CryptographicOperations.ZeroMemory(kek);
                    }
                },
                cancellationToken).ConfigureAwait(true);
            if (!ok)
            {
                throw new VaultException(VaultError.InvalidPassword, "The password does not open this backup.");
            }

            using var plain = VaultCodec.Open(file, header, oldVmk.Span);
            restored = DocumentSerializer.Deserialize(plain.Span);
            using var oldCipher = FieldCipher.FromVmk(oldVmk.Span, VaultId);
            DocumentRekey.Rewrap(restored, oldCipher, _fields);
        }

        lock (_gate)
        {
            Document.Entries.Clear();
            Document.Entries.AddRange(restored.Entries);
            Document.Trash.Clear();
            Document.Trash.AddRange(restored.Trash);
            Document.Contacts.Clear();
            Document.Contacts.AddRange(restored.Contacts);
            Document.TagInfos.Clear();
            Document.TagInfos.AddRange(restored.TagInfos);
            Document.Identity = restored.Identity;
            Document.Name = restored.Name;
            Document.AddAudit(AuditKind.BackupRestored, backup.Created.ToString("u", System.Globalization.CultureInfo.InvariantCulture));
            _header.WriteCounter = Math.Max(_header.WriteCounter, header.WriteCounter);
        }

        await SaveNowAsync(forceBackup: true).ConfigureAwait(true);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // ============================================================ persistence

    /// <summary>Marks the document changed and schedules a save.</summary>
    public void Commit()
    {
        Interlocked.Increment(ref _revision);
        Changed?.Invoke(this, EventArgs.Empty);
        RequestSave();
    }

    public void RequestSave(bool forceBackup = false)
    {
        if (_disposed)
        {
            return;
        }

        _debounce?.Dispose();
        _debounce = null;

        SecureBuffer snapshot;
        VaultHeader header;
        ProtectedBuffer key;
        lock (_gate)
        {
            Document.Modified = DateTimeOffset.Now;
            snapshot = DocumentSerializer.Serialize(Document);
            _header.WriteCounter++;
            header = _header.CloneForSave();

            // The snapshot must be sealed with the very key its field values were sealed for.
            key = _vmk;
        }

        lock (_saveGate)
        {
            var force = forceBackup || (_pending?.ForceBackup ?? false);
            _pending?.Snapshot.Dispose();
            _pending = (snapshot, header, force, key);
            SaveState = SaveState.Saving;
            if (!_writerRunning)
            {
                _writerRunning = true;
                _writer = Task.Run(WriterLoop);
            }
        }
    }

    public void RequestSaveDebounced(TimeSpan delay)
    {
        if (_disposed)
        {
            return;
        }

        _debounce?.Dispose();
        _debounce = new Timer(_ => RequestSave(), null, delay, Timeout.InfiniteTimeSpan);
    }

    /// <summary>Saves and waits for the write to finish; throws if it failed.</summary>
    public async Task SaveNowAsync(bool forceBackup = false)
    {
        RequestSave(forceBackup);
        await FlushAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
        if (SaveState == SaveState.Failed && LastSaveError is { } error)
        {
            throw error;
        }
    }

    /// <summary>Waits for pending writes, up to <paramref name="timeout"/>.</summary>
    public async Task FlushAsync(TimeSpan timeout)
    {
        if (_debounce is not null)
        {
            RequestSave();
        }

        Task writer;
        lock (_saveGate)
        {
            writer = _writer;
        }

        try
        {
            await writer.WaitAsync(timeout).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // The writer keeps going; the caller decided not to wait any longer.
        }
    }

    public bool HasPendingWrites
    {
        get
        {
            lock (_saveGate)
            {
                return _writerRunning || _pending is not null || _debounce is not null;
            }
        }
    }

    private void WriterLoop()
    {
        while (true)
        {
            (SecureBuffer Snapshot, VaultHeader Header, bool ForceBackup, ProtectedBuffer Key) item;
            lock (_saveGate)
            {
                if (_pending is null)
                {
                    _writerRunning = false;
                    return;
                }

                item = _pending.Value;
                _pending = null;
            }

            SaveResult result;
            try
            {
                byte[] file;
                using (var vmk = item.Key.Unprotect())
                {
                    file = VaultCodec.Seal(item.Header, vmk.Span, item.Snapshot.Span);
                }

                _storage.WriteAtomic(file, item.Header.VaultId, item.Header.WriteCounter, item.ForceBackup);
                var now = DateTimeOffset.Now;
                LastSaved = now;
                LastSaveError = null;
                result = new SaveResult(true, item.Header.WriteCounter, now, null);
            }
            catch (VaultException error)
            {
                LastSaveError = error;
                result = new SaveResult(false, item.Header.WriteCounter, DateTimeOffset.Now, error);
            }
            catch (ObjectDisposedException)
            {
                // The key was rotated or the vault locked meanwhile: a newer snapshot (if any) follows.
                continue;
            }
            catch (Exception error)
            {
                var wrapped = new VaultException(VaultError.IoFailure, "Unexpected failure while writing the vault.", error);
                LastSaveError = wrapped;
                result = new SaveResult(false, item.Header.WriteCounter, DateTimeOffset.Now, wrapped);
            }
            finally
            {
                item.Snapshot.Dispose();
            }

            lock (_saveGate)
            {
                if (_pending is null)
                {
                    SaveState = result.Success ? SaveState.Saved : SaveState.Failed;
                }
            }

            SaveCompleted?.Invoke(this, result);
        }
    }

    /// <summary>Flushes within <paramref name="budget"/> and wipes every key.</summary>
    public async Task CloseAsync(TimeSpan budget)
    {
        if (_disposed)
        {
            return;
        }

        await FlushAsync(budget).ConfigureAwait(false);
        Dispose();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new VaultException(VaultError.Locked, "The vault is locked.");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _debounce?.Dispose();
        _debounce = null;
        lock (_saveGate)
        {
            _pending?.Snapshot.Dispose();
            _pending = null;
        }

        _vmk.Dispose();
        _fields.Dispose();
        _indexKey.Dispose();
    }
}

/// <summary>Re-encrypts every sealed value of a document from one field cipher to another.</summary>
public static class DocumentRekey
{
    public static void Rewrap(VaultDocument document, FieldCipher from, FieldCipher to)
    {
        foreach (var entry in document.Entries.Concat(document.Trash))
        {
            foreach (var field in entry.Fields)
            {
                if (field.Sealed is { } value)
                {
                    field.Sealed = from.Rewrap(value, entry.Id, field.Id, to);
                }
            }

            foreach (var item in entry.History)
            {
                item.Value = from.Rewrap(item.Value, entry.Id, item.Id, to);
            }
        }

        if (document.Identity is { } identity)
        {
            identity.EcdhPrivate = from.Rewrap(identity.EcdhPrivate, IdentitySlots.Owner, IdentitySlots.Ecdh, to);
            identity.MlKemSeed = from.Rewrap(identity.MlKemSeed, IdentitySlots.Owner, IdentitySlots.MlKem, to);
            identity.MlDsaSeed = from.Rewrap(identity.MlDsaSeed, IdentitySlots.Owner, IdentitySlots.MlDsa, to);
            identity.EcdsaPrivate = from.Rewrap(identity.EcdsaPrivate, IdentitySlots.Owner, IdentitySlots.Ecdsa, to);
        }
    }
}

/// <summary>Fixed owner/slot identifiers that bind the identity keys' sealed values.</summary>
public static class IdentitySlots
{
    public static readonly Guid Owner = new("5a1e7c10-0000-4000-8000-000000000001");
    public static readonly Guid Ecdh = new("5a1e7c10-0000-4000-8000-0000000000e1");
    public static readonly Guid MlKem = new("5a1e7c10-0000-4000-8000-0000000000e2");
    public static readonly Guid MlDsa = new("5a1e7c10-0000-4000-8000-0000000000e3");
    public static readonly Guid Ecdsa = new("5a1e7c10-0000-4000-8000-0000000000e4");
}
