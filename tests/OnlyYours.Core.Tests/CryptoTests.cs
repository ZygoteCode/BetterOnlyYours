using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OnlyYours.Core.Security.Crypto;
using Xunit;

namespace OnlyYours.Core.Tests;

public class Blake2bTests
{
    [Theory]
    [InlineData("", "786a02f742015903c6c6fd852552d272912f4740e15847618a86e217f71f5419d25e1031afee585313896444934eb04b903a685b1448b755d56f701afe9be2ce")]
    [InlineData("abc", "ba80a53f981c4d0d6a2797b69f12f6e94c212f14685ac4b74b12bb6fdbffa2d17d87c5392aab792dc252d5de4533cc9518d38aa8dbf1925ab92386edd4009923")]
    public void Blake2b512MatchesRfc7693(string input, string expected)
    {
        var output = new byte[64];
        Blake2b.Hash(Encoding.ASCII.GetBytes(input), output);
        Assert.Equal(expected, T.ToHex(output));
    }

    [Fact]
    public void IncrementalUpdatesEqualOneShot()
    {
        var data = new byte[1000];
        new Random(42).NextBytes(data);
        var oneShot = new byte[64];
        Blake2b.Hash(data, oneShot);

        foreach (var chunk in new[] { 1, 7, 64, 127, 128, 129, 333 })
        {
            var hasher = new Blake2b(64);
            for (var offset = 0; offset < data.Length; offset += chunk)
            {
                hasher.Update(data.AsSpan(offset, Math.Min(chunk, data.Length - offset)));
            }

            var incremental = new byte[64];
            hasher.Final(incremental);
            Assert.Equal(oneShot, incremental);
        }
    }
}

public class Argon2Tests
{
    private static readonly byte[] Password = Enumerable.Repeat((byte)0x01, 32).ToArray();
    private static readonly byte[] Salt = Enumerable.Repeat((byte)0x02, 16).ToArray();
    private static readonly byte[] Secret = Enumerable.Repeat((byte)0x03, 8).ToArray();
    private static readonly byte[] Ad = Enumerable.Repeat((byte)0x04, 12).ToArray();

    [Theory]
    [InlineData(Argon2Type.D, "512b391b6f1162975371d30919734294f868e3be3984f3c1a13a4db9fabe4acb")]
    [InlineData(Argon2Type.I, "c814d9d1dc7f37aa13f0d77f2494bda1c8de6b016dd388d29952a4c4672b6ce8")]
    [InlineData(Argon2Type.Id, "0d640df58d78766c08c037a34a8b53c9d01ef0452d75b65eb52520e96b01e659")]
    public void MatchesRfc9106Vectors(Argon2Type type, string expected)
    {
        foreach (var scalar in new[] { false, true })
        {
            Argon2.ForceScalar = scalar;
            try
            {
                var tag = new byte[32];
                Argon2.Derive(type, Password, Salt, 32, 3, 4, tag, Secret, Ad);
                Assert.Equal(expected, T.ToHex(tag));
            }
            finally
            {
                Argon2.ForceScalar = false;
            }
        }
    }

    [Fact]
    public void ScalarAndVectorPathsAgreeOnLargerMemory()
    {
        var accelerated = new byte[64];
        var scalar = new byte[64];
        Argon2.DeriveId("onlyyours"u8, "0123456789abcdef"u8, 4096, 2, 3, accelerated);
        Argon2.ForceScalar = true;
        try
        {
            Argon2.DeriveId("onlyyours"u8, "0123456789abcdef"u8, 4096, 2, 3, scalar);
        }
        finally
        {
            Argon2.ForceScalar = false;
        }

        Assert.Equal(accelerated, scalar);
    }

    [Fact]
    public void MatchesPointyCastleCrossVectorsFromBetterOnlyYours()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(T.FixturePath("Legacy", "manifest.json")));
        var vectors = manifest.RootElement.GetProperty("argon2id").EnumerateArray().ToList();
        Assert.NotEmpty(vectors);
        foreach (var vector in vectors)
        {
            var key = new byte[32];
            Argon2.DeriveId(
                T.Hex(vector.GetProperty("password").GetString()!),
                T.Hex(vector.GetProperty("salt").GetString()!),
                vector.GetProperty("memoryKiB").GetUInt32(),
                vector.GetProperty("iterations").GetUInt32(),
                vector.GetProperty("parallelism").GetUInt32(),
                key);
            Assert.Equal(vector.GetProperty("key").GetString(), T.ToHex(key));
        }
    }

    [Fact]
    public void LegacyPbkdf2AndHkdfMatchDotNet()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(T.FixturePath("Legacy", "manifest.json")));
        var pbkdf2 = manifest.RootElement.GetProperty("pbkdf2Sha256");
        var derived = Rfc2898DeriveBytes.Pbkdf2(
            T.Hex(pbkdf2.GetProperty("password").GetString()!),
            T.Hex(pbkdf2.GetProperty("salt").GetString()!),
            pbkdf2.GetProperty("iterations").GetInt32(),
            HashAlgorithmName.SHA256,
            32);
        Assert.Equal(pbkdf2.GetProperty("key").GetString(), T.ToHex(derived));

        var hkdf = manifest.RootElement.GetProperty("hkdfSha256");
        var key = HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            T.Hex(hkdf.GetProperty("ikm").GetString()!),
            32,
            T.Hex(hkdf.GetProperty("salt").GetString()!),
            Encoding.UTF8.GetBytes(hkdf.GetProperty("info").GetString()!));
        Assert.Equal(hkdf.GetProperty("key").GetString(), T.ToHex(key));
    }

    [Fact]
    public void CancellationStopsDerivation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var output = new byte[32];
        Assert.ThrowsAny<OperationCanceledException>(() => Argon2.DeriveId("x"u8, "0123456789abcdef"u8, 65536, 3, 1, output, cancellationToken: cts.Token));
    }

    [Fact]
    public void KdfPresetsAreSaneAndOrdered()
    {
        Assert.True(KdfParameters.Standard.IsWithinBounds);
        Assert.True(KdfParameters.High.IsWithinBounds);
        Assert.True(KdfParameters.Extreme.IsWithinBounds);
        Assert.True(KdfParameters.Standard.IsWeakerThan(KdfParameters.High));
        Assert.True(KdfParameters.High.IsWeakerThan(KdfParameters.Extreme));
        Assert.Equal(KdfParameters.High, KdfParameters.Default);
        Assert.False(new KdfParameters(KdfAlgorithm.Argon2id, 1024, 1, 1).IsWithinBounds);
        Assert.False(new KdfParameters(KdfAlgorithm.Argon2id, 16384, 0, 1).IsWithinBounds);
    }
}

public class CascadeAeadTests
{
    private static (byte[] K1, byte[] K2, byte[] N1, byte[] N2) Keys() =>
        (RandomNumberGenerator.GetBytes(32), RandomNumberGenerator.GetBytes(32), RandomNumberGenerator.GetBytes(12), RandomNumberGenerator.GetBytes(12));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4096)]
    public void RoundTrips(int size)
    {
        var (k1, k2, n1, n2) = Keys();
        var plaintext = RandomNumberGenerator.GetBytes(size);
        var aad = "header"u8.ToArray();
        var ciphertext = new byte[size + CascadeAead.Overhead];
        CascadeAead.Encrypt(k1, k2, n1, n2, plaintext, aad, ciphertext);
        var decrypted = new byte[size];
        Assert.True(CascadeAead.TryDecrypt(k1, k2, n1, n2, ciphertext, aad, decrypted));
        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void RejectsAnyTampering()
    {
        var (k1, k2, n1, n2) = Keys();
        var plaintext = "the vault"u8.ToArray();
        var aad = "header"u8.ToArray();
        var ciphertext = new byte[plaintext.Length + CascadeAead.Overhead];
        CascadeAead.Encrypt(k1, k2, n1, n2, plaintext, aad, ciphertext);
        var output = new byte[plaintext.Length];

        for (var i = 0; i < ciphertext.Length; i++)
        {
            var copy = (byte[])ciphertext.Clone();
            copy[i] ^= 0x01;
            Assert.False(CascadeAead.TryDecrypt(k1, k2, n1, n2, copy, aad, output));
        }

        Assert.False(CascadeAead.TryDecrypt(k1, k2, n1, n2, ciphertext, "Header"u8, output));
        Assert.False(CascadeAead.TryDecrypt(k2, k1, n1, n2, ciphertext, aad, output));
        Assert.False(CascadeAead.TryDecrypt(k1, k2, n2, n1, ciphertext, aad, output));
    }
}

public class PadmeTests
{
    [Fact]
    public void PaddingIsMonotonicBoundedAndAtLeastTheMinimum()
    {
        long previous = 0;
        for (long length = 0; length < 200_000; length += 97)
        {
            var padded = Padme.PaddedLength(length);
            Assert.True(padded >= length);
            Assert.True(padded >= Padme.MinimumSize);
            Assert.True(padded >= previous);
            if (length > 4096)
            {
                Assert.True(padded - length <= length * 0.12, $"overhead too large at {length}");
            }

            previous = padded;
        }
    }

    [Fact]
    public void SimilarSizesShareABucket()
    {
        Assert.Equal(Padme.PaddedLength(10_001), Padme.PaddedLength(10_050));
    }
}
