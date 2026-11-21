namespace OnlyYours.Core.Security.Crypto;

public enum KdfAlgorithm : byte
{
    Argon2id = 1,
}

public enum KdfStrength
{
    Standard,
    High,
    Extreme,
    Custom,
}

/// <summary>
/// Cost settings of the memory-hard password hash protecting a key slot. They are
/// stored (authenticated) in the vault header, so a vault can be re-keyed to
/// stronger settings at any time without breaking older files.
/// </summary>
public sealed record KdfParameters(KdfAlgorithm Algorithm, uint MemoryKiB, uint Iterations, uint Parallelism)
{
    /// <summary>Guards against a crafted header asking for an absurd amount of work.</summary>
    public const uint MinMemoryKiB = 8 * 1024;
    public const uint MaxMemoryKiB = 4 * 1024 * 1024;
    public const uint MaxIterations = 64;
    public const uint MaxParallelism = 64;

    /// <summary>128 MiB · t=3 · p=4 — about twice the RFC 9106 second recommendation.</summary>
    public static KdfParameters Standard { get; } = new(KdfAlgorithm.Argon2id, 128 * 1024, 3, 4);

    /// <summary>256 MiB · t=3 · p=4 — the default for new vaults.</summary>
    public static KdfParameters High { get; } = new(KdfAlgorithm.Argon2id, 256 * 1024, 3, 4);

    /// <summary>1 GiB · t=4 · p=8 — for the paranoid; roughly a second per unlock on a modern CPU.</summary>
    public static KdfParameters Extreme { get; } = new(KdfAlgorithm.Argon2id, 1024 * 1024, 4, 8);

    public static KdfParameters Default => High;

    public KdfStrength Strength =>
        this == Standard ? KdfStrength.Standard
        : this == High ? KdfStrength.High
        : this == Extreme ? KdfStrength.Extreme
        : KdfStrength.Custom;

    public double MemoryMiB => MemoryKiB / 1024.0;

    public static KdfParameters FromStrength(KdfStrength strength) => strength switch
    {
        KdfStrength.Standard => Standard,
        KdfStrength.Extreme => Extreme,
        _ => High,
    };

    public bool IsWithinBounds =>
        Algorithm == KdfAlgorithm.Argon2id
        && MemoryKiB is >= MinMemoryKiB and <= MaxMemoryKiB
        && Iterations is >= 1 and <= MaxIterations
        && Parallelism is >= 1 and <= MaxParallelism
        && MemoryKiB >= 8 * Parallelism;

    /// <summary>True when <paramref name="other"/> costs strictly more to attack.</summary>
    public bool IsWeakerThan(KdfParameters other) =>
        MemoryKiB < other.MemoryKiB || (MemoryKiB == other.MemoryKiB && Iterations < other.Iterations);

    public string Describe() => $"Argon2id · {MemoryMiB:0.#} MiB · t={Iterations} · p={Parallelism}";

    public override string ToString() => Describe();
}
