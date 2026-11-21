using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace OnlyYours.Core.Security.Memory;

/// <summary>
/// A page-aligned block of native memory that never lives on the managed heap.
/// <list type="bullet">
/// <item>it is allocated with <c>VirtualAlloc</c>, so the GC can neither move nor copy it;</item>
/// <item>it is pinned in RAM with <c>VirtualLock</c>, so it is never written to the page file;</item>
/// <item>it is zeroed with <see cref="CryptographicOperations.ZeroMemory"/> before being returned to the OS.</item>
/// </list>
/// The region is a <see cref="SafeHandle"/>: even a leaked buffer is wiped by the
/// finalizer, and <see cref="SecureMemory.WipeAll"/> can scrub every live region at once
/// (used on fatal errors and at process exit).
/// </summary>
internal sealed unsafe class SecureRegion : SafeHandle
{
    private SecureRegion(nint pointer, nuint size, bool locked)
        : base(nint.Zero, ownsHandle: true)
    {
        SetHandle(pointer);
        Size = size;
        Locked = locked;
    }

    public nuint Size { get; }

    public bool Locked { get; }

    /// <summary>True while the pages are mapped <c>PAGE_NOACCESS</c>.</summary>
    internal bool Guarded { get; set; }

    public override bool IsInvalid => handle == nint.Zero;

    public byte* Pointer => (byte*)handle;

    public static SecureRegion Allocate(int bytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bytes);
        var page = (nuint)SecureMemory.PageSize;
        var size = ((nuint)bytes + page - 1) / page * page;

        var pointer = Win32.VirtualAlloc(null, size, Win32.MEM_COMMIT | Win32.MEM_RESERVE, Win32.PAGE_READWRITE);
        if (pointer is null)
        {
            throw new OutOfMemoryException($"VirtualAlloc failed for {size} bytes (error {Marshal.GetLastPInvokeError()}).");
        }

        var locked = SecureMemory.TryLock(pointer, size);
        var region = new SecureRegion((nint)pointer, size, locked);
        SecureMemory.Register(region);
        return region;
    }

    /// <summary>Overwrites the whole region with zeros, whatever its current protection.</summary>
    internal void Scrub()
    {
        if (IsInvalid || IsClosed)
        {
            return;
        }

        if (Guarded)
        {
            Win32.VirtualProtect(Pointer, Size, Win32.PAGE_READWRITE, out _);
            Guarded = false;
        }

        CryptographicOperations.ZeroMemory(new Span<byte>(Pointer, checked((int)Size)));
    }

    protected override bool ReleaseHandle()
    {
        var pointer = (void*)handle;
        if (Guarded)
        {
            Win32.VirtualProtect(pointer, Size, Win32.PAGE_READWRITE, out _);
            Guarded = false;
        }

        CryptographicOperations.ZeroMemory(new Span<byte>(pointer, checked((int)Size)));
        if (Locked)
        {
            Win32.VirtualUnlock(pointer, Size);
        }

        Win32.VirtualFree(pointer, 0, Win32.MEM_RELEASE);
        SecureMemory.Unregister(this, Size, Locked);
        return true;
    }
}
