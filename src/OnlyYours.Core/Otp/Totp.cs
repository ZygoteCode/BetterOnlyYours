using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using OnlyYours.Core.Security.Memory;

namespace OnlyYours.Core.Otp;

public enum TotpAlgorithm : byte
{
    Sha1 = 1,
    Sha256 = 2,
    Sha512 = 3,
}

public enum TotpKind : byte
{
    Standard = 0,
    Steam = 1,
}

/// <summary>Everything about a token except the secret itself.</summary>
public sealed record TotpConfig
{
    public const int DefaultDigits = 6;
    public const int DefaultPeriod = 30;
    public const int MinDigits = 4;
    public const int MaxDigits = 10;
    public const int MinPeriod = 5;
    public const int MaxPeriod = 300;

    public TotpAlgorithm Algorithm { get; init; } = TotpAlgorithm.Sha1;

    public int Digits { get; init; } = DefaultDigits;

    public int Period { get; init; } = DefaultPeriod;

    public TotpKind Kind { get; init; } = TotpKind.Standard;

    public string Issuer { get; init; } = string.Empty;

    public string Account { get; init; } = string.Empty;

    public int EffectiveDigits => Kind == TotpKind.Steam ? 5 : Digits;

    public TotpConfig Normalized() => this with
    {
        Digits = Math.Clamp(Digits, MinDigits, MaxDigits),
        Period = Math.Clamp(Period, MinPeriod, MaxPeriod),
        Issuer = Issuer.Trim(),
        Account = Account.Trim(),
    };

    public static TotpAlgorithm ParseAlgorithm(string? raw) =>
        (raw?.Trim().Replace("-", string.Empty, StringComparison.Ordinal).ToUpperInvariant()) switch
        {
            "SHA256" => TotpAlgorithm.Sha256,
            "SHA512" => TotpAlgorithm.Sha512,
            _ => TotpAlgorithm.Sha1,
        };

    public static string AlgorithmLabel(TotpAlgorithm algorithm) => algorithm switch
    {
        TotpAlgorithm.Sha256 => "SHA256",
        TotpAlgorithm.Sha512 => "SHA512",
        _ => "SHA1",
    };
}

/// <summary>A generated code plus the timing the interface needs to animate it.</summary>
public readonly record struct TotpCode(string Value, long Counter, int Period, int MillisecondsRemaining)
{
    public int SecondsRemaining => (MillisecondsRemaining + 999) / 1000;

    /// <summary>1.0 right after a refresh, 0.0 at expiry.</summary>
    public double Progress => Math.Clamp(MillisecondsRemaining / (Period * 1000.0), 0, 1);

    /// <summary>"123 456" — grouped for reading aloud.</summary>
    public string Grouped
    {
        get
        {
            var text = Value;
            if (text.Length < 6)
            {
                return text;
            }

            var split = text.Length % 2 == 0 ? text.Length / 2 : 3;
            return string.Concat(text.AsSpan(0, split), " ", text.AsSpan(split));
        }
    }
}

/// <summary>RFC 4226 (HOTP) and RFC 6238 (TOTP), plus Steam Guard's alphabet.</summary>
public static class Totp
{
    public const string SteamAlphabet = "23456789BCDFGHJKMNPQRTVWXY";

    public static long CounterAt(DateTimeOffset time, int period) => time.ToUnixTimeSeconds() / period;

    public static int MillisecondsRemaining(DateTimeOffset time, int period)
    {
        var window = period * 1000L;
        return (int)(window - (time.ToUnixTimeMilliseconds() % window));
    }

    public static string Hotp(ReadOnlySpan<byte> secret, long counter, TotpConfig config)
    {
        if (secret.IsEmpty)
        {
            throw new ArgumentException("The secret is empty.", nameof(secret));
        }

        Span<byte> message = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(message, counter);
        Span<byte> digest = stackalloc byte[64];
        var length = config.Algorithm switch
        {
            TotpAlgorithm.Sha256 => HMACSHA256.HashData(secret, message, digest),
            TotpAlgorithm.Sha512 => HMACSHA512.HashData(secret, message, digest),
            _ => HMACSHA1.HashData(secret, message, digest),
        };

        var offset = digest[length - 1] & 0x0F;
        var binary = ((digest[offset] & 0x7F) << 24)
            | (digest[offset + 1] << 16)
            | (digest[offset + 2] << 8)
            | digest[offset + 3];
        CryptographicOperations.ZeroMemory(digest);

        if (config.Kind == TotpKind.Steam)
        {
            Span<char> chars = stackalloc char[5];
            var value = binary;
            for (var i = 0; i < 5; i++)
            {
                chars[i] = SteamAlphabet[value % SteamAlphabet.Length];
                value /= SteamAlphabet.Length;
            }

            return new string(chars);
        }

        var digits = Math.Clamp(config.Digits, TotpConfig.MinDigits, TotpConfig.MaxDigits);
        var modulus = 1L;
        for (var i = 0; i < digits; i++)
        {
            modulus *= 10;
        }

        return (binary % modulus).ToString(System.Globalization.CultureInfo.InvariantCulture).PadLeft(digits, '0');
    }

    public static TotpCode Generate(ReadOnlySpan<byte> secret, TotpConfig config, DateTimeOffset at)
    {
        var normalized = config.Normalized();
        var counter = CounterAt(at, normalized.Period);
        return new TotpCode(Hotp(secret, counter, normalized), counter, normalized.Period, MillisecondsRemaining(at, normalized.Period));
    }
}

/// <summary>
/// Binary form of a token inside its sealed field:
/// <c>u8 version ‖ u8 algorithm ‖ u8 digits ‖ u16 period ‖ u8 kind ‖ issuer ‖ account ‖ secret</c>.
/// Issuer and account are sealed too: the stored blob says "there is a token" and nothing else.
/// </summary>
public static class TotpPayload
{
    private const byte Version = 1;

    public static SecureBuffer Encode(ReadOnlySpan<byte> secret, TotpConfig config)
    {
        var normalized = config.Normalized();
        var issuer = Encoding.UTF8.GetBytes(Truncate(normalized.Issuer));
        var account = Encoding.UTF8.GetBytes(Truncate(normalized.Account));
        var buffer = SecureBuffer.Allocate(6 + 1 + issuer.Length + 1 + account.Length + secret.Length);
        var span = buffer.Span;
        span[0] = Version;
        span[1] = (byte)normalized.Algorithm;
        span[2] = (byte)normalized.Digits;
        BinaryPrimitives.WriteUInt16LittleEndian(span[3..], (ushort)normalized.Period);
        span[5] = (byte)normalized.Kind;
        var position = 6;
        span[position++] = (byte)issuer.Length;
        issuer.CopyTo(span[position..]);
        position += issuer.Length;
        span[position++] = (byte)account.Length;
        account.CopyTo(span[position..]);
        position += account.Length;
        secret.CopyTo(span[position..]);
        return buffer;
    }

    /// <summary>Splits a payload into its config and a view of the secret bytes (valid while <paramref name="payload"/> is alive).</summary>
    public static bool TryDecode(ReadOnlySpan<byte> payload, out TotpConfig config, out int secretOffset)
    {
        config = new TotpConfig();
        secretOffset = 0;
        if (payload.Length < 8 || payload[0] != Version)
        {
            return false;
        }

        var algorithm = (TotpAlgorithm)payload[1];
        var digits = payload[2];
        var period = BinaryPrimitives.ReadUInt16LittleEndian(payload[3..]);
        var kind = (TotpKind)payload[5];
        var position = 6;
        int issuerLength = payload[position++];
        if (position + issuerLength + 1 > payload.Length)
        {
            return false;
        }

        var issuer = Encoding.UTF8.GetString(payload.Slice(position, issuerLength));
        position += issuerLength;
        int accountLength = payload[position++];
        if (position + accountLength >= payload.Length)
        {
            return false;
        }

        var account = Encoding.UTF8.GetString(payload.Slice(position, accountLength));
        position += accountLength;
        config = new TotpConfig
        {
            Algorithm = Enum.IsDefined(algorithm) ? algorithm : TotpAlgorithm.Sha1,
            Digits = digits,
            Period = period,
            Kind = Enum.IsDefined(kind) ? kind : TotpKind.Standard,
            Issuer = issuer,
            Account = account,
        }.Normalized();
        secretOffset = position;
        return true;
    }

    private static string Truncate(string value)
    {
        if (Encoding.UTF8.GetByteCount(value) <= 200)
        {
            return value;
        }

        return value[..Math.Min(value.Length, 60)];
    }
}
