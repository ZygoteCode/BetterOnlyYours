using OnlyYours.Core.Security.Memory;
using OnlyYours.Core.Vault.Model;

namespace OnlyYours.Core.Vault;

/// <summary>An edited field as produced by the editor. Secrets travel as <see cref="SecretDraft"/>, never as strings.</summary>
public sealed class FieldEdit
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public FieldKind Kind { get; init; }

    public string? Label { get; init; }

    public FieldFlags Flags { get; init; }

    /// <summary>Value of a non-sensitive field.</summary>
    public string? Text { get; init; }

    /// <summary>New value of a sensitive field, read only when <see cref="SecretChanged"/> is true.</summary>
    public SecretDraft? Secret { get; init; }

    public bool SecretChanged { get; init; }

    public bool IsSensitive => Kind.IsSensitive() || (Flags & FieldFlags.Sensitive) != 0;
}

/// <summary>A whole entry as submitted by the editor.</summary>
public sealed class EntryEdit
{
    /// <summary>Null for a new entry.</summary>
    public Guid? EntryId { get; init; }

    public EntryKind Kind { get; init; }

    public string Title { get; init; } = string.Empty;

    public bool Favorite { get; init; }

    public List<string> Tags { get; init; } = [];

    public DateTimeOffset? Expires { get; init; }

    public string? AutoType { get; init; }

    public List<FieldEdit> Fields { get; init; } = [];
}

public enum UnlockMethod
{
    Password,
    RecoveryKey,
    WindowsHello,
    Created,
}

public enum SaveState
{
    Idle,
    Saving,
    Saved,
    Failed,
}

public sealed record SaveResult(bool Success, ulong WriteCounter, DateTimeOffset At, VaultException? Error);

/// <summary>Abstraction over Windows Hello (KeyCredentialManager) so the core stays testable.</summary>
public interface IHelloProvider
{
    Task<bool> IsAvailableAsync();

    /// <summary>
    /// Signs <paramref name="challenge"/> with the TPM-backed key <paramref name="credentialName"/>
    /// after user verification. Returns null if the user cancelled.
    /// </summary>
    Task<byte[]?> SignAsync(string credentialName, byte[] challenge, bool createIfMissing, CancellationToken cancellationToken);

    Task DeleteAsync(string credentialName);
}
