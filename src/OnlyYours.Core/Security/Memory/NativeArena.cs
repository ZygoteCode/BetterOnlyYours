using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace OnlyYours.Core.Security.Memory;

/// <summary>
/// Large scratch memory for the memory-hard KDF (hundreds of MiB). Too big to lock,
/// but still allocated outside the GC heap so it is never copied, and zeroed before
/// release — off the critical path, so unlocking does not wait for the wipe.
/// </summary>
internal sealed unsafe class NativeArena : IDisposable
{
    private byte* _pointer;

    public NativeArena(nuint size)
    {
        Size = size;
        _pointer = (byte*)Win32.VirtualAlloc(null, size, Win32.MEM_COMMIT | Win32.MEM_RESERVE, Win32.PAGE_READWRITE);
        if (_pointer is null)
        {
            throw new OutOfMemoryException(
                $"Unable to reserve {size / (1024 * 1024)} MiB for key derivation (error {Marshal.GetLastPInvokeError()}).");
        }
    }

    public nuint Size { get; }

    public byte* Pointer => _pointer;

    /// <summary>Zeroes and frees the arena on a background thread.</summary>
    public void DisposeInBackground()
    {
        var pointer = (nint)_pointer;
        _pointer = null;
        if (pointer == 0)
        {
            return;
        }

        var size = Size;
        ThreadPool.UnsafeQueueUserWorkItem(_ => Release((byte*)pointer, size), null);
    }

    public void Dispose()
    {
        var pointer = _pointer;
        _pointer = null;
        if (pointer is not null)
        {
            Release(pointer, Size);
        }
    }

    private static void Release(byte* pointer, nuint size)
    {
        const int chunk = 1 << 30;
        nuint offset = 0;
        while (offset < size)
        {
            var length = (int)Math.Min((nuint)chunk, size - offset);
            CryptographicOperations.ZeroMemory(new Span<byte>(pointer + offset, length));
            offset += (nuint)length;
        }

        Win32.VirtualFree(pointer, 0, Win32.MEM_RELEASE);
    }
}
