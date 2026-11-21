using OnlyYours.Core.Security.Memory;

namespace OnlyYours.Core.Otp;

/// <summary>
/// RFC 4648 base32 — the encoding every authenticator uses for TOTP secrets.
/// Forgiving about presentation (case, spaces, dashes, missing padding), strict
/// about content: an alphabet violation or an impossible length is an error,
/// never a silently mangled key. Works on spans, so a secret pasted by the user is
/// decoded straight into secure memory.
/// </summary>
public static class Base32
{
    public const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static bool TryDecode(ReadOnlySpan<char> input, out SecureBuffer? bytes, out string? error)
    {
        bytes = null;
        error = null;
        var symbols = 0;
        foreach (var c in input)
        {
            if (IsIgnorable(c))
            {
                continue;
            }

            if (c == '=')
            {
                break;
            }

            if (Value(c) < 0)
            {
                error = "invalid-character";
                return false;
            }

            symbols++;
        }

        if (symbols == 0)
        {
            error = "empty";
            return false;
        }

        if (symbols % 8 is 1 or 3 or 6)
        {
            error = "invalid-length";
            return false;
        }

        var output = SecureBuffer.Allocate(symbols * 5 / 8);
        var buffer = 0UL;
        var bits = 0;
        var index = 0;
        foreach (var c in input)
        {
            if (IsIgnorable(c))
            {
                continue;
            }

            if (c == '=')
            {
                break;
            }

            buffer = (buffer << 5) | (uint)Value(c);
            bits += 5;
            if (bits >= 8)
            {
                bits -= 8;
                output.Span[index++] = (byte)(buffer >> bits);
            }
        }

        if (bits > 0 && (buffer & ((1UL << bits) - 1)) != 0)
        {
            output.Dispose();
            error = "trailing-bits";
            return false;
        }

        bytes = output;
        return true;
    }

    public static void Encode(ReadOnlySpan<byte> data, SecureCharBuffer output)
    {
        var buffer = 0;
        var bits = 0;
        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                bits -= 5;
                output.Append(Alphabet[(buffer >> bits) & 31]);
            }

            buffer &= (1 << bits) - 1;
        }

        if (bits > 0)
        {
            output.Append(Alphabet[(buffer << (5 - bits)) & 31]);
        }
    }

    public static string EncodeToString(ReadOnlySpan<byte> data)
    {
        using var chars = new SecureCharBuffer((data.Length * 8 / 5) + 2);
        Encode(data, chars);
        return chars.Span.ToString();
    }

    private static bool IsIgnorable(char c) => c is ' ' or '-' or '\t' or '\r' or '\n' or '_';

    private static int Value(char c) => c switch
    {
        >= 'A' and <= 'Z' => c - 'A',
        >= 'a' and <= 'z' => c - 'a',
        >= '2' and <= '7' => c - '2' + 26,
        _ => -1,
    };
}
