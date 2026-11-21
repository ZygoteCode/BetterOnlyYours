using OnlyYours.Core.Vault.Model;

namespace OnlyYours.Core.Search;

/// <summary>
/// Small, allocation-light fuzzy matcher. Case-insensitive, order-preserving,
/// rewarding prefixes and word boundaries so that "gh" finds "GitHub" before
/// "Flight Hub", and substring hits always beat scattered ones.
/// </summary>
public static class FuzzySearch
{
    public static string[] Tokenize(string query) =>
        query.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>Scores one token against <paramref name="text"/>; returns -1 when it does not match.</summary>
    public static int MatchToken(ReadOnlySpan<char> token, ReadOnlySpan<char> text, List<int>? positions = null)
    {
        if (token.IsEmpty)
        {
            return 0;
        }

        if (text.IsEmpty)
        {
            return -1;
        }

        var direct = text.IndexOf(token, StringComparison.OrdinalIgnoreCase);
        if (direct >= 0)
        {
            var score = 120 - Math.Min(direct, 40);
            if (direct == 0)
            {
                score += 60;
            }
            else if (IsBoundary(text[direct - 1]))
            {
                score += 30;
            }

            if (token.Length == text.Length)
            {
                score += 40;
            }

            if (positions is not null)
            {
                for (var i = 0; i < token.Length; i++)
                {
                    positions.Add(direct + i);
                }
            }

            return score + (token.Length * 3);
        }

        var total = 0;
        var textIndex = 0;
        var previous = -2;
        var start = positions?.Count ?? 0;
        foreach (var needle in token)
        {
            var lowerNeedle = char.ToLowerInvariant(needle);
            var found = -1;
            while (textIndex < text.Length)
            {
                if (char.ToLowerInvariant(text[textIndex]) == lowerNeedle)
                {
                    found = textIndex;
                    break;
                }

                textIndex++;
            }

            if (found < 0)
            {
                if (positions is not null)
                {
                    positions.RemoveRange(start, positions.Count - start);
                }

                return -1;
            }

            total += 10;
            if (found == previous + 1)
            {
                total += 12;
            }

            if (found == 0)
            {
                total += 20;
            }
            else if (IsBoundary(text[found - 1]))
            {
                total += 16;
            }

            total -= Math.Clamp(found - previous - 1, 0, 6);
            positions?.Add(found);
            previous = found;
            textIndex++;
        }

        return total;
    }

    /// <summary>All tokens must match; returns the summed score or -1.</summary>
    public static int Match(ReadOnlySpan<string> tokens, string text, List<int>? positions = null)
    {
        var total = 0;
        foreach (var token in tokens)
        {
            var score = MatchToken(token, text, positions);
            if (score < 0)
            {
                return -1;
            }

            total += score;
        }

        positions?.Sort();
        return total;
    }

    private static bool IsBoundary(char c) => c is ' ' or '-' or '_' or '.' or '/' or ':' or '@' or '(' or '[';
}

/// <summary>Entry-level search over every non-secret field, weighted by importance.</summary>
public static class EntrySearch
{
    public static int Score(Entry entry, ReadOnlySpan<string> tokens)
    {
        if (tokens.IsEmpty)
        {
            return 0;
        }

        var total = 0;
        foreach (var token in tokens)
        {
            var best = Weighted(FuzzySearch.MatchToken(token, entry.Title), 3);
            if (entry.Username is { } user)
            {
                best = Math.Max(best, Weighted(FuzzySearch.MatchToken(token, user), 2));
            }

            if (entry.Host is { } host)
            {
                best = Math.Max(best, Weighted(FuzzySearch.MatchToken(token, host), 2));
            }

            foreach (var tag in entry.Tags)
            {
                best = Math.Max(best, Weighted(FuzzySearch.MatchToken(token, tag), 1.5));
            }

            foreach (var field in entry.Fields)
            {
                if (field.Text is { Length: > 0 } text && !field.IsSensitive)
                {
                    best = Math.Max(best, Weighted(FuzzySearch.MatchToken(token, text), 1));
                }

                if (field.Label is { Length: > 0 } label)
                {
                    best = Math.Max(best, Weighted(FuzzySearch.MatchToken(token, label), 0.5));
                }
            }

            if (best < 0)
            {
                return -1;
            }

            total += best;
        }

        return total + (entry.Favorite ? 5 : 0) + Math.Min(entry.UseCount, 20);
    }

    private static int Weighted(int score, double weight) => score < 0 ? -1 : (int)(score * weight);
}
