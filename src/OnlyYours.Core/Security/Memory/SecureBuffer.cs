namespace OnlyYours.Core.Security.Memory;

/// <summary>
/// Plaintext bytes that must not leak: held in locked native memory, zeroed on
/// <see cref="Dispose"/>. Use it for short-lived plaintext (a decrypted field,
/// a serialized document, a password encoded as UTF-8) and dispose as soon as
/// the work is done.
/// </summary>
public sealed unsafe class SecureBuffer : IDisposable
{
    private readonly SecureRegion _region;
    private int _length;

    private SecureBuffer(int length)
    {
        _region = SecureRegion.Allocate(Math.Max(length, 1));
        _length = length;
    }

    public int Length => _length;

    public int Capacity => (int)_region.Size;

    public bool IsLocked => _region.Locked;

    public bool IsDisposed => _region.IsClosed;

    public Span<byte> Span
    {
        get
        {
            ThrowIfDisposed();
            return new Span<byte>(_region.Pointer, _length);
        }
    }

    internal byte* Pointer
    {
        get
        {
            ThrowIfDisposed();
            return _region.Pointer;
        }
    }

    public static SecureBuffer Allocate(int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        return new SecureBuffer(length);
    }

    public static SecureBuffer CopyOf(ReadOnlySpan<byte> data)
    {
        var buffer = new SecureBuffer(data.Length);
        data.CopyTo(buffer.Span);
        return buffer;
    }

    /// <summary>Shrinks (or grows within capacity) the logical length; bytes past the new end are zeroed.</summary>
    public void SetLength(int length)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(length, Capacity);
        if (length < _length)
        {
            new Span<byte>(_region.Pointer + length, _length - length).Clear();
        }

        _length = length;
    }

    public void Dispose() => _region.Dispose();

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_region.IsClosed, this);

    public override string ToString() => $"SecureBuffer({_length} bytes, locked={IsLocked})";
}
