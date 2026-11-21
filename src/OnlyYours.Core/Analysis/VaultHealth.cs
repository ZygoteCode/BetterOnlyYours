using OnlyYours.Core.Vault;
using OnlyYours.Core.Vault.Model;

namespace OnlyYours.Core.Analysis;

public sealed record EntryHealth(
    Guid EntryId,
    StrengthLevel Level,
    double EntropyBits,
    int ReuseCount,
    TimeSpan Age,
    bool Expired,
    bool ExpiringSoon,
    bool MissingTotp)
{
    public bool IsWeak => Level is StrengthLevel.VeryWeak or StrengthLevel.Weak;

    public bool IsReused => ReuseCount > 1;

    public bool IsOld => Age > VaultHealth.OldAfter;
}

public sealed class HealthReport
{
    public static HealthReport Empty { get; } = new();

    public int Score { get; init; } = 100;

    public int Analyzed { get; init; }

    public int LoginCount { get; init; }

    public IReadOnlyDictionary<Guid, EntryHealth> PerEntry { get; init; } = new Dictionary<Guid, EntryHealth>();

    public IReadOnlyList<IReadOnlyList<Guid>> ReusedGroups { get; init; } = [];

    public IReadOnlyList<Guid> Weak { get; init; } = [];

    public IReadOnlyList<Guid> Reused { get; init; } = [];

    public IReadOnlyList<Guid> Old { get; init; } = [];

    public IReadOnlyList<Guid> Expired { get; init; } = [];

    public IReadOnlyList<Guid> ExpiringSoon { get; init; } = [];

    public IReadOnlyList<Guid> MissingTotp { get; init; } = [];

    public IReadOnlyList<Guid> EmptyPassword { get; init; } = [];

    public int IssueCount => Weak.Count + Reused.Count + Expired.Count + EmptyPassword.Count;

    public EntryHealth? For(Guid id) => PerEntry.TryGetValue(id, out var health) ? health : null;
}

/// <summary>
/// Password hygiene across the vault. Each password is unsealed in turn into locked
/// memory, rated, fingerprinted with a keyed HMAC (so reuse is found without keeping
/// any plaintext) and wiped before the next one is opened.
/// </summary>
public static class VaultHealth
{
    public static readonly TimeSpan OldAfter = TimeSpan.FromDays(365);
    public static readonly TimeSpan ExpiringWithin = TimeSpan.FromDays(30);

    public static HealthReport Analyze(VaultSession session, IReadOnlyList<Entry> entries, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var perEntry = new Dictionary<Guid, EntryHealth>();
        var byDigest = new Dictionary<string, List<Guid>>(StringComparer.Ordinal);
        var digests = new Dictionary<Guid, string>();
        var levels = new Dictionary<Guid, StrengthReport>();
        var empty = new List<Guid>();
        var expired = new List<Guid>();
        var expiring = new List<Guid>();
        var missingTotp = new List<Guid>();
        var logins = 0;

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry.Expires is { } expires)
            {
                if (expires <= now)
                {
                    expired.Add(entry.Id);
                }
                else if (expires - now <= ExpiringWithin)
                {
                    expiring.Add(entry.Id);
                }
            }

            var fields = entry.Fields;
            var password = fields.FirstOrDefault(f => f.Kind == FieldKind.Password) ?? fields.FirstOrDefault(f => f.Kind is FieldKind.Passphrase or FieldKind.ApiSecret);
            if (entry.Kind == EntryKind.Login)
            {
                logins++;
                if (!fields.Any(f => f.Kind == FieldKind.Totp && f.Sealed is not null))
                {
                    missingTotp.Add(entry.Id);
                }
            }

            if (password?.Sealed is null)
            {
                if (entry.Kind == EntryKind.Login)
                {
                    empty.Add(entry.Id);
                }

                continue;
            }

            if (!session.TryReveal(entry, password, out var plain))
            {
                continue;
            }

            using (plain)
            {
                if (plain!.Length == 0)
                {
                    empty.Add(entry.Id);
                    continue;
                }

                var report = PasswordStrength.Evaluate(plain.Span);
                var digest = Convert.ToHexString(session.IndexDigest(plain.Span));
                levels[entry.Id] = report;
                digests[entry.Id] = digest;
                if (!byDigest.TryGetValue(digest, out var group))
                {
                    byDigest[digest] = group = [];
                }

                group.Add(entry.Id);
            }
        }

        var reusedGroups = byDigest.Values.Where(g => g.Count > 1).Select(g => (IReadOnlyList<Guid>)g).ToList();
        var weak = new List<Guid>();
        var old = new List<Guid>();
        foreach (var entry in entries)
        {
            if (!levels.TryGetValue(entry.Id, out var report))
            {
                continue;
            }

            var password = entry.Fields.FirstOrDefault(f => f.Kind == FieldKind.Password) ?? entry.Fields.FirstOrDefault(f => f.Kind is FieldKind.Passphrase or FieldKind.ApiSecret);
            var modified = password?.Modified ?? entry.Modified;
            var age = modified == default ? TimeSpan.Zero : now - modified;
            var reuse = byDigest[digests[entry.Id]].Count;
            var health = new EntryHealth(
                entry.Id,
                report.Level,
                report.EntropyBits,
                reuse,
                age,
                expired.Contains(entry.Id),
                expiring.Contains(entry.Id),
                missingTotp.Contains(entry.Id));
            perEntry[entry.Id] = health;
            if (health.IsWeak)
            {
                weak.Add(entry.Id);
            }

            if (health.IsOld)
            {
                old.Add(entry.Id);
            }
        }

        var reused = reusedGroups.SelectMany(g => g).ToList();
        var analyzed = levels.Count;
        var score = 100.0;
        if (analyzed > 0)
        {
            score -= 45.0 * weak.Count / analyzed;
            score -= 35.0 * reused.Count / analyzed;
            score -= 10.0 * old.Count / analyzed;
        }

        var total = Math.Max(1, entries.Count);
        score -= 10.0 * (expired.Count + empty.Count) / total;

        return new HealthReport
        {
            Score = (int)Math.Round(Math.Clamp(score, 0, 100)),
            Analyzed = analyzed,
            LoginCount = logins,
            PerEntry = perEntry,
            ReusedGroups = reusedGroups,
            Weak = weak,
            Reused = reused,
            Old = old,
            Expired = expired,
            ExpiringSoon = expiring,
            MissingTotp = missingTotp,
            EmptyPassword = empty,
        };
    }
}
