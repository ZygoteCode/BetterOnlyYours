using System.Text;
using OnlyYours.Core.Otp;
using OnlyYours.Core.Security.Memory;
using OnlyYours.Core.Vault;
using Xunit;

namespace OnlyYours.Core.Tests;

public class TotpTests
{
    private static readonly byte[] Sha1Seed = Encoding.ASCII.GetBytes("12345678901234567890");
    private static readonly byte[] Sha256Seed = Encoding.ASCII.GetBytes("12345678901234567890123456789012");
    private static readonly byte[] Sha512Seed = Encoding.ASCII.GetBytes("1234567890123456789012345678901234567890123456789012345678901234");

    public static TheoryData<TotpAlgorithm, long, string> Rfc6238 => new()
    {
        { TotpAlgorithm.Sha1, 59, "94287082" },
        { TotpAlgorithm.Sha1, 1111111109, "07081804" },
        { TotpAlgorithm.Sha1, 1111111111, "14050471" },
        { TotpAlgorithm.Sha1, 1234567890, "89005924" },
        { TotpAlgorithm.Sha1, 2000000000, "69279037" },
        { TotpAlgorithm.Sha1, 20000000000, "65353130" },
        { TotpAlgorithm.Sha256, 59, "46119246" },
        { TotpAlgorithm.Sha256, 1111111109, "68084774" },
        { TotpAlgorithm.Sha256, 1111111111, "67062674" },
        { TotpAlgorithm.Sha256, 1234567890, "91819424" },
        { TotpAlgorithm.Sha256, 2000000000, "90698825" },
        { TotpAlgorithm.Sha256, 20000000000, "77737706" },
        { TotpAlgorithm.Sha512, 59, "90693936" },
        { TotpAlgorithm.Sha512, 1111111109, "25091201" },
        { TotpAlgorithm.Sha512, 1111111111, "99943326" },
        { TotpAlgorithm.Sha512, 1234567890, "93441116" },
        { TotpAlgorithm.Sha512, 2000000000, "38618901" },
        { TotpAlgorithm.Sha512, 20000000000, "47863826" },
    };

    [Theory]
    [MemberData(nameof(Rfc6238))]
    public void MatchesRfc6238(TotpAlgorithm algorithm, long unixSeconds, string expected)
    {
        var seed = algorithm switch { TotpAlgorithm.Sha256 => Sha256Seed, TotpAlgorithm.Sha512 => Sha512Seed, _ => Sha1Seed };
        var config = new TotpConfig { Algorithm = algorithm, Digits = 8, Period = 30 };
        var code = Totp.Generate(seed, config, DateTimeOffset.FromUnixTimeSeconds(unixSeconds));
        Assert.Equal(expected, code.Value);
        Assert.Equal(unixSeconds / 30, code.Counter);
    }

    [Theory]
    [InlineData(0, "755224")]
    [InlineData(1, "287082")]
    [InlineData(2, "359152")]
    [InlineData(3, "969429")]
    [InlineData(4, "338314")]
    [InlineData(5, "254676")]
    [InlineData(6, "287922")]
    [InlineData(7, "162583")]
    [InlineData(8, "399871")]
    [InlineData(9, "520489")]
    public void MatchesRfc4226Hotp(long counter, string expected)
    {
        Assert.Equal(expected, Totp.Hotp(Sha1Seed, counter, new TotpConfig()));
    }

    [Fact]
    public void SteamCodesUseTheSteamAlphabet()
    {
        var config = new TotpConfig { Kind = TotpKind.Steam };
        var code = Totp.Generate(Sha1Seed, config, DateTimeOffset.FromUnixTimeSeconds(1700000000));
        Assert.Equal(5, code.Value.Length);
        Assert.All(code.Value, c => Assert.Contains(c, Totp.SteamAlphabet));
    }

    [Fact]
    public void RemainingTimeAndProgressAreConsistent()
    {
        // 1 699 999 990 s is 10 s into its 30 s window: 19.5 s remain at +500 ms.
        var at = DateTimeOffset.FromUnixTimeMilliseconds(1699999990500);
        var code = Totp.Generate(Sha1Seed, new TotpConfig(), at);
        Assert.Equal(19500, code.MillisecondsRemaining);
        Assert.Equal(20, code.SecondsRemaining);
        Assert.InRange(code.Progress, 0.64, 0.66);
        Assert.Equal(code.Value[..3] + " " + code.Value[3..], code.Grouped);
    }

    [Fact]
    public void PayloadRoundTripsConfigAndSecret()
    {
        var config = new TotpConfig { Algorithm = TotpAlgorithm.Sha512, Digits = 7, Period = 45, Issuer = "Acme", Account = "ada" };
        using var payload = TotpPayload.Encode(Sha1Seed, config);
        Assert.True(TotpPayload.TryDecode(payload.Span, out var decoded, out var offset));
        Assert.Equal(config with { }, decoded with { });
        Assert.Equal(Sha1Seed, payload.Span[offset..].ToArray());
    }
}

public class Base32Tests
{
    [Theory]
    [InlineData("JBSWY3DPEHPK3PXP", "48656c6c6f21deadbeef")]
    [InlineData("jbsw y3dp-ehpk 3pxp", "48656c6c6f21deadbeef")]
    [InlineData("MZXW6===", "666f6f")]
    [InlineData("MZXW6YTBOI======", "666f6f626172")]
    public void DecodesWithRelaxedFormatting(string input, string expectedHex)
    {
        Assert.True(Base32.TryDecode(input, out var bytes, out var error), error);
        using (bytes)
        {
            Assert.Equal(expectedHex, T.ToHex(bytes!.Span));
        }
    }

    [Theory]
    [InlineData("", "empty")]
    [InlineData("JBSW!3DP", "invalid-character")]
    [InlineData("A", "invalid-length")]
    public void ReportsPreciseErrors(string input, string expected)
    {
        Assert.False(Base32.TryDecode(input, out var bytes, out var error));
        Assert.Null(bytes);
        Assert.Equal(expected, error);
    }

    [Fact]
    public void EncodeRoundTrips()
    {
        var data = new byte[37];
        new Random(7).NextBytes(data);
        var text = Base32.EncodeToString(data);
        Assert.True(Base32.TryDecode(text, out var bytes, out _));
        using (bytes)
        {
            Assert.Equal(data, bytes!.Span.ToArray());
        }

        using var buffer = new SecureCharBuffer();
        Base32.Encode(data, buffer);
        Assert.Equal(text, buffer.Span.ToString());
    }
}

public class OtpAuthUriTests
{
    [Fact]
    public void ParsesAFullUri()
    {
        const string uri = "otpauth://totp/ACME%20Co:ada@example.com?secret=JBSWY3DPEHPK3PXP&issuer=ACME%20Co&algorithm=SHA256&digits=8&period=60";
        Assert.True(OtpAuthUri.LooksLikeUri(uri));
        Assert.True(OtpAuthUri.TryParse(uri, null, out var setup, out var error), error);
        using (setup)
        {
            Assert.Equal(TotpAlgorithm.Sha256, setup!.Config.Algorithm);
            Assert.Equal(8, setup.Config.Digits);
            Assert.Equal(60, setup.Config.Period);
            Assert.Equal("ACME Co", setup.Config.Issuer);
            Assert.Equal("ada@example.com", setup.Config.Account);
            Assert.Equal("48656c6c6f21deadbeef", T.ToHex(setup.Secret.Span));
        }
    }

    [Fact]
    public void AcceptsABareSecretWithDefaults()
    {
        Assert.True(OtpAuthUri.TryParse("JBSWY3DPEHPK3PXP", new TotpConfig { Digits = 6 }, out var setup, out _));
        using (setup)
        {
            Assert.Equal(TotpAlgorithm.Sha1, setup!.Config.Algorithm);
            Assert.Equal(30, setup.Config.Period);
        }
    }

    [Theory]
    [InlineData("otpauth://hotp/x?secret=JBSWY3DPEHPK3PXP&counter=1", "hotp-unsupported")]
    [InlineData("otpauth://totp/x?issuer=a", "no-secret")]
    [InlineData("otpauth://totp/x?secret=JB!!", "invalid-character")]
    public void RejectsUnsupportedOrBrokenLinks(string uri, string expected)
    {
        Assert.False(OtpAuthUri.TryParse(uri, null, out var setup, out var error));
        Assert.Null(setup);
        Assert.Equal(expected, error);
    }

    [Fact]
    public void FormatProducesAParsableUri()
    {
        var secret = "secret-bytes-1234"u8.ToArray();
        var config = new TotpConfig { Algorithm = TotpAlgorithm.Sha512, Digits = 8, Period = 30, Issuer = "OnlyYours & Co", Account = "ada lovelace" };
        using var output = new SecureCharBuffer();
        OtpAuthUri.Format(secret, config, output);
        Assert.StartsWith("otpauth://totp/", output.Span.ToString());
        Assert.True(OtpAuthUri.TryParse(output.Span, null, out var setup, out _));
        using (setup)
        {
            Assert.Equal(secret, setup!.Secret.Span.ToArray());
            Assert.Equal(config.Issuer, setup.Config.Issuer);
            Assert.Equal(config.Account, setup.Config.Account);
            Assert.Equal(TotpAlgorithm.Sha512, setup.Config.Algorithm);
        }
    }
}

public class RecoveryKeyTests
{
    [Fact]
    public void GeneratesFormatsAndParses()
    {
        using var code = RecoveryKey.Generate(out var key);
        using (key)
        {
            Assert.Equal(RecoveryKey.FormattedLength, code.Length);
            Assert.Equal(RecoveryKey.KeyBytes, key.Length);
            var text = code.Span.ToString();
            Assert.Equal(RecoveryKey.EncodedChars / RecoveryKey.GroupSize, text.Split('-').Length);

            Assert.True(RecoveryKey.TryParse(text, out var parsed));
            using (parsed)
            {
                Assert.Equal(key.Span.ToArray(), parsed!.Span.ToArray());
            }

            // Lower case, no dashes, extra spaces: still the same key.
            Assert.True(RecoveryKey.TryParse(" " + text.Replace("-", " ").ToLowerInvariant() + " ", out var relaxed));
            using (relaxed)
            {
                Assert.Equal(key.Span.ToArray(), relaxed!.Span.ToArray());
            }
        }
    }

    [Fact]
    public void ChecksumCatchesTypos()
    {
        using var code = RecoveryKey.Generate(out var key);
        key.Dispose();
        var text = code.Span.ToString().ToCharArray();
        var detected = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '-')
            {
                continue;
            }

            var original = text[i];
            text[i] = original == 'A' ? 'B' : 'A';
            if (!RecoveryKey.TryParse(text, out var parsed))
            {
                detected++;
            }

            parsed?.Dispose();
            text[i] = original;
        }

        // A 16-bit checksum lets through roughly one typo in 65 536.
        Assert.True(detected >= RecoveryKey.EncodedChars - 1, $"only {detected} typos detected");
    }
}
