using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace OnlyYours.Core.Security.Crypto;

[InlineArray(8)]
internal struct UInt64x8
{
    private ulong _element0;
}

[InlineArray(128)]
internal struct Byte128
{
    private byte _element0;
}

/// <summary>
/// BLAKE2b (RFC 7693), unkeyed, 1..64 byte digests. It is the hash inside Argon2;
/// the implementation is incremental, allocation-free and wipes its state on
/// <see cref="Final"/>.
/// </summary>
public struct Blake2b
{
    public const int BlockBytes = 128;
    public const int MaxOutputBytes = 64;

    private static ReadOnlySpan<ulong> IV =>
    [
        0x6A09E667F3BCC908UL, 0xBB67AE8584CAA73BUL, 0x3C6EF372FE94F82BUL, 0xA54FF53A5F1D36F1UL,
        0x510E527FADE682D1UL, 0x9B05688C2B3E6C1FUL, 0x1F83D9ABFB41BD6BUL, 0x5BE0CD19137E2179UL,
    ];

    private static ReadOnlySpan<byte> Sigma =>
    [
        0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15,
        14, 10, 4, 8, 9, 15, 13, 6, 1, 12, 0, 2, 11, 7, 5, 3,
        11, 8, 12, 0, 5, 2, 15, 13, 10, 14, 3, 6, 7, 1, 9, 4,
        7, 9, 3, 1, 13, 12, 11, 14, 2, 6, 5, 10, 4, 0, 15, 8,
        9, 0, 5, 7, 2, 4, 10, 15, 14, 1, 11, 12, 6, 8, 3, 13,
        2, 12, 6, 10, 0, 11, 8, 3, 4, 13, 7, 5, 15, 14, 1, 9,
        12, 5, 1, 15, 14, 13, 4, 10, 0, 7, 6, 3, 9, 2, 8, 11,
        13, 11, 7, 14, 12, 1, 3, 9, 5, 0, 15, 4, 8, 6, 2, 10,
        6, 15, 14, 9, 11, 3, 0, 8, 12, 2, 13, 7, 1, 4, 10, 5,
        10, 2, 8, 4, 7, 6, 1, 5, 15, 11, 9, 14, 3, 12, 13, 0,
    ];

    private UInt64x8 _h;
    private Byte128 _buffer;
    private int _bufferLength;
    private ulong _t0;
    private ulong _t1;
    private readonly int _outputLength;

    public Blake2b(int outputLength)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(outputLength, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(outputLength, MaxOutputBytes);
        _outputLength = outputLength;
        Span<ulong> h = _h;
        IV.CopyTo(h);
        h[0] ^= 0x01010000UL ^ (ulong)outputLength;
    }

    public readonly int OutputLength => _outputLength;

    public static void Hash(ReadOnlySpan<byte> data, Span<byte> output)
    {
        var state = new Blake2b(output.Length);
        state.Update(data);
        state.Final(output);
    }

    public void Update(ReadOnlySpan<byte> data)
    {
        Span<ulong> h = _h;
        Span<byte> buffer = _buffer;
        while (!data.IsEmpty)
        {
            if (_bufferLength == BlockBytes)
            {
                Increment(BlockBytes);
                Compress(h, buffer, _t0, _t1, last: false);
                _bufferLength = 0;
            }

            var take = Math.Min(BlockBytes - _bufferLength, data.Length);
            data[..take].CopyTo(buffer[_bufferLength..]);
            _bufferLength += take;
            data = data[take..];
        }
    }

    public void Final(Span<byte> output)
    {
        if (output.Length < _outputLength)
        {
            throw new ArgumentException("Output buffer is too small.", nameof(output));
        }

        Span<ulong> h = _h;
        Span<byte> buffer = _buffer;
        Increment((ulong)_bufferLength);
        buffer[_bufferLength..].Clear();
        Compress(h, buffer, _t0, _t1, last: true);

        Span<byte> digest = stackalloc byte[MaxOutputBytes];
        for (var i = 0; i < 8; i++)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(digest[(i * 8)..], h[i]);
        }

        digest[.._outputLength].CopyTo(output);
        CryptographicOperations.ZeroMemory(digest);
        CryptographicOperations.ZeroMemory(System.Runtime.InteropServices.MemoryMarshal.AsBytes(h));
        CryptographicOperations.ZeroMemory(buffer);
        _bufferLength = 0;
    }

    private void Increment(ulong count)
    {
        _t0 += count;
        if (_t0 < count)
        {
            _t1++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void G(ref ulong a, ref ulong b, ref ulong c, ref ulong d, ulong x, ulong y)
    {
        a = a + b + x;
        d = BitOperations.RotateRight(d ^ a, 32);
        c += d;
        b = BitOperations.RotateRight(b ^ c, 24);
        a = a + b + y;
        d = BitOperations.RotateRight(d ^ a, 16);
        c += d;
        b = BitOperations.RotateRight(b ^ c, 63);
    }

    private static void Compress(Span<ulong> h, ReadOnlySpan<byte> block, ulong t0, ulong t1, bool last)
    {
        Span<ulong> m = stackalloc ulong[16];
        for (var i = 0; i < 16; i++)
        {
            m[i] = BinaryPrimitives.ReadUInt64LittleEndian(block.Slice(i * 8, 8));
        }

        var iv = IV;
        ulong v0 = h[0], v1 = h[1], v2 = h[2], v3 = h[3], v4 = h[4], v5 = h[5], v6 = h[6], v7 = h[7];
        ulong v8 = iv[0], v9 = iv[1], v10 = iv[2], v11 = iv[3];
        var v12 = iv[4] ^ t0;
        var v13 = iv[5] ^ t1;
        var v14 = last ? ~iv[6] : iv[6];
        var v15 = iv[7];

        var sigma = Sigma;
        for (var r = 0; r < 12; r++)
        {
            var s = sigma.Slice(r % 10 * 16, 16);
            G(ref v0, ref v4, ref v8, ref v12, m[s[0]], m[s[1]]);
            G(ref v1, ref v5, ref v9, ref v13, m[s[2]], m[s[3]]);
            G(ref v2, ref v6, ref v10, ref v14, m[s[4]], m[s[5]]);
            G(ref v3, ref v7, ref v11, ref v15, m[s[6]], m[s[7]]);
            G(ref v0, ref v5, ref v10, ref v15, m[s[8]], m[s[9]]);
            G(ref v1, ref v6, ref v11, ref v12, m[s[10]], m[s[11]]);
            G(ref v2, ref v7, ref v8, ref v13, m[s[12]], m[s[13]]);
            G(ref v3, ref v4, ref v9, ref v14, m[s[14]], m[s[15]]);
        }

        h[0] ^= v0 ^ v8;
        h[1] ^= v1 ^ v9;
        h[2] ^= v2 ^ v10;
        h[3] ^= v3 ^ v11;
        h[4] ^= v4 ^ v12;
        h[5] ^= v5 ^ v13;
        h[6] ^= v6 ^ v14;
        h[7] ^= v7 ^ v15;
        m.Clear();
    }
}
