using System.Security.Cryptography;
using OnlyYours.Core.Security.Memory;

namespace OnlyYours.Core.Vault;

/// <summary>
/// The emergency key printed in the recovery kit: 240 random bits plus a 16-bit
/// checksum, written as 52 Crockford base-32 characters in groups of four
/// (<c>7K2Q-…</c>). Crockford's alphabet has no I, L, O or U, and decoding forgives
/// the usual transcription mistakes (o→0, i/l→1, lower case, missing dashes).
/// </summary>
public static class RecoveryKey
{
    public const int KeyBytes = 30;
    public const int EncodedChars = 52;
    public const int GroupSize = 4;
    public const int FormattedLength = EncodedChars + (EncodedChars / GroupSize) - 1;

    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    /// <summary>Creates a new key. The formatted code and the raw key are both returned in secure memory.</summary>
    public static SecureCharBuffer Generate(out SecureBuffer key)
    {
        key = SecureBuffer.Allocate(KeyBytes);
        RandomNumberGenerator.Fill(key.Span);
        return Format(key.Span);
    }

    public static SecureCharBuffer Format(ReadOnlySpan<byte> key)
    {
        if (key.Length != KeyBytes)
        {
            throw new ArgumentException("Recovery keys are 30 bytes.", nameof(key));
        }

        using var full = SecureBuffer.Allocate(KeyBytes + 2);
        key.CopyTo(full.Span);
        Checksum(key, full.Span[KeyBytes..]);

        var output = new SecureCharBuffer(FormattedLength);
        var bits = 0;
        var buffer = 0;
        var written = 0;
        foreach (var b in full.Span)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                bits -= 5;
                AppendSymbol(output, Alphabet[(buffer >> bits) & 31], ref written);
            }

            buffer &= (1 << bits) - 1;
        }

        if (bits > 0)
        {
            AppendSymbol(output, Alphabet[(buffer << (5 - bits)) & 31], ref written);
        }

        buffer = 0;
        return output;
    }

    /// <summary>Parses user input. Returns false on wrong length, bad characters or a checksum mismatch.</summary>
    public static bool TryParse(ReadOnlySpan<char> text, out SecureBuffer? key)
    {
        key = null;
        Span<byte> symbols = stackalloc byte[EncodedChars];
        var count = 0;
        foreach (var raw in text)
        {
            if (raw is '-' or ' ' or '\t' or '\r' or '\n')
            {
                continue;
            }

            var value = Decode(raw);
            if (value < 0 || count >= EncodedChars)
            {
                symbols.Clear();
                return false;
            }

            symbols[count++] = (byte)value;
        }

        if (count != EncodedChars)
        {
            symbols.Clear();
            return false;
        }

        using var full = SecureBuffer.Allocate(KeyBytes + 2);
        var bits = 0;
        var buffer = 0;
        var index = 0;
        foreach (var symbol in symbols)
        {
            buffer = (buffer << 5) | symbol;
            bits += 5;
            if (bits >= 8)
            {
                bits -= 8;
                full.Span[index++] = (byte)((buffer >> bits) & 0xFF);
                buffer &= (1 << bits) - 1;
            }
        }

        symbols.Clear();
        Span<byte> expected = stackalloc byte[2];
        Checksum(full.Span[..KeyBytes], expected);
        if (!CryptographicOperations.FixedTimeEquals(expected, full.Span[KeyBytes..]))
        {
            return false;
        }

        key = SecureBuffer.CopyOf(full.Span[..KeyBytes]);
        return true;
    }

    private static void AppendSymbol(SecureCharBuffer output, char symbol, ref int written)
    {
        if (written > 0 && written % GroupSize == 0)
        {
            output.Append('-');
        }

        output.Append(symbol);
        written++;
    }

    private static int Decode(char c)
    {
        c = char.ToUpperInvariant(c);
        c = c switch
        {
            'O' => '0',
            'I' or 'L' => '1',
            _ => c,
        };
        return Alphabet.IndexOf(c);
    }

    private static void Checksum(ReadOnlySpan<byte> key, Span<byte> destination)
    {
        Span<byte> input = stackalloc byte[24 + KeyBytes];
        "OnlyYours/v1/recovery/crc"u8.CopyTo(input);
        key.CopyTo(input[23..]);
        Span<byte> digest = stackalloc byte[32];
        SHA256.HashData(input[..(23 + KeyBytes)], digest);
        digest[..2].CopyTo(destination);
        CryptographicOperations.ZeroMemory(input);
        CryptographicOperations.ZeroMemory(digest);
    }
}
