using System.Buffers.Binary;
using System.Text;
using OnlyYours.Core.Security.Memory;

namespace OnlyYours.Core.Vault.Format;

public enum WireType : byte
{
    Varint = 0,
    Fixed64 = 1,
    Bytes = 2,
}

/// <summary>
/// Compact tag-length-value writer (protobuf-compatible wire format) used for the
/// encrypted document. It writes into locked memory that is wiped on growth and on
/// dispose, so the serialized document never touches the managed heap.
/// </summary>
public sealed class TlvWriter : IDisposable
{
    private SecureBuffer _buffer;
    private int _position;

    public TlvWriter(int initialCapacity = 8192)
    {
        _buffer = SecureBuffer.Allocate(Math.Max(initialCapacity, 256));
    }

    public int Position => _position;

    public ReadOnlySpan<byte> Written => _buffer.Span[.._position];

    public void WriteVarint(int field, ulong value)
    {
        WriteKey(field, WireType.Varint);
        WriteRawVarint(value);
    }

    public void WriteSigned(int field, long value) => WriteVarint(field, (ulong)((value << 1) ^ (value >> 63)));

    public void WriteBool(int field, bool value)
    {
        if (value)
        {
            WriteVarint(field, 1);
        }
    }

    public void WriteFixed64(int field, long value)
    {
        WriteKey(field, WireType.Fixed64);
        BinaryPrimitives.WriteInt64LittleEndian(Reserve(8), value);
    }

    public void WriteTimestamp(int field, DateTimeOffset value) => WriteFixed64(field, value.ToUnixTimeMilliseconds());

    public void WriteTimestamp(int field, DateTimeOffset? value)
    {
        if (value is { } stamp)
        {
            WriteTimestamp(field, stamp);
        }
    }

    public void WriteBytes(int field, ReadOnlySpan<byte> value)
    {
        WriteKey(field, WireType.Bytes);
        WriteRawVarint((ulong)value.Length);
        value.CopyTo(Reserve(value.Length));
    }

    public void WriteString(int field, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        var count = Encoding.UTF8.GetByteCount(value);
        WriteKey(field, WireType.Bytes);
        WriteRawVarint((ulong)count);
        Encoding.UTF8.GetBytes(value, Reserve(count));
    }

    /// <summary>Writes UTF-16 chars as UTF-8 straight into locked memory (no intermediate string).</summary>
    public void WriteChars(int field, ReadOnlySpan<char> value)
    {
        var count = Encoding.UTF8.GetByteCount(value);
        WriteKey(field, WireType.Bytes);
        WriteRawVarint((ulong)count);
        Encoding.UTF8.GetBytes(value, Reserve(count));
    }

    public void WriteGuid(int field, Guid value)
    {
        WriteKey(field, WireType.Bytes);
        WriteRawVarint(16);
        value.TryWriteBytes(Reserve(16));
    }

    /// <summary>Opens a nested message; the length is back-patched by <see cref="EndMessage"/>.</summary>
    public int BeginMessage(int field)
    {
        WriteKey(field, WireType.Bytes);
        var at = _position;
        Reserve(5);
        return at;
    }

    public void EndMessage(int at)
    {
        var length = (uint)(_position - at - 5);
        var span = _buffer.Span.Slice(at, 5);
        for (var i = 0; i < 4; i++)
        {
            span[i] = (byte)((length & 0x7F) | 0x80);
            length >>= 7;
        }

        span[4] = (byte)length;
    }

    public void WriteRaw(ReadOnlySpan<byte> raw) => raw.CopyTo(Reserve(raw.Length));

    public SecureBuffer ToSecureBuffer() => SecureBuffer.CopyOf(Written);

    public byte[] ToArray() => Written.ToArray();

    private void WriteKey(int field, WireType type) => WriteRawVarint(((ulong)field << 3) | (byte)type);

    private void WriteRawVarint(ulong value)
    {
        Span<byte> scratch = stackalloc byte[10];
        var count = 0;
        while (value >= 0x80)
        {
            scratch[count++] = (byte)(value | 0x80);
            value >>= 7;
        }

        scratch[count++] = (byte)value;
        scratch[..count].CopyTo(Reserve(count));
    }

    private Span<byte> Reserve(int count)
    {
        var needed = _position + count;
        if (needed > _buffer.Length)
        {
            var next = SecureBuffer.Allocate(Math.Max(needed, _buffer.Length * 2));
            _buffer.Span[.._position].CopyTo(next.Span);
            _buffer.Dispose();
            _buffer = next;
        }

        var span = _buffer.Span.Slice(_position, count);
        _position = needed;
        return span;
    }

    public void Dispose() => _buffer.Dispose();
}

/// <summary>Reader for <see cref="TlvWriter"/> output. Every length is bounds-checked.</summary>
public ref struct TlvReader
{
    private readonly ReadOnlySpan<byte> _data;
    private int _position;
    private int _fieldStart;

    public TlvReader(ReadOnlySpan<byte> data)
    {
        _data = data;
        _position = 0;
        _fieldStart = 0;
    }

    public readonly bool End => _position >= _data.Length;

    public bool TryReadField(out int field, out WireType type)
    {
        if (End)
        {
            field = 0;
            type = 0;
            return false;
        }

        _fieldStart = _position;
        var key = ReadRawVarint();
        field = (int)(key >> 3);
        type = (WireType)(key & 7);
        if (field <= 0 || type > WireType.Bytes)
        {
            throw Malformed("invalid field key");
        }

        return true;
    }

    public ulong ReadVarint() => ReadRawVarint();

    public long ReadSigned()
    {
        var raw = ReadRawVarint();
        return (long)(raw >> 1) ^ -(long)(raw & 1);
    }

    public int ReadInt32()
    {
        var value = ReadRawVarint();
        if (value > int.MaxValue)
        {
            throw Malformed("integer out of range");
        }

        return (int)value;
    }

    public bool ReadBool() => ReadRawVarint() != 0;

    public long ReadFixed64()
    {
        if (_data.Length - _position < 8)
        {
            throw Malformed("truncated fixed64");
        }

        var value = BinaryPrimitives.ReadInt64LittleEndian(_data.Slice(_position, 8));
        _position += 8;
        return value;
    }

    public DateTimeOffset ReadTimestamp()
    {
        var millis = Math.Clamp(ReadFixed64(), -62_135_596_800_000L, 253_402_300_799_999L);
        return DateTimeOffset.FromUnixTimeMilliseconds(millis);
    }

    public ReadOnlySpan<byte> ReadBytes()
    {
        var length = ReadRawVarint();
        if (length > (ulong)(_data.Length - _position))
        {
            throw Malformed("length exceeds payload");
        }

        var span = _data.Slice(_position, (int)length);
        _position += (int)length;
        return span;
    }

    public string ReadString() => Encoding.UTF8.GetString(ReadBytes());

    public Guid ReadGuid()
    {
        var bytes = ReadBytes();
        if (bytes.Length != 16)
        {
            throw Malformed("identifier is not 16 bytes");
        }

        return new Guid(bytes);
    }

    public void Skip(WireType type)
    {
        switch (type)
        {
            case WireType.Varint:
                ReadRawVarint();
                break;
            case WireType.Fixed64:
                ReadFixed64();
                break;
            case WireType.Bytes:
                ReadBytes();
                break;
            default:
                throw Malformed("unknown wire type");
        }
    }

    /// <summary>Skips the current field and returns its raw encoding, so unknown data written by a newer version survives a round-trip.</summary>
    public byte[] Capture(WireType type)
    {
        Skip(type);
        return _data[_fieldStart.._position].ToArray();
    }

    private ulong ReadRawVarint()
    {
        ulong result = 0;
        for (var shift = 0; shift < 64; shift += 7)
        {
            if (_position >= _data.Length)
            {
                throw Malformed("truncated varint");
            }

            var b = _data[_position++];
            result |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0)
            {
                return result;
            }
        }

        throw Malformed("varint too long");
    }

    private static VaultException Malformed(string detail) =>
        new(VaultError.Corrupted, $"The vault document is malformed ({detail}).");
}
