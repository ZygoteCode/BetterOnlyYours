using OnlyYours.Core.Generators;

namespace OnlyYours.Core.Analysis;

public enum StrengthLevel
{
    Empty,
    VeryWeak,
    Weak,
    Fair,
    Strong,
    Excellent,
}

public enum StrengthHint
{
    Empty,
    TooShort,
    CommonPassword,
    ContainsCommonWord,
    KeyboardPattern,
    Repetition,
    Sequence,
    DateOrYear,
    OnlyDigits,
    AddMoreClasses,
    LongerIsStronger,
    Excellent,
}

public sealed record StrengthReport(StrengthLevel Level, double EntropyBits, IReadOnlyList<StrengthHint> Hints)
{
    public static StrengthReport Empty { get; } = new(StrengthLevel.Empty, 0, [StrengthHint.Empty]);

    /// <summary>0..1 for meters (110 bits or more fills the meter).</summary>
    public double Fraction => Math.Clamp(EntropyBits / 110.0, 0, 1);

    public double GuessesLog10 => Math.Max(0, (EntropyBits - 1) * Math.Log10(2));

    /// <summary>Expected time to guess, for a given number of guesses per second.</summary>
    public double SecondsToCrack(double guessesPerSecond) => Math.Pow(10, GuessesLog10 - Math.Log10(guessesPerSecond));
}

/// <summary>
/// Pattern-aware strength estimation in the spirit of zxcvbn: entropy per character
/// from the character classes, reduced for everything an attacker tries first —
/// common passwords (with l33t substitutions), dictionary words in English and
/// Italian, keyboard walks (QWERTY, QWERTZ, AZERTY), repetitions, sequences and
/// years. It works on a span: the password is never turned into a string.
/// </summary>
public static class PasswordStrength
{
    private static readonly Lazy<HashSet<string>> CommonPasswords = new(LoadCommon);
    private static readonly Lazy<HashSet<string>> Dictionary = new(LoadDictionary);

    private static readonly string[] KeyboardRows =
    [
        "1234567890", "qwertyuiop", "asdfghjkl", "zxcvbnm",
        "azertyuiop", "qsdfghjklm", "wxcvbn",
        "qwertzuiop", "yxcvbnm",
        "1qaz", "2wsx", "3edc", "4rfv", "5tgb", "6yhn", "7ujm",
        "qazwsx", "wsxedc", "edcrfv",
        "789456123", "147258369", "159753",
    ];

    private static readonly string[] KeyboardRowsReversed = [.. KeyboardRows.Select(Reversed)];

    public static StrengthReport Evaluate(ReadOnlySpan<char> password)
    {
        if (password.IsEmpty)
        {
            return StrengthReport.Empty;
        }

        var length = Math.Min(password.Length, 256);
        password = password[..length];
        Span<char> lower = stackalloc char[length];
        Span<char> leet = stackalloc char[length];
        Span<double> bits = stackalloc double[length];
        try
        {
            bool hasLower = false, hasUpper = false, hasDigit = false, hasSymbol = false, hasOther = false;
            for (var i = 0; i < length; i++)
            {
                var c = password[i];
                if (char.IsAsciiLetterLower(c))
                {
                    hasLower = true;
                }
                else if (char.IsAsciiLetterUpper(c))
                {
                    hasUpper = true;
                }
                else if (char.IsAsciiDigit(c))
                {
                    hasDigit = true;
                }
                else if (c < 128)
                {
                    hasSymbol = true;
                }
                else
                {
                    hasOther = true;
                }

                lower[i] = char.ToLowerInvariant(c);
                leet[i] = Unleet(lower[i]);
            }

            var pool = (hasLower ? 26 : 0) + (hasUpper ? 26 : 0) + (hasDigit ? 10 : 0) + (hasSymbol ? 33 : 0) + (hasOther ? 100 : 0);
            pool = Math.Max(pool, 10);
            var perChar = Math.Log2(pool);
            bits.Fill(perChar);

            var hints = new List<StrengthHint>();

            // Exact match against the most common passwords dominates everything else.
            var common = CommonPasswords.Value.GetAlternateLookup<ReadOnlySpan<char>>();
            if (common.Contains(lower) || common.Contains(leet))
            {
                return new StrengthReport(StrengthLevel.VeryWeak, Math.Min(8, length * perChar), [StrengthHint.CommonPassword]);
            }

            var flags = new PatternFlags();
            ApplyRepetition(lower, bits, ref flags);
            ApplySequences(lower, bits, ref flags);
            ApplyKeyboard(lower, bits, ref flags);
            ApplyYears(password, bits, ref flags);
            ApplyWords(lower, leet, password, bits, common, ref flags);

            var entropy = 0.0;
            foreach (var b in bits)
            {
                entropy += b;
            }

            if (flags.CommonWord)
            {
                hints.Add(StrengthHint.ContainsCommonWord);
            }

            if (flags.Keyboard)
            {
                hints.Add(StrengthHint.KeyboardPattern);
            }

            if (flags.Repetition)
            {
                hints.Add(StrengthHint.Repetition);
            }

            if (flags.Sequence)
            {
                hints.Add(StrengthHint.Sequence);
            }

            if (flags.Year)
            {
                hints.Add(StrengthHint.DateOrYear);
            }

            if (length < 12)
            {
                hints.Add(StrengthHint.TooShort);
            }

            if (hasDigit && !hasLower && !hasUpper && !hasSymbol && !hasOther)
            {
                hints.Add(StrengthHint.OnlyDigits);
            }

            var classes = (hasLower ? 1 : 0) + (hasUpper ? 1 : 0) + (hasDigit ? 1 : 0) + (hasSymbol || hasOther ? 1 : 0);
            if (classes < 3 && length < 20)
            {
                hints.Add(StrengthHint.AddMoreClasses);
            }

            var level = entropy switch
            {
                < 28 => StrengthLevel.VeryWeak,
                < 40 => StrengthLevel.Weak,
                < 60 => StrengthLevel.Fair,
                < 80 => StrengthLevel.Strong,
                _ => StrengthLevel.Excellent,
            };

            if (level < StrengthLevel.Excellent && length < 16 && !hints.Contains(StrengthHint.TooShort))
            {
                hints.Add(StrengthHint.LongerIsStronger);
            }

            if (level == StrengthLevel.Excellent && hints.Count == 0)
            {
                hints.Add(StrengthHint.Excellent);
            }

            return new StrengthReport(level, entropy, hints);
        }
        finally
        {
            lower.Clear();
            leet.Clear();
        }
    }

    private struct PatternFlags
    {
        public bool Repetition;
        public bool Sequence;
        public bool Keyboard;
        public bool Year;
        public bool CommonWord;
    }

    private static char Unleet(char c) => c switch
    {
        '4' or '@' => 'a',
        '3' => 'e',
        '1' or '!' or '|' => 'i',
        '0' => 'o',
        '$' or '5' => 's',
        '7' or '+' => 't',
        '8' => 'b',
        '9' => 'g',
        _ => c,
    };

    /// <summary>Replaces the entropy of a block with <paramref name="blockBits"/> if that is lower.</summary>
    private static bool Cover(Span<double> bits, int start, int count, double blockBits)
    {
        var current = 0.0;
        for (var i = start; i < start + count; i++)
        {
            current += bits[i];
        }

        if (blockBits >= current)
        {
            return false;
        }

        bits[start] = blockBits;
        for (var i = start + 1; i < start + count; i++)
        {
            bits[i] = 0;
        }

        return true;
    }

    private static void ApplyRepetition(ReadOnlySpan<char> lower, Span<double> bits, ref PatternFlags flags)
    {
        // Runs of the same character: "aaaa".
        for (var i = 0; i < lower.Length;)
        {
            var j = i + 1;
            while (j < lower.Length && lower[j] == lower[i])
            {
                j++;
            }

            if (j - i >= 3 && Cover(bits, i, j - i, bits[i] + Math.Log2(j - i)))
            {
                flags.Repetition = true;
            }

            i = j;
        }

        // Repeated blocks: "abcabc", "2024!2024!".
        for (var size = 2; size <= lower.Length / 2; size++)
        {
            for (var i = 0; i + (2 * size) <= lower.Length; i++)
            {
                if (lower.Slice(i, size).SequenceEqual(lower.Slice(i + size, size)) && Cover(bits, i + size, size, 1))
                {
                    flags.Repetition = true;
                }
            }
        }
    }

    private static void ApplySequences(ReadOnlySpan<char> lower, Span<double> bits, ref PatternFlags flags)
    {
        for (var i = 0; i < lower.Length - 2;)
        {
            var delta = lower[i + 1] - lower[i];
            if (delta is not (1 or -1) || !char.IsAsciiLetterOrDigit(lower[i]))
            {
                i++;
                continue;
            }

            var j = i + 1;
            while (j < lower.Length && lower[j] - lower[j - 1] == delta)
            {
                j++;
            }

            if (j - i >= 3 && Cover(bits, i, j - i, Math.Log2(36) + Math.Log2(j - i) + 1))
            {
                flags.Sequence = true;
            }

            i = j;
        }
    }

    private static void ApplyKeyboard(ReadOnlySpan<char> lower, Span<double> bits, ref PatternFlags flags)
    {
        for (var i = 0; i < lower.Length; i++)
        {
            var best = 0;
            for (var r = 0; r < KeyboardRows.Length; r++)
            {
                var row = KeyboardRows[r];
                var reversed = KeyboardRowsReversed[r];
                for (var length = Math.Min(lower.Length - i, row.Length); length >= 4; length--)
                {
                    var slice = lower.Slice(i, length);
                    if (row.AsSpan().IndexOf(slice) >= 0 || reversed.AsSpan().IndexOf(slice) >= 0)
                    {
                        best = Math.Max(best, length);
                        break;
                    }
                }
            }

            if (best >= 4 && Cover(bits, i, best, Math.Log2(KeyboardRows.Length * 10) + Math.Log2(best)))
            {
                flags.Keyboard = true;
                i += best - 1;
            }
        }
    }

    private static void ApplyYears(ReadOnlySpan<char> password, Span<double> bits, ref PatternFlags flags)
    {
        for (var i = 0; i + 4 <= password.Length; i++)
        {
            var slice = password.Slice(i, 4);
            if (!char.IsAsciiDigit(slice[0]) || !char.IsAsciiDigit(slice[1]) || !char.IsAsciiDigit(slice[2]) || !char.IsAsciiDigit(slice[3]))
            {
                continue;
            }

            var year = ((slice[0] - '0') * 1000) + ((slice[1] - '0') * 100) + ((slice[2] - '0') * 10) + (slice[3] - '0');
            if (year is >= 1900 and <= 2099 && Cover(bits, i, 4, Math.Log2(200)))
            {
                flags.Year = true;
            }
        }
    }

    private static void ApplyWords(ReadOnlySpan<char> lower, ReadOnlySpan<char> leet, ReadOnlySpan<char> original, Span<double> bits, HashSet<string>.AlternateLookup<ReadOnlySpan<char>> common, ref PatternFlags flags)
    {
        var dictionary = Dictionary.Value.GetAlternateLookup<ReadOnlySpan<char>>();
        var dictionaryBits = Math.Log2(Dictionary.Value.Count + CommonPasswords.Value.Count);
        for (var i = 0; i < lower.Length; i++)
        {
            for (var length = Math.Min(14, lower.Length - i); length >= 4; length--)
            {
                var word = lower.Slice(i, length);
                var leetWord = leet.Slice(i, length);
                var isCommon = common.Contains(word) || common.Contains(leetWord);
                var isWord = isCommon || dictionary.Contains(word) || dictionary.Contains(leetWord);
                if (!isWord)
                {
                    continue;
                }

                var block = dictionaryBits + (HasMixedCase(original.Slice(i, length)) ? 1 : 0) + (word.SequenceEqual(leetWord) ? 0 : 1);
                if (Cover(bits, i, length, block))
                {
                    flags.CommonWord = true;
                    i += length - 1;
                }

                break;
            }
        }
    }

    private static bool HasMixedCase(ReadOnlySpan<char> text)
    {
        var upper = 0;
        foreach (var c in text)
        {
            if (char.IsAsciiLetterUpper(c))
            {
                upper++;
            }
        }

        return upper > 0 && !(upper == 1 && char.IsAsciiLetterUpper(text[0]));
    }

    private static string Reversed(string value)
    {
        var chars = value.ToCharArray();
        Array.Reverse(chars);
        return new string(chars);
    }

    private static HashSet<string> LoadCommon()
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        using var stream = typeof(PasswordStrength).Assembly.GetManifestResourceStream("OnlyYours.Core.Analysis.common-passwords.txt");
        if (stream is null)
        {
            return set;
        }

        using var reader = new StreamReader(stream);
        while (reader.ReadLine() is { } line)
        {
            var word = line.Trim().ToLowerInvariant();
            if (word.Length > 0)
            {
                set.Add(word);
            }
        }

        return set;
    }

    private static HashSet<string> LoadDictionary()
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var word in PasswordGenerator.WordList("en").Concat(PasswordGenerator.WordList("it")))
        {
            if (word.Length >= 4)
            {
                set.Add(word);
            }
        }

        return set;
    }
}
