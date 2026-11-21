using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace OnlyYours.Core.Security.Memory;

/// <summary>
/// Long-lived key material that is <em>encrypted while idle</em>.
/// <para>
/// Between uses the bytes are sealed with <c>CryptProtectMemory</c> (a per-process
/// key held by the kernel, never in this address space) and the pages are mapped
/// <c>PAGE_NOACCESS</c>, so a stray read faults instead of disclosing anything and a
/// memory dump of the process contains only ciphertext. A <see cref="Lease"/>
/// decrypts the bytes in place for the few microseconds they are needed and seals
/// them again when the last lease is disposed.
/// </para>
/// </summary>
public sealed unsafe class ProtectedBuffer : IDisposable
{
    private readonly SecureRegion _region;
    private readonly int _length;
    private readonly uint _sealSize;
    private readonly Lock _gate = new();
    private int _leases;
    private bool _sealed;
    private bool _disposeRequested;
    private bool _released;

    private ProtectedBuffer(int length)
    {
        _length = length;
        var blocks = Math.Max(length, 1);
        _sealSize = (uint)((blocks + Win32.CRYPTPROTECTMEMORY_BLOCK_SIZE - 1) / Win32.CRYPTPROTECTMEMORY_BLOCK_SIZE * Win32.CRYPTPROTECTMEMORY_BLOCK_SIZE);
        _region = SecureRegion.Allocate((int)_sealSize);
        SecureMemory.TrackProtected(+1);
    }

    public int Length => _length;

    public bool IsDisposed
    {
        get
        {
            lock (_gate)
            {
                return _disposeRequested;
            }
        }
    }

    /// <summary>True when the bytes are currently encrypted by the kernel.</summary>
    public bool IsSealed
    {
        get
        {
            lock (_gate)
            {
                return _sealed;
            }
        }
    }

    public static ProtectedBuffer Create(ReadOnlySpan<byte> data)
    {
        var buffer = new ProtectedBuffer(data.Length);
        data.CopyTo(new Span<byte>(buffer._region.Pointer, data.Length));
        buffer.SealCore();
        return buffer;
    }

    public static ProtectedBuffer CreateZeroed(int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        var buffer = new ProtectedBuffer(length);
        buffer.SealCore();
        return buffer;
    }

    public static ProtectedBuffer CreateRandom(int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        var buffer = new ProtectedBuffer(length);
        RandomNumberGenerator.Fill(new Span<byte>(buffer._region.Pointer, length));
        buffer.SealCore();
        return buffer;
    }

    /// <summary>Creates a buffer and lets <paramref name="fill"/> write its content before it is sealed.</summary>
    public static ProtectedBuffer Create(int length, Action<Span<byte>> fill)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        var buffer = new ProtectedBuffer(length);
        try
        {
            fill(new Span<byte>(buffer._region.Pointer, length));
        }
        catch
        {
            buffer.Dispose();
            throw;
        }

        buffer.SealCore();
        return buffer;
    }

    /// <summary>Decrypts the bytes in place until the returned lease is disposed.</summary>
    public Lease Unprotect()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposeRequested, this);
            if (_leases == 0)
            {
                OpenCore();
            }

            _leases++;
        }

        return new Lease(this);
    }

    public ProtectedBuffer Clone()
    {
        using var lease = Unprotect();
        return Create(lease.Span);
    }

    public bool FixedTimeEquals(ProtectedBuffer other)
    {
        using var a = Unprotect();
        using var b = other.Unprotect();
        return CryptographicOperations.FixedTimeEquals(a.Span, b.Span);
    }

    public bool FixedTimeEquals(ReadOnlySpan<byte> other)
    {
        using var a = Unprotect();
        return CryptographicOperations.FixedTimeEquals(a.Span, other);
    }

    private void Release()
    {
        var free = false;
        lock (_gate)
        {
            _leases--;
            if (_leases == 0)
            {
                if (_disposeRequested)
                {
                    free = !_released;
                    _released = true;
                }
                else
                {
                    SealCore();
                }
            }
        }

        if (free)
        {
            FreeCore();
        }
    }

    private void OpenCore()
    {
        if (_region.Guarded)
        {
            if (!Win32.VirtualProtect(_region.Pointer, _region.Size, Win32.PAGE_READWRITE, out _))
            {
                throw new CryptographicException($"VirtualProtect failed ({Marshal.GetLastPInvokeError()}).");
            }

            _region.Guarded = false;
        }

        if (_sealed)
        {
            if (!Win32.CryptUnprotectMemory(_region.Pointer, _sealSize, Win32.CRYPTPROTECTMEMORY_SAME_PROCESS))
            {
                throw new CryptographicException($"CryptUnprotectMemory failed ({Marshal.GetLastPInvokeError()}).");
            }

            _sealed = false;
        }
    }

    private void SealCore()
    {
        if (!_sealed)
        {
            if (Win32.CryptProtectMemory(_region.Pointer, _sealSize, Win32.CRYPTPROTECTMEMORY_SAME_PROCESS))
            {
                _sealed = true;
            }
            else
            {
                SecureMemory.HardwareProtectionAvailable = false;
            }
        }

        if (!_region.Guarded && Win32.VirtualProtect(_region.Pointer, _region.Size, Win32.PAGE_NOACCESS, out _))
        {
            _region.Guarded = true;
        }
    }

    private void FreeCore()
    {
        _region.Dispose();
        SecureMemory.TrackProtected(-1);
    }

    public void Dispose()
    {
        var free = false;
        lock (_gate)
        {
            if (_disposeRequested)
            {
                return;
            }

            _disposeRequested = true;
            if (_leases == 0)
            {
                free = true;
                _released = true;
            }
        }

        if (free)
        {
            FreeCore();
        }
    }

    public override string ToString() => $"ProtectedBuffer({_length} bytes)";

    /// <summary>Scoped plaintext access. Never store the span beyond the lease.</summary>
    public ref struct Lease
    {
        private ProtectedBuffer? _owner;

        internal Lease(ProtectedBuffer owner)
        {
            _owner = owner;
            Span = new Span<byte>(owner._region.Pointer, owner._length);
        }

        public Span<byte> Span { get; }

        public void Dispose()
        {
            var owner = _owner;
            _owner = null;
            owner?.Release();
        }
    }
}
