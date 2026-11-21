using System.Security.Cryptography;
using System.Text;

namespace OnlyYours.Core.Security.Memory;

/// <summary>
/// A growable UTF-16 secret held in locked native memory — the replacement for
/// <see cref="string"/> wherever a password is handled. Strings are immutable and
/// copied freely by the runtime, so they can never be wiped; this buffer is wiped
/// on every reallocation and on <see cref="Dispose"/>.
/// </summary>
public sealed unsafe class SecureCharBuffer : IDisposable
{
    private SecureBuffer _store;
    private int _length;

    public SecureCharBuffer(int capacity = 32)
    {
        _store = SecureBuffer.Allocate(Math.Max(capacity, 8) * sizeof(char));
    }

    public int Length => _length;

    public bool IsEmpty => _length == 0;

    public int Capacity => _store.Capacity / sizeof(char);

    public bool IsDisposed => _store.IsDisposed;

    public ReadOnlySpan<char> Span => new((char*)_store.Pointer, _length);

    public Span<char> WritableSpan => new((char*)_store.Pointer, _length);

    public static SecureCharBuffer CopyOf(ReadOnlySpan<char> chars)
    {
        var buffer = new SecureCharBuffer(chars.Length);
        buffer.Append(chars);
        return buffer;
    }

    public static SecureCharBuffer FromUtf8(ReadOnlySpan<byte> utf8)
    {
        var count = Encoding.UTF8.GetCharCount(utf8);
        var buffer = new SecureCharBuffer(count);
        Encoding.UTF8.GetChars(utf8, new Span<char>((char*)buffer._store.Pointer, count));
        buffer._length = count;
        return buffer;
    }

    public void Append(char value)
    {
        EnsureCapacity(_length + 1);
        ((char*)_store.Pointer)[_length++] = value;
    }

    public void Append(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty)
        {
            return;
        }

        EnsureCapacity(_length + value.Length);
        value.CopyTo(new Span<char>((char*)_store.Pointer + _length, value.Length));
        _length += value.Length;
    }

    public void Insert(int index, ReadOnlySpan<char> value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, _length);
        if (value.IsEmpty)
        {
            return;
        }

        EnsureCapacity(_length + value.Length);
        var all = new Span<char>((char*)_store.Pointer, _length + value.Length);
        all[index.._length].CopyTo(all[(index + value.Length)..]);
        value.CopyTo(all[index..]);
        _length += value.Length;
    }

    public void Remove(int index, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index + count, _length);
        if (count == 0)
        {
            return;
        }

        var all = new Span<char>((char*)_store.Pointer, _length);
        all[(index + count)..].CopyTo(all[index..]);
        all[(_length - count).._length].Clear();
        _length -= count;
    }

    public void Truncate(int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        if (length >= _length)
        {
            return;
        }

        new Span<char>((char*)_store.Pointer + length, _length - length).Clear();
        _length = length;
    }

    public void Clear()
    {
        new Span<char>((char*)_store.Pointer, _length).Clear();
        _length = 0;
    }

    /// <summary>UTF-8 copy in a fresh secure buffer (what the KDFs and the field cipher consume).</summary>
    public SecureBuffer ToUtf8()
    {
        var count = Encoding.UTF8.GetByteCount(Span);
        var buffer = SecureBuffer.Allocate(count);
        Encoding.UTF8.GetBytes(Span, buffer.Span);
        return buffer;
    }

    /// <summary>Constant-time comparison (length is not hidden).</summary>
    public bool ContentEquals(ReadOnlySpan<char> other)
    {
        if (other.Length != _length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            System.Runtime.InteropServices.MemoryMarshal.AsBytes(Span),
            System.Runtime.InteropServices.MemoryMarshal.AsBytes(other));
    }

    private void EnsureCapacity(int chars)
    {
        if (chars <= Capacity)
        {
            return;
        }

        var next = SecureBuffer.Allocate(Math.Max(chars, Capacity * 2) * sizeof(char));
        new Span<byte>(_store.Pointer, _length * sizeof(char)).CopyTo(next.Span);
        _store.Dispose();
        _store = next;
    }

    public void Dispose() => _store.Dispose();

    /// <summary>Never prints the secret.</summary>
    public override string ToString() => $"SecureCharBuffer({_length} chars)";
}
