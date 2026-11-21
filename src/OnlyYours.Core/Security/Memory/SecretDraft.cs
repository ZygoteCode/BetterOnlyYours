using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace OnlyYours.Core.Security.Memory;

/// <summary>
/// The editable secret behind every password field of the interface.
/// <para>
/// Characters typed by the user go straight into a <see cref="ProtectedBuffer"/>:
/// encrypted by the kernel and unreadable (PAGE_NOACCESS) between keystrokes,
/// decrypted for the microseconds an edit takes. The full secret never exists as
/// a <see cref="string"/>.
/// </para>
/// Drafts are owned by the UI thread; they are not meant to be edited concurrently.
/// </summary>
public sealed class SecretDraft : IDisposable
{
    private ProtectedBuffer _store;
    private int _length;
    private int _capacity;
    private bool _disposed;

    public SecretDraft(int capacity = 32)
    {
        _capacity = Math.Max(16, capacity);
        _store = ProtectedBuffer.CreateZeroed(_capacity * sizeof(char));
    }

    public event EventHandler? Changed;

    public int Length => _length;

    public bool IsEmpty => _length == 0;

    /// <summary>Monotonic counter, bumped on every change (useful for cheap change detection).</summary>
    public int Version { get; private set; }

    public bool IsDisposed => _disposed;

    public static SecretDraft From(ReadOnlySpan<char> text)
    {
        var draft = new SecretDraft(text.Length);
        draft.Set(text);
        return draft;
    }

    public void Insert(int index, ReadOnlySpan<char> text)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, _length);
        if (text.IsEmpty)
        {
            return;
        }

        EnsureCapacity(_length + text.Length);
        using (var lease = _store.Unprotect())
        {
            var chars = MemoryMarshal.Cast<byte, char>(lease.Span);
            chars[index.._length].CopyTo(chars[(index + text.Length)..]);
            text.CopyTo(chars[index..]);
        }

        _length += text.Length;
        OnChanged();
    }

    public void Append(ReadOnlySpan<char> text) => Insert(_length, text);

    public void Remove(int index, int count)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index + count, _length);
        if (count == 0)
        {
            return;
        }

        using (var lease = _store.Unprotect())
        {
            var chars = MemoryMarshal.Cast<byte, char>(lease.Span);
            chars[(index + count).._length].CopyTo(chars[index..]);
            chars[(_length - count).._length].Clear();
        }

        _length -= count;
        OnChanged();
    }

    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_length == 0)
        {
            return;
        }

        using (var lease = _store.Unprotect())
        {
            lease.Span.Clear();
        }

        _length = 0;
        OnChanged();
    }

    public void Set(ReadOnlySpan<char> text)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureCapacity(text.Length);
        using (var lease = _store.Unprotect())
        {
            var chars = MemoryMarshal.Cast<byte, char>(lease.Span);
            chars.Clear();
            text.CopyTo(chars);
        }

        _length = text.Length;
        OnChanged();
    }

    public void SetUtf8(ReadOnlySpan<byte> utf8)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var count = Encoding.UTF8.GetCharCount(utf8);
        EnsureCapacity(count);
        using (var lease = _store.Unprotect())
        {
            var chars = MemoryMarshal.Cast<byte, char>(lease.Span);
            chars.Clear();
            Encoding.UTF8.GetChars(utf8, chars);
        }

        _length = count;
        OnChanged();
    }

    public void CopyFrom(SecretDraft other)
    {
        using var lease = other.Read();
        Set(lease.Chars);
    }

    /// <summary>Scoped read access to the characters.</summary>
    public CharLease Read()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new CharLease(_store.Unprotect(), _length);
    }

    public SecureBuffer ToUtf8()
    {
        using var lease = Read();
        var count = Encoding.UTF8.GetByteCount(lease.Chars);
        var buffer = SecureBuffer.Allocate(count);
        Encoding.UTF8.GetBytes(lease.Chars, buffer.Span);
        return buffer;
    }

    public SecureCharBuffer ToCharBuffer()
    {
        using var lease = Read();
        return SecureCharBuffer.CopyOf(lease.Chars);
    }

    /// <summary>Constant-time equality of two drafts (the length is not hidden).</summary>
    public bool ContentEquals(SecretDraft other)
    {
        if (other._length != _length)
        {
            return false;
        }

        using var a = Read();
        using var b = other.Read();
        return CryptographicOperations.FixedTimeEquals(MemoryMarshal.AsBytes(a.Chars), MemoryMarshal.AsBytes(b.Chars));
    }

    private void EnsureCapacity(int chars)
    {
        if (chars <= _capacity)
        {
            return;
        }

        var capacity = Math.Max(chars, _capacity * 2);
        var next = ProtectedBuffer.CreateZeroed(capacity * sizeof(char));
        using (var source = _store.Unprotect())
        using (var target = next.Unprotect())
        {
            source.Span[..(_length * sizeof(char))].CopyTo(target.Span);
        }

        _store.Dispose();
        _store = next;
        _capacity = capacity;
    }

    private void OnChanged()
    {
        Version++;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _length = 0;
        _store.Dispose();
    }

    public override string ToString() => $"SecretDraft({_length} chars)";

    public ref struct CharLease
    {
        private ProtectedBuffer.Lease _lease;

        internal CharLease(ProtectedBuffer.Lease lease, int length)
        {
            _lease = lease;
            Chars = MemoryMarshal.Cast<byte, char>(lease.Span)[..length];
        }

        public ReadOnlySpan<char> Chars { get; }

        public void Dispose() => _lease.Dispose();
    }
}
