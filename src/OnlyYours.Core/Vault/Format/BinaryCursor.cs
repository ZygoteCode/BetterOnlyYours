using System.Buffers;
using System.Buffers.Binary;

namespace OnlyYours.Core.Vault.Format;

/// <summary>Little-endian writer for non-secret binary structures (headers, slots, packages).</summary>
internal sealed class BinaryOut
{
    private readonly ArrayBufferWriter<byte> _writer = new(512);

    public int Length => _writer.WrittenCount;

    public ReadOnlySpan<byte> Written => _writer.WrittenSpan;

    public void Byte(byte value)
    {
        _writer.GetSpan(1)[0] = value;
        _writer.Advance(1);
    }

    public void U16(ushort value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(_writer.GetSpan(2), value);
        _writer.Advance(2);
    }

    public void U32(uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(_writer.GetSpan(4), value);
        _writer.Advance(4);
    }

    public void U64(ulong value)
    {
        BinaryPrimitives.WriteUInt64LittleEndian(_writer.GetSpan(8), value);
        _writer.Advance(8);
    }

    public void I64(long value)
    {
        BinaryPrimitives.WriteInt64LittleEndian(_writer.GetSpan(8), value);
        _writer.Advance(8);
    }

    public void Raw(ReadOnlySpan<byte> data) => _writer.Write(data);

    public void Guid(Guid value)
    {
        value.TryWriteBytes(_writer.GetSpan(16));
        _writer.Advance(16);
    }

    /// <summary>u8 length prefix + bytes (≤ 255).</summary>
    public void Short(ReadOnlySpan<byte> data)
    {
        if (data.Length > byte.MaxValue)
        {
            throw new ArgumentException("Field too long for an 8-bit length.");
        }

        Byte((byte)data.Length);
        Raw(data);
    }

    /// <summary>u32 length prefix + bytes.</summary>
    public void Long(ReadOnlySpan<byte> data)
    {
        U32((uint)data.Length);
        Raw(data);
    }

    public void PatchU32(int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(System.Runtime.InteropServices.MemoryMarshal.AsMemory(_writer.WrittenMemory).Span.Slice(offset, 4), value);

    public byte[] ToArray() => _writer.WrittenSpan.ToArray();
}

/// <summary>Bounds-checked little-endian reader; any overrun is reported as a truncated vault.</summary>
internal ref struct BinaryIn
{
    private readonly ReadOnlySpan<byte> _data;
    private int _position;

    public BinaryIn(ReadOnlySpan<byte> data)
    {
        _data = data;
        _position = 0;
    }

    public readonly int Position => _position;

    public readonly int Remaining => _data.Length - _position;

    public byte U8()
    {
        Need(1);
        return _data[_position++];
    }

    public ushort U16()
    {
        Need(2);
        var value = BinaryPrimitives.ReadUInt16LittleEndian(_data.Slice(_position, 2));
        _position += 2;
        return value;
    }

    public uint U32()
    {
        Need(4);
        var value = BinaryPrimitives.ReadUInt32LittleEndian(_data.Slice(_position, 4));
        _position += 4;
        return value;
    }

    public ulong U64()
    {
        Need(8);
        var value = BinaryPrimitives.ReadUInt64LittleEndian(_data.Slice(_position, 8));
        _position += 8;
        return value;
    }

    public long I64()
    {
        Need(8);
        var value = BinaryPrimitives.ReadInt64LittleEndian(_data.Slice(_position, 8));
        _position += 8;
        return value;
    }

    public ReadOnlySpan<byte> Raw(int count)
    {
        Need(count);
        var span = _data.Slice(_position, count);
        _position += count;
        return span;
    }

    public Guid Guid() => new(Raw(16));

    public ReadOnlySpan<byte> Short() => Raw(U8());

    public ReadOnlySpan<byte> Long(int max)
    {
        var length = U32();
        if (length > (uint)max)
        {
            throw new VaultException(VaultError.Corrupted, "A length field exceeds its allowed maximum.");
        }

        return Raw((int)length);
    }

    private readonly void Need(int count)
    {
        if (count < 0 || _data.Length - _position < count)
        {
            throw new VaultException(VaultError.Truncated, "The file ends before the structure is complete.");
        }
    }
}
