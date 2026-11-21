using System.Runtime.InteropServices;

namespace OnlyYours.Core.Security.Memory;

/// <summary>Live statistics shown by the security center.</summary>
public readonly record struct SecureMemoryStatus(
    int Regions,
    long LockedBytes,
    long UnlockedBytes,
    int ProtectedBuffers,
    bool HardwareProtection);

/// <summary>
/// Process-wide bookkeeping for secure regions: working-set headroom for
/// <c>VirtualLock</c>, a registry of every live region (for emergency wipes) and
/// statistics for the UI.
/// </summary>
public static unsafe class SecureMemory
{
    private static readonly Lock Gate = new();
    private static readonly HashSet<SecureRegion> Live = [];
    private static long _lockedBytes;
    private static long _unlockedBytes;
    private static int _protectedBuffers;
    private static bool _headroomRequested;

    public static int PageSize { get; } = Environment.SystemPageSize;

    /// <summary>True when <c>CryptProtectMemory</c> works on this machine (always on supported Windows).</summary>
    public static bool HardwareProtectionAvailable { get; internal set; } = true;

    public static SecureMemoryStatus Status
    {
        get
        {
            lock (Gate)
            {
                return new SecureMemoryStatus(Live.Count, _lockedBytes, _unlockedBytes, _protectedBuffers, HardwareProtectionAvailable);
            }
        }
    }

    /// <summary>
    /// Raises the minimum working set so that a few hundred kilobytes of secrets can be
    /// locked in RAM. The default quota (a few dozen pages) would make <c>VirtualLock</c>
    /// fail as soon as more than a handful of keys are alive.
    /// </summary>
    public static void EnsureLockHeadroom(long bytes = 4 * 1024 * 1024)
    {
        lock (Gate)
        {
            if (_headroomRequested)
            {
                return;
            }

            _headroomRequested = true;
        }

        GrowWorkingSet((nuint)bytes);
    }

    internal static bool TryLock(void* pointer, nuint size)
    {
        if (Win32.VirtualLock(pointer, size))
        {
            return true;
        }

        if (Marshal.GetLastPInvokeError() == Win32.ERROR_WORKING_SET_QUOTA)
        {
            GrowWorkingSet(Math.Max(size * 4, 1024 * 1024));
            return Win32.VirtualLock(pointer, size);
        }

        return false;
    }

    private static void GrowWorkingSet(nuint extra)
    {
        var process = Win32.GetCurrentProcess();
        if (!Win32.GetProcessWorkingSetSizeEx(process, out var min, out var max, out _))
        {
            return;
        }

        var newMin = min + extra;
        var newMax = Math.Max(max, newMin + extra);
        Win32.SetProcessWorkingSetSizeEx(
            process,
            newMin,
            newMax,
            Win32.QUOTA_LIMITS_HARDWS_MIN_DISABLE | Win32.QUOTA_LIMITS_HARDWS_MAX_DISABLE);
    }

    internal static void Register(SecureRegion region)
    {
        lock (Gate)
        {
            Live.Add(region);
            if (region.Locked)
            {
                _lockedBytes += (long)region.Size;
            }
            else
            {
                _unlockedBytes += (long)region.Size;
            }
        }
    }

    internal static void Unregister(SecureRegion region, nuint size, bool locked)
    {
        lock (Gate)
        {
            if (Live.Remove(region))
            {
                if (locked)
                {
                    _lockedBytes -= (long)size;
                }
                else
                {
                    _unlockedBytes -= (long)size;
                }
            }
        }
    }

    internal static void TrackProtected(int delta)
    {
        lock (Gate)
        {
            _protectedBuffers += delta;
        }
    }

    /// <summary>
    /// Zeroes every live secure region. Called on unrecoverable errors and right
    /// before the process exits, because <see cref="Environment.Exit"/> does not run
    /// finalizers on .NET.
    /// </summary>
    public static void WipeAll()
    {
        SecureRegion[] snapshot;
        lock (Gate)
        {
            snapshot = [.. Live];
        }

        foreach (var region in snapshot)
        {
            try
            {
                region.Scrub();
            }
            catch
            {
                // Best effort: a region released concurrently is already wiped.
            }
        }
    }
}
