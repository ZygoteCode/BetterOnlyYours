using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Security.Cryptography;
using OnlyYours.Core.Security.Memory;

namespace OnlyYours.Core.Security.Crypto;

public enum Argon2Type
{
    D = 0,
    I = 1,
    Id = 2,
}

/// <summary>
/// Argon2 (RFC 9106, version 1.3) — written from the specification, validated
/// against the RFC test vectors and against the implementation that wrote the
/// legacy <c>credentials.plf</c> files.
/// <list type="bullet">
/// <item>lanes are filled in parallel, one thread per lane, synchronised at every slice;</item>
/// <item>the BlaMka permutation runs on AVX2 (4 × 64-bit lanes per instruction) with a scalar fallback;</item>
/// <item>the work area lives outside the GC heap and is zeroed off the critical path.</item>
/// </list>
/// </summary>
[SkipLocalsInit]
public static unsafe class Argon2
{
    public const uint Version13 = 0x13;

    private const int BlockWords = 128;
    private const int BlockBytes = 1024;
    private const uint SyncPoints = 4;

    /// <summary>Tests flip this to exercise the portable code path.</summary>
    internal static bool ForceScalar { get; set; }

    public static bool IsAccelerated => Avx2.IsSupported && !ForceScalar;

    public static void DeriveId(
        ReadOnlySpan<byte> password,
        ReadOnlySpan<byte> salt,
        uint memoryKiB,
        uint iterations,
        uint parallelism,
        Span<byte> output,
        ReadOnlySpan<byte> secret = default,
        ReadOnlySpan<byte> associatedData = default,
        CancellationToken cancellationToken = default)
        => Derive(Argon2Type.Id, password, salt, memoryKiB, iterations, parallelism, output, secret, associatedData, cancellationToken);

    public static void Derive(
        Argon2Type type,
        ReadOnlySpan<byte> password,
        ReadOnlySpan<byte> salt,
        uint memoryKiB,
        uint iterations,
        uint parallelism,
        Span<byte> output,
        ReadOnlySpan<byte> secret = default,
        ReadOnlySpan<byte> associatedData = default,
        CancellationToken cancellationToken = default)
    {
        if (output.Length < 4)
        {
            throw new ArgumentException("Argon2 output must be at least 4 bytes.", nameof(output));
        }

        if (salt.Length < 8)
        {
            throw new ArgumentException("Argon2 salt must be at least 8 bytes.", nameof(salt));
        }

        ArgumentOutOfRangeException.ThrowIfZero(iterations);
        ArgumentOutOfRangeException.ThrowIfZero(parallelism);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(parallelism, 0xFFFFFFu);
        if (memoryKiB < 8 * parallelism)
        {
            throw new ArgumentOutOfRangeException(nameof(memoryKiB), "Argon2 needs at least 8 KiB per lane.");
        }

        var memoryBlocks = memoryKiB;
        if (memoryBlocks < 2 * SyncPoints * parallelism)
        {
            memoryBlocks = 2 * SyncPoints * parallelism;
        }

        var segmentLength = memoryBlocks / (parallelism * SyncPoints);
        memoryBlocks = segmentLength * parallelism * SyncPoints;
        var laneLength = segmentLength * SyncPoints;

        Span<byte> h0 = stackalloc byte[72];
        ComputeH0(type, password, salt, secret, associatedData, memoryKiB, iterations, parallelism, (uint)output.Length, h0[..64]);

        var arena = new NativeArena((nuint)memoryBlocks * BlockBytes);
        try
        {
            var memory = (ulong*)arena.Pointer;

            for (uint lane = 0; lane < parallelism; lane++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(h0.Slice(64, 4), 0);
                BinaryPrimitives.WriteUInt32LittleEndian(h0.Slice(68, 4), lane);
                HPrime(h0, new Span<byte>(memory + (nuint)(lane * laneLength) * BlockWords, BlockBytes));
                BinaryPrimitives.WriteUInt32LittleEndian(h0.Slice(64, 4), 1);
                HPrime(h0, new Span<byte>(memory + ((nuint)(lane * laneLength) + 1) * BlockWords, BlockBytes));
            }

            CryptographicOperations.ZeroMemory(h0);

            var context = new Context((nint)memory, memoryBlocks, laneLength, segmentLength, parallelism, iterations, type, IsAccelerated);

            for (uint pass = 0; pass < iterations; pass++)
            {
                for (uint slice = 0; slice < SyncPoints; slice++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (parallelism == 1)
                    {
                        FillSegment(context, pass, 0, slice);
                    }
                    else
                    {
                        var p = pass;
                        var s = slice;
                        Parallel.For(0, (int)parallelism, new ParallelOptions { MaxDegreeOfParallelism = (int)parallelism }, lane => FillSegment(context, p, (uint)lane, s));
                    }
                }
            }

            var final = stackalloc ulong[BlockWords];
            var lastOfLane0 = memory + (nuint)(laneLength - 1) * BlockWords;
            new ReadOnlySpan<ulong>(lastOfLane0, BlockWords).CopyTo(new Span<ulong>(final, BlockWords));
            for (uint lane = 1; lane < parallelism; lane++)
            {
                var last = memory + ((nuint)(lane * laneLength) + laneLength - 1) * BlockWords;
                for (var i = 0; i < BlockWords; i++)
                {
                    final[i] ^= last[i];
                }
            }

            HPrime(new ReadOnlySpan<byte>(final, BlockBytes), output);
            CryptographicOperations.ZeroMemory(new Span<byte>(final, BlockBytes));
        }
        finally
        {
            arena.DisposeInBackground();
        }
    }

    private static void ComputeH0(
        Argon2Type type,
        ReadOnlySpan<byte> password,
        ReadOnlySpan<byte> salt,
        ReadOnlySpan<byte> secret,
        ReadOnlySpan<byte> associatedData,
        uint memoryKiB,
        uint iterations,
        uint parallelism,
        uint tagLength,
        Span<byte> destination)
    {
        var hash = new Blake2b(64);
        Span<byte> word = stackalloc byte[4];

        void Le(ref Blake2b state, Span<byte> scratch, uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(scratch, value);
            state.Update(scratch);
        }

        Le(ref hash, word, parallelism);
        Le(ref hash, word, tagLength);
        Le(ref hash, word, memoryKiB);
        Le(ref hash, word, iterations);
        Le(ref hash, word, Version13);
        Le(ref hash, word, (uint)type);
        Le(ref hash, word, (uint)password.Length);
        hash.Update(password);
        Le(ref hash, word, (uint)salt.Length);
        hash.Update(salt);
        Le(ref hash, word, (uint)secret.Length);
        hash.Update(secret);
        Le(ref hash, word, (uint)associatedData.Length);
        hash.Update(associatedData);
        hash.Final(destination);
    }

    /// <summary>The variable-length hash H' of RFC 9106 §3.3.</summary>
    internal static void HPrime(ReadOnlySpan<byte> input, Span<byte> output)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(length, (uint)output.Length);

        if (output.Length <= Blake2b.MaxOutputBytes)
        {
            var single = new Blake2b(output.Length);
            single.Update(length);
            single.Update(input);
            single.Final(output);
            return;
        }

        Span<byte> v = stackalloc byte[64];
        Span<byte> next = stackalloc byte[64];
        var first = new Blake2b(64);
        first.Update(length);
        first.Update(input);
        first.Final(v);

        v[..32].CopyTo(output);
        var position = 32;
        while (output.Length - position > 64)
        {
            Blake2b.Hash(v, next);
            next.CopyTo(v);
            v[..32].CopyTo(output[position..]);
            position += 32;
        }

        Blake2b.Hash(v, output[position..]);
        CryptographicOperations.ZeroMemory(v);
        CryptographicOperations.ZeroMemory(next);
    }

    private sealed class Context(nint memory, uint memoryBlocks, uint laneLength, uint segmentLength, uint lanes, uint passes, Argon2Type type, bool accelerated)
    {
        public readonly nint Memory = memory;
        public readonly uint MemoryBlocks = memoryBlocks;
        public readonly uint LaneLength = laneLength;
        public readonly uint SegmentLength = segmentLength;
        public readonly uint Lanes = lanes;
        public readonly uint Passes = passes;
        public readonly Argon2Type Type = type;
        public readonly bool Accelerated = accelerated;
    }

    private static void FillSegment(Context context, uint pass, uint lane, uint slice)
    {
        var memory = (ulong*)context.Memory;
        var dataIndependent = context.Type == Argon2Type.I
            || (context.Type == Argon2Type.Id && pass == 0 && slice < SyncPoints / 2);

        var addressBlock = stackalloc ulong[BlockWords];
        var inputBlock = stackalloc ulong[BlockWords];
        var zeroBlock = stackalloc ulong[BlockWords];

        if (dataIndependent)
        {
            new Span<ulong>(zeroBlock, BlockWords).Clear();
            new Span<ulong>(inputBlock, BlockWords).Clear();
            inputBlock[0] = pass;
            inputBlock[1] = lane;
            inputBlock[2] = slice;
            inputBlock[3] = context.MemoryBlocks;
            inputBlock[4] = context.Passes;
            inputBlock[5] = (ulong)context.Type;
        }

        uint startIndex = 0;
        if (pass == 0 && slice == 0)
        {
            startIndex = 2;
            if (dataIndependent)
            {
                NextAddresses(addressBlock, inputBlock, zeroBlock, context.Accelerated);
            }
        }

        var laneLength = context.LaneLength;
        var currentOffset = (lane * laneLength) + (slice * context.SegmentLength) + startIndex;
        var previousOffset = currentOffset % laneLength == 0
            ? currentOffset + laneLength - 1
            : currentOffset - 1;

        for (var i = startIndex; i < context.SegmentLength; i++, currentOffset++, previousOffset++)
        {
            if (currentOffset % laneLength == 1)
            {
                previousOffset = currentOffset - 1;
            }

            ulong pseudoRandom;
            if (dataIndependent)
            {
                if (i % BlockWords == 0)
                {
                    NextAddresses(addressBlock, inputBlock, zeroBlock, context.Accelerated);
                }

                pseudoRandom = addressBlock[i % BlockWords];
            }
            else
            {
                pseudoRandom = memory[(nuint)previousOffset * BlockWords];
            }

            var referenceLane = (pseudoRandom >> 32) % context.Lanes;
            if (pass == 0 && slice == 0)
            {
                referenceLane = lane;
            }

            var referenceIndex = IndexAlpha(context, pass, slice, i, (uint)pseudoRandom, referenceLane == lane);
            var referenceBlock = memory + (((nuint)referenceLane * laneLength) + referenceIndex) * BlockWords;
            var currentBlock = memory + (nuint)currentOffset * BlockWords;
            var previousBlock = memory + (nuint)previousOffset * BlockWords;

            FillBlock(previousBlock, referenceBlock, currentBlock, withXor: pass != 0, context.Accelerated);
        }

        if (dataIndependent)
        {
            new Span<ulong>(addressBlock, BlockWords).Clear();
            new Span<ulong>(inputBlock, BlockWords).Clear();
        }
    }

    private static uint IndexAlpha(Context context, uint pass, uint slice, uint index, uint pseudoRandom, bool sameLane)
    {
        uint referenceAreaSize;
        if (pass == 0)
        {
            if (slice == 0)
            {
                referenceAreaSize = index - 1;
            }
            else if (sameLane)
            {
                referenceAreaSize = (slice * context.SegmentLength) + index - 1;
            }
            else
            {
                referenceAreaSize = (slice * context.SegmentLength) + (index == 0 ? uint.MaxValue : 0u);
            }
        }
        else if (sameLane)
        {
            referenceAreaSize = context.LaneLength - context.SegmentLength + index - 1;
        }
        else
        {
            referenceAreaSize = context.LaneLength - context.SegmentLength + (index == 0 ? uint.MaxValue : 0u);
        }

        ulong relative = pseudoRandom;
        relative = (relative * relative) >> 32;
        relative = referenceAreaSize - 1 - ((referenceAreaSize * relative) >> 32);

        ulong start = 0;
        if (pass != 0)
        {
            start = slice == SyncPoints - 1 ? 0 : (slice + 1) * context.SegmentLength;
        }

        return (uint)((start + relative) % context.LaneLength);
    }

    private static void NextAddresses(ulong* addressBlock, ulong* inputBlock, ulong* zeroBlock, bool accelerated)
    {
        inputBlock[6]++;
        FillBlock(zeroBlock, inputBlock, addressBlock, withXor: false, accelerated);
        FillBlock(zeroBlock, addressBlock, addressBlock, withXor: false, accelerated);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FillBlock(ulong* previous, ulong* reference, ulong* next, bool withXor, bool accelerated)
    {
        if (accelerated)
        {
            FillBlockAvx2(previous, reference, next, withXor);
        }
        else
        {
            FillBlockScalar(previous, reference, next, withXor);
        }
    }

    // ------------------------------------------------------------------ scalar

    private static void FillBlockScalar(ulong* previous, ulong* reference, ulong* next, bool withXor)
    {
        var r = stackalloc ulong[BlockWords];
        var t = stackalloc ulong[BlockWords];
        for (var i = 0; i < BlockWords; i++)
        {
            var x = reference[i] ^ previous[i];
            r[i] = x;
            t[i] = withXor ? x ^ next[i] : x;
        }

        for (var i = 0; i < 8; i++)
        {
            var o = 16 * i;
            Round(
                ref r[o], ref r[o + 1], ref r[o + 2], ref r[o + 3],
                ref r[o + 4], ref r[o + 5], ref r[o + 6], ref r[o + 7],
                ref r[o + 8], ref r[o + 9], ref r[o + 10], ref r[o + 11],
                ref r[o + 12], ref r[o + 13], ref r[o + 14], ref r[o + 15]);
        }

        for (var i = 0; i < 8; i++)
        {
            var o = 2 * i;
            Round(
                ref r[o], ref r[o + 1], ref r[o + 16], ref r[o + 17],
                ref r[o + 32], ref r[o + 33], ref r[o + 48], ref r[o + 49],
                ref r[o + 64], ref r[o + 65], ref r[o + 80], ref r[o + 81],
                ref r[o + 96], ref r[o + 97], ref r[o + 112], ref r[o + 113]);
        }

        for (var i = 0; i < BlockWords; i++)
        {
            next[i] = t[i] ^ r[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong BlaMka(ulong x, ulong y) => x + y + (2 * (ulong)(uint)x * (uint)y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void GB(ref ulong a, ref ulong b, ref ulong c, ref ulong d)
    {
        a = BlaMka(a, b);
        d = BitOperations.RotateRight(d ^ a, 32);
        c = BlaMka(c, d);
        b = BitOperations.RotateRight(b ^ c, 24);
        a = BlaMka(a, b);
        d = BitOperations.RotateRight(d ^ a, 16);
        c = BlaMka(c, d);
        b = BitOperations.RotateRight(b ^ c, 63);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Round(
        ref ulong v0, ref ulong v1, ref ulong v2, ref ulong v3,
        ref ulong v4, ref ulong v5, ref ulong v6, ref ulong v7,
        ref ulong v8, ref ulong v9, ref ulong v10, ref ulong v11,
        ref ulong v12, ref ulong v13, ref ulong v14, ref ulong v15)
    {
        GB(ref v0, ref v4, ref v8, ref v12);
        GB(ref v1, ref v5, ref v9, ref v13);
        GB(ref v2, ref v6, ref v10, ref v14);
        GB(ref v3, ref v7, ref v11, ref v15);
        GB(ref v0, ref v5, ref v10, ref v15);
        GB(ref v1, ref v6, ref v11, ref v12);
        GB(ref v2, ref v7, ref v8, ref v13);
        GB(ref v3, ref v4, ref v9, ref v14);
    }

    // -------------------------------------------------------------------- AVX2

    private static void FillBlockAvx2(ulong* previous, ulong* reference, ulong* next, bool withXor)
    {
        var r = stackalloc Vector256<ulong>[BlockWords / 4];
        var t = stackalloc Vector256<ulong>[BlockWords / 4];
        for (var i = 0; i < BlockWords / 4; i++)
        {
            var x = Avx.LoadVector256(reference + (4 * i)) ^ Avx.LoadVector256(previous + (4 * i));
            r[i] = x;
            t[i] = withXor ? x ^ Avx.LoadVector256(next + (4 * i)) : x;
        }

        for (var i = 0; i < 8; i++)
        {
            RoundV(ref r[4 * i], ref r[(4 * i) + 1], ref r[(4 * i) + 2], ref r[(4 * i) + 3]);
        }

        var words = (ulong*)r;
        for (var i = 0; i < 8; i++)
        {
            var b0 = words + (2 * i);
            var a = Vector256.Create(Vector128.Load(b0), Vector128.Load(b0 + 16));
            var b = Vector256.Create(Vector128.Load(b0 + 32), Vector128.Load(b0 + 48));
            var c = Vector256.Create(Vector128.Load(b0 + 64), Vector128.Load(b0 + 80));
            var d = Vector256.Create(Vector128.Load(b0 + 96), Vector128.Load(b0 + 112));
            RoundV(ref a, ref b, ref c, ref d);
            a.GetLower().Store(b0);
            a.GetUpper().Store(b0 + 16);
            b.GetLower().Store(b0 + 32);
            b.GetUpper().Store(b0 + 48);
            c.GetLower().Store(b0 + 64);
            c.GetUpper().Store(b0 + 80);
            d.GetLower().Store(b0 + 96);
            d.GetUpper().Store(b0 + 112);
        }

        for (var i = 0; i < BlockWords / 4; i++)
        {
            Avx.Store(next + (4 * i), t[i] ^ r[i]);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<ulong> BlaMkaV(Vector256<ulong> x, Vector256<ulong> y)
    {
        var product = Avx2.Multiply(x.AsUInt32(), y.AsUInt32());
        return x + y + product + product;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<ulong> Rotr32(Vector256<ulong> x) => Avx2.Shuffle(x.AsUInt32(), 0b10_11_00_01).AsUInt64();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<ulong> Rotr24(Vector256<ulong> x) => Avx2.ShiftRightLogical(x, 24) | Avx2.ShiftLeftLogical(x, 40);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<ulong> Rotr16(Vector256<ulong> x) => Avx2.ShiftRightLogical(x, 16) | Avx2.ShiftLeftLogical(x, 48);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<ulong> Rotr63(Vector256<ulong> x) => Avx2.ShiftRightLogical(x, 63) | (x + x);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void GV(ref Vector256<ulong> a, ref Vector256<ulong> b, ref Vector256<ulong> c, ref Vector256<ulong> d)
    {
        a = BlaMkaV(a, b);
        d = Rotr32(d ^ a);
        c = BlaMkaV(c, d);
        b = Rotr24(b ^ c);
        a = BlaMkaV(a, b);
        d = Rotr16(d ^ a);
        c = BlaMkaV(c, d);
        b = Rotr63(b ^ c);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void RoundV(ref Vector256<ulong> a, ref Vector256<ulong> b, ref Vector256<ulong> c, ref Vector256<ulong> d)
    {
        GV(ref a, ref b, ref c, ref d);
        b = Avx2.Permute4x64(b, 0x39);
        c = Avx2.Permute4x64(c, 0x4E);
        d = Avx2.Permute4x64(d, 0x93);
        GV(ref a, ref b, ref c, ref d);
        b = Avx2.Permute4x64(b, 0x93);
        c = Avx2.Permute4x64(c, 0x4E);
        d = Avx2.Permute4x64(d, 0x39);
    }
}
