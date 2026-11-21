namespace OnlyYours.Core.Vault.Model;

/// <summary>
/// A sensitive value as it lives in memory and on disk: AES-256-GCM ciphertext under
/// the vault's field key, bound to its owner (entry) and slot (field) by the
/// associated data. Holding a <see cref="SealedValue"/> reveals nothing but an
/// upper bound on the secret's length (rounded to 32 bytes).
/// </summary>
public sealed class SealedValue(byte[] blob)
{
    public byte[] Blob { get; } = blob;

    public override string ToString() => $"SealedValue({Blob.Length} bytes)";
}

public sealed class Field
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public FieldKind Kind { get; set; }

    /// <summary>Custom label; null means "use the localized name of <see cref="Kind"/>".</summary>
    public string? Label { get; set; }

    /// <summary>Plain value for non-sensitive fields.</summary>
    public string? Text { get; set; }

    /// <summary>Sealed value for sensitive fields.</summary>
    public SealedValue? Sealed { get; set; }

    public FieldFlags Flags { get; set; }

    public DateTimeOffset Modified { get; set; }

    public List<byte[]> Unknown { get; } = [];

    public bool IsSensitive => Sealed is not null || (Flags & FieldFlags.Sensitive) != 0;

    public bool IsMultiline => (Flags & FieldFlags.Multiline) != 0 || Kind.IsMultiline();

    public bool HasValue => Sealed is not null || !string.IsNullOrEmpty(Text);

    public Field CloneShallow() => new()
    {
        Id = Id,
        Kind = Kind,
        Label = Label,
        Text = Text,
        Sealed = Sealed,
        Flags = Flags,
        Modified = Modified,
    };
}

public sealed class HistoryItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid FieldId { get; set; }

    public required SealedValue Value { get; set; }

    public DateTimeOffset ReplacedAt { get; set; }
}

public sealed class Entry
{
    public const int MaxHistory = 30;

    public Guid Id { get; set; } = Guid.NewGuid();

    public EntryKind Kind { get; set; }

    public string Title { get; set; } = string.Empty;

    public bool Favorite { get; set; }

    public List<string> Tags { get; set; } = [];

    public DateTimeOffset Created { get; set; }

    public DateTimeOffset Modified { get; set; }

    public DateTimeOffset? LastUsed { get; set; }

    public int UseCount { get; set; }

    /// <summary>Optional expiry reminder (e.g. a card or a password to rotate).</summary>
    public DateTimeOffset? Expires { get; set; }

    /// <summary>Set while the entry sits in the trash.</summary>
    public DateTimeOffset? Deleted { get; set; }

    /// <summary>Custom auto-type sequence; null uses the default {USERNAME}{TAB}{PASSWORD}{ENTER}.</summary>
    public string? AutoType { get; set; }

    public List<Field> Fields { get; set; } = [];

    public List<HistoryItem> History { get; set; } = [];

    public List<byte[]> Unknown { get; } = [];

    public Field? FirstField(FieldKind kind) => Fields.FirstOrDefault(f => f.Kind == kind);

    public string? TextOf(FieldKind kind) => FirstField(kind)?.Text;

    public Field? PrimaryPassword => FirstField(FieldKind.Password) ?? FirstField(FieldKind.Passphrase) ?? FirstField(FieldKind.ApiSecret);

    public Field? Totp => FirstField(FieldKind.Totp);

    public bool HasTotp => Totp?.Sealed is not null;

    public string? Url => Fields.FirstOrDefault(f => f.Kind.IsUrl() && !string.IsNullOrWhiteSpace(f.Text))?.Text;

    public string? Username => TextOf(FieldKind.Username) ?? TextOf(FieldKind.Email);

    /// <summary>Host of the first URL, without "www.", for display and matching.</summary>
    public string? Host => UrlHost(Url);

    public static string? UrlHost(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var raw = url.Trim();
        if (!raw.Contains("://", StringComparison.Ordinal))
        {
            raw = "https://" + raw;
        }

        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host))
        {
            return null;
        }

        return uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host;
    }
}

public enum AuditKind
{
    VaultCreated = 0,
    Unlocked = 1,
    UnlockedWithRecovery = 2,
    UnlockedWithHello = 3,
    MasterPasswordChanged = 4,
    RecoveryKeyRegenerated = 5,
    HelloEnabled = 6,
    HelloDisabled = 7,
    Exported = 8,
    Imported = 9,
    Migrated = 10,
    BackupRestored = 11,
    KdfUpgraded = 12,
    PackageShared = 13,
    PackageReceived = 14,
    RollbackDetected = 15,
    TrashEmptied = 16,
}

public sealed record AuditEvent(DateTimeOffset At, AuditKind Kind, string? Detail);

public sealed class Contact
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public required byte[] PublicBundle { get; set; }

    public DateTimeOffset Added { get; set; }

    public bool Verified { get; set; }

    public string? Note { get; set; }
}

/// <summary>Private half of the vault identity used for sealed sharing; every key is itself sealed.</summary>
public sealed class IdentityRecord
{
    public DateTimeOffset Created { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public required SealedValue EcdhPrivate { get; set; }

    public required SealedValue MlKemSeed { get; set; }

    public required SealedValue MlDsaSeed { get; set; }

    public required SealedValue EcdsaPrivate { get; set; }

    public required byte[] PublicBundle { get; set; }
}

public sealed class TagInfo
{
    public required string Name { get; set; }

    public int Color { get; set; }
}

/// <summary>The decrypted content of a vault.</summary>
public sealed class VaultDocument
{
    public const int CurrentSchema = 1;
    public const int MaxAudit = 400;

    public int Schema { get; set; } = CurrentSchema;

    public string Name { get; set; } = string.Empty;

    public DateTimeOffset Created { get; set; }

    public DateTimeOffset Modified { get; set; }

    public List<Entry> Entries { get; } = [];

    public List<Entry> Trash { get; } = [];

    public IdentityRecord? Identity { get; set; }

    public List<Contact> Contacts { get; } = [];

    public List<AuditEvent> Audit { get; } = [];

    public List<TagInfo> TagInfos { get; } = [];

    public List<byte[]> Unknown { get; } = [];

    public void AddAudit(AuditKind kind, string? detail = null, DateTimeOffset? at = null)
    {
        Audit.Add(new AuditEvent(at ?? DateTimeOffset.Now, kind, detail));
        if (Audit.Count > MaxAudit)
        {
            Audit.RemoveRange(0, Audit.Count - MaxAudit);
        }
    }

    public Entry? Find(Guid id) => Entries.FirstOrDefault(e => e.Id == id) ?? Trash.FirstOrDefault(e => e.Id == id);
}
