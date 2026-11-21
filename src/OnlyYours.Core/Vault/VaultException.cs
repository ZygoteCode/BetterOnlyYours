namespace OnlyYours.Core.Vault;

public enum VaultError
{
    NotFound,
    NotAVault,
    UnsupportedVersion,
    Truncated,
    Corrupted,
    InvalidPassword,
    InvalidRecoveryKey,
    HelloUnavailable,
    HelloFailed,
    IntegrityFailure,
    AccessDenied,
    IoFailure,
    Cancelled,
    Locked,
    UnsafeParameters,
}

/// <summary>
/// Typed vault failure. The interface maps <see cref="Error"/> to localized,
/// actionable copy instead of showing raw exception text.
/// </summary>
public sealed class VaultException : Exception
{
    public VaultException(VaultError error, string message, Exception? inner = null)
        : base(message, inner)
    {
        Error = error;
    }

    public VaultError Error { get; }

    public override string ToString() => $"VaultException({Error}): {Message}";
}
