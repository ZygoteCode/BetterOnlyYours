using System.Globalization;
using OnlyYours.Core.Security.Memory;

namespace OnlyYours.Core.Otp;

/// <summary>The outcome of reading an <c>otpauth://</c> link or a bare base32 secret.</summary>
public sealed class OtpSetup : IDisposable
{
    internal OtpSetup(SecureBuffer secret, TotpConfig config)
    {
        Secret = secret;
        Config = config;
    }

    public SecureBuffer Secret { get; }

    public TotpConfig Config { get; }

    public void Dispose() => Secret.Dispose();
}

/// <summary>
/// Parser for <c>otpauth://totp/Issuer:account?secret=…&amp;issuer=…&amp;algorithm=…&amp;digits=…&amp;period=…</c>.
/// It runs over a <see cref="ReadOnlySpan{T}"/> so the secret parameter is base32-decoded
/// directly into secure memory without ever becoming a string.
/// </summary>
public static class OtpAuthUri
{
    public static bool LooksLikeUri(ReadOnlySpan<char> text) =>
        text.TrimStart().StartsWith("otpauth://", StringComparison.OrdinalIgnoreCase);

    public static bool TryParse(ReadOnlySpan<char> text, TotpConfig? defaults, out OtpSetup? setup, out string? error)
    {
        setup = null;
        error = null;
        text = text.Trim();
        if (!LooksLikeUri(text))
        {
            if (!Base32.TryDecode(text, out var bare, out error))
            {
                return false;
            }

            setup = new OtpSetup(bare!, (defaults ?? new TotpConfig()).Normalized());
            return true;
        }

        var rest = text["otpauth://".Length..];
        var slash = rest.IndexOf('/');
        if (slash <= 0)
        {
            error = "malformed";
            return false;
        }

        var type = rest[..slash];
        if (type.Equals("hotp", StringComparison.OrdinalIgnoreCase))
        {
            error = "hotp-unsupported";
            return false;
        }

        if (!type.Equals("totp", StringComparison.OrdinalIgnoreCase))
        {
            error = "malformed";
            return false;
        }

        rest = rest[(slash + 1)..];
        var question = rest.IndexOf('?');
        var labelPart = question >= 0 ? rest[..question] : rest;
        var query = question >= 0 ? rest[(question + 1)..] : [];

        var label = Unescape(labelPart);
        string issuer = string.Empty, account = label;
        var colon = label.IndexOf(':', StringComparison.Ordinal);
        if (colon >= 0)
        {
            issuer = label[..colon].Trim();
            account = label[(colon + 1)..].Trim();
        }

        string? algorithm = null, encoder = null;
        int? digits = null, period = null;
        SecureBuffer? secret = null;
        while (!query.IsEmpty)
        {
            var amp = query.IndexOf('&');
            var pair = amp >= 0 ? query[..amp] : query;
            query = amp >= 0 ? query[(amp + 1)..] : [];
            var eq = pair.IndexOf('=');
            if (eq <= 0)
            {
                continue;
            }

            var key = pair[..eq];
            var value = pair[(eq + 1)..];
            if (key.Equals("secret", StringComparison.OrdinalIgnoreCase))
            {
                secret?.Dispose();
                secret = null;
                using var unescaped = UnescapeSecret(value);
                if (!Base32.TryDecode(unescaped.Span, out secret, out error))
                {
                    return false;
                }
            }
            else if (key.Equals("issuer", StringComparison.OrdinalIgnoreCase))
            {
                var parsed = Unescape(value).Trim();
                if (parsed.Length > 0)
                {
                    issuer = parsed;
                }
            }
            else if (key.Equals("algorithm", StringComparison.OrdinalIgnoreCase))
            {
                algorithm = Unescape(value);
            }
            else if (key.Equals("digits", StringComparison.OrdinalIgnoreCase))
            {
                digits = int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var d) ? d : null;
            }
            else if (key.Equals("period", StringComparison.OrdinalIgnoreCase))
            {
                period = int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var p) ? p : null;
            }
            else if (key.Equals("encoder", StringComparison.OrdinalIgnoreCase) || key.Equals("kind", StringComparison.OrdinalIgnoreCase))
            {
                encoder = Unescape(value);
            }
        }

        if (secret is null)
        {
            error = "no-secret";
            return false;
        }

        var config = new TotpConfig
        {
            Algorithm = TotpConfig.ParseAlgorithm(algorithm),
            Digits = digits ?? TotpConfig.DefaultDigits,
            Period = period ?? TotpConfig.DefaultPeriod,
            Kind = string.Equals(encoder, "steam", StringComparison.OrdinalIgnoreCase) ? TotpKind.Steam : TotpKind.Standard,
            Issuer = issuer,
            Account = account,
        }.Normalized();

        setup = new OtpSetup(secret, config);
        return true;
    }

    /// <summary>Canonical link, used by exports (the interface never shows it).</summary>
    public static void Format(ReadOnlySpan<byte> secret, TotpConfig config, SecureCharBuffer output)
    {
        var label = string.IsNullOrEmpty(config.Issuer) ? config.Account : $"{config.Issuer}:{config.Account}";
        output.Append("otpauth://totp/");
        output.Append(Uri.EscapeDataString(label));
        output.Append("?secret=");
        Base32.Encode(secret, output);
        if (!string.IsNullOrEmpty(config.Issuer))
        {
            output.Append("&issuer=");
            output.Append(Uri.EscapeDataString(config.Issuer));
        }

        output.Append("&algorithm=");
        output.Append(TotpConfig.AlgorithmLabel(config.Algorithm));
        output.Append("&digits=");
        output.Append(config.Digits.ToString(CultureInfo.InvariantCulture));
        output.Append("&period=");
        output.Append(config.Period.ToString(CultureInfo.InvariantCulture));
        if (config.Kind == TotpKind.Steam)
        {
            output.Append("&encoder=steam");
        }
    }

    private static string Unescape(ReadOnlySpan<char> value)
    {
        var text = value.ToString().Replace('+', ' ');
        try
        {
            return Uri.UnescapeDataString(text);
        }
        catch (UriFormatException)
        {
            return text;
        }
    }

    /// <summary>Percent-decoding for the secret parameter, into secure memory.</summary>
    private static SecureCharBuffer UnescapeSecret(ReadOnlySpan<char> value)
    {
        var output = new SecureCharBuffer(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c == '%' && i + 2 < value.Length &&
                int.TryParse(value.Slice(i + 1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
            {
                output.Append((char)code);
                i += 2;
            }
            else
            {
                output.Append(c);
            }
        }

        return output;
    }
}
