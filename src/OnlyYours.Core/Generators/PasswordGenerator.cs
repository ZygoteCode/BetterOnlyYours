using System.Reflection;
using System.Security.Cryptography;
using OnlyYours.Core.Security.Memory;
using OnlyYours.Core.Settings;

namespace OnlyYours.Core.Generators;

/// <summary>A freshly generated secret, already in secure memory, with the entropy of its generation process.</summary>
public sealed class GeneratedSecret(SecureCharBuffer value, double entropyBits) : IDisposable
{
    public SecureCharBuffer Value { get; } = value;

    /// <summary>log2 of the number of equally likely outcomes of the generator — not a guess-resistance estimate.</summary>
    public double EntropyBits { get; } = entropyBits;

    public void Dispose() => Value.Dispose();
}

/// <summary>
/// Cryptographically secure generator (<see cref="RandomNumberGenerator.GetInt32(int)"/> is unbiased)
/// for random characters, passphrases and PINs. Output is written character by
/// character into a <see cref="SecureCharBuffer"/>.
/// </summary>
public static class PasswordGenerator
{
    public const string Lowercase = "abcdefghijklmnopqrstuvwxyz";
    public const string Uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    public const string Digits = "0123456789";
    public const string Symbols = "!@#$%^&*()-_=+[]{};:,.?/~";
    public const string Ambiguous = "Il1O0oB8S5Z2|`'\"";

    private static readonly Lazy<string[]> English = new(() => LoadWords("en"));
    private static readonly Lazy<string[]> Italian = new(() => LoadWords("it"));

    public static IReadOnlyList<string> WordList(string language) => language == "en" ? English.Value : Italian.Value;

    public static GeneratedSecret Generate(GeneratorSettings settings) => settings.Mode switch
    {
        GeneratorMode.Passphrase => Passphrase(settings),
        GeneratorMode.Pin => Pin(settings.PinLength),
        _ => Characters(settings),
    };

    public static GeneratedSecret Characters(GeneratorSettings settings)
    {
        var classes = new List<string>(4);
        void AddClass(bool enabled, string set)
        {
            if (!enabled)
            {
                return;
            }

            var filtered = Filter(set, settings);
            if (filtered.Length > 0)
            {
                classes.Add(filtered);
            }
        }

        AddClass(settings.Lowercase, Lowercase);
        AddClass(settings.Uppercase, Uppercase);
        AddClass(settings.Digits, Digits);
        AddClass(settings.Symbols, Symbols);
        if (classes.Count == 0)
        {
            // Never hand back an empty or trivially weak secret.
            classes.Add(Filter(Lowercase + Uppercase + Digits, settings) is { Length: > 0 } fallback ? fallback : Lowercase + Uppercase + Digits);
        }

        var pool = string.Concat(classes);
        var length = Math.Clamp(settings.Length, GeneratorSettings.MinLength, GeneratorSettings.MaxLength);
        var output = new SecureCharBuffer(length);

        // One character from every enabled class, then fill from the whole pool, then shuffle.
        foreach (var set in classes)
        {
            if (output.Length >= length)
            {
                break;
            }

            output.Append(set[RandomNumberGenerator.GetInt32(set.Length)]);
        }

        while (output.Length < length)
        {
            output.Append(pool[RandomNumberGenerator.GetInt32(pool.Length)]);
        }

        Shuffle(output.WritableSpan);
        return new GeneratedSecret(output, length * Math.Log2(pool.Length));
    }

    public static GeneratedSecret Passphrase(GeneratorSettings settings)
    {
        var words = WordList(settings.WordList);
        var count = Math.Clamp(settings.Words, GeneratorSettings.MinWords, GeneratorSettings.MaxWords);
        var separator = string.IsNullOrEmpty(settings.Separator) ? "-" : settings.Separator;
        var output = new SecureCharBuffer(count * 10);
        for (var i = 0; i < count; i++)
        {
            if (i > 0)
            {
                output.Append(separator);
            }

            var word = words[RandomNumberGenerator.GetInt32(words.Count)];
            if (settings.CapitalizeWords)
            {
                output.Append(char.ToUpperInvariant(word[0]));
                output.Append(word.AsSpan(1));
            }
            else
            {
                output.Append(word);
            }
        }

        var entropy = count * Math.Log2(words.Count);
        if (settings.AppendNumber)
        {
            output.Append(separator);
            output.Append((char)('0' + RandomNumberGenerator.GetInt32(10)));
            output.Append((char)('0' + RandomNumberGenerator.GetInt32(10)));
            entropy += Math.Log2(100);
        }

        return new GeneratedSecret(output, entropy);
    }

    public static GeneratedSecret Pin(int length)
    {
        length = Math.Clamp(length, GeneratorSettings.MinPin, GeneratorSettings.MaxPin);
        var output = new SecureCharBuffer(length);
        while (true)
        {
            output.Clear();
            for (var i = 0; i < length; i++)
            {
                output.Append((char)('0' + RandomNumberGenerator.GetInt32(10)));
            }

            // Reject the handful of PINs everybody tries first (all equal, straight runs).
            if (!IsTrivialPin(output.Span))
            {
                break;
            }
        }

        return new GeneratedSecret(output, length * Math.Log2(10));
    }

    public static bool IsTrivialPin(ReadOnlySpan<char> pin)
    {
        if (pin.Length < 2)
        {
            return false;
        }

        var allSame = true;
        var ascending = true;
        var descending = true;
        for (var i = 1; i < pin.Length; i++)
        {
            allSame &= pin[i] == pin[0];
            ascending &= pin[i] - pin[i - 1] == 1;
            descending &= pin[i - 1] - pin[i] == 1;
        }

        return allSame || ascending || descending;
    }

    private static string Filter(string set, GeneratorSettings settings)
    {
        if (!settings.AvoidAmbiguous && string.IsNullOrEmpty(settings.ExcludeCharacters))
        {
            return set;
        }

        return string.Concat(set.Where(c =>
            !(settings.AvoidAmbiguous && Ambiguous.Contains(c, StringComparison.Ordinal))
            && !(settings.ExcludeCharacters?.Contains(c, StringComparison.Ordinal) ?? false)));
    }

    private static void Shuffle(Span<char> chars)
    {
        for (var i = chars.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
    }

    private static string[] LoadWords(string language)
    {
        using var stream = typeof(PasswordGenerator).Assembly.GetManifestResourceStream($"OnlyYours.Core.Generators.Words.{language}.txt")
            ?? throw new InvalidOperationException($"Word list '{language}' is missing.");
        using var reader = new StreamReader(stream);
        var words = new HashSet<string>(StringComparer.Ordinal);
        while (reader.ReadLine() is { } line)
        {
            var word = line.Trim().ToLowerInvariant();
            if (word.Length is >= 3 and <= 12 && word.All(char.IsAsciiLetterLower))
            {
                words.Add(word);
            }
        }

        return [.. words.Order(StringComparer.Ordinal)];
    }
}
