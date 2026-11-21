using System.Numerics;

namespace OnlyYours.Core.Security.Crypto;

/// <summary>
/// PADMÉ length padding (Nikitin et al., "Reducing Metadata Leakage from Encrypted
/// Files and Communication with PURBs", PETS 2019). An encrypted vault would
/// otherwise reveal its exact plaintext size — and with it, roughly how many
/// entries it holds. PADMÉ leaks only O(log log n) bits with at most ~12 % overhead.
/// </summary>
public static class Padme
{
    public const int MinimumSize = 512;

    public static long PaddedLength(long length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        if (length <= MinimumSize)
        {
            return MinimumSize;
        }

        var e = 63 - BitOperations.LeadingZeroCount((ulong)length);
        var s = 64 - BitOperations.LeadingZeroCount((ulong)e);
        var lastBits = e - s;
        var mask = (1L << lastBits) - 1;
        return (length + mask) & ~mask;
    }
}
