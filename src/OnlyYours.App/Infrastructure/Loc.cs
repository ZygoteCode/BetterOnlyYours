using System.Globalization;
using System.Windows;

namespace OnlyYours.App.Infrastructure;

/// <summary>
/// Every user-facing text lives in the string dictionary <c>Localization/Strings.en.xaml</c>.
/// XAML binds with <c>{DynamicResource Key}</c>; code uses <see cref="T"/> and <see cref="F"/>.
/// Dates and numbers follow British English conventions (day month year, 24-hour clock).
/// </summary>
public static class Loc
{
    private static ResourceDictionary? _current;

    public static CultureInfo Culture { get; } = CultureInfo.GetCultureInfo("en-GB");

    /// <summary>Merges the string dictionary into <paramref name="root"/> (once).</summary>
    public static void Apply(ResourceDictionary root)
    {
        if (_current is not null && root.MergedDictionaries.Contains(_current))
        {
            return;
        }

        var dictionary = new ResourceDictionary { Source = new Uri("pack://application:,,,/OnlyYours;component/Localization/Strings.en.xaml", UriKind.Absolute) };
        root.MergedDictionaries.Add(dictionary);
        _current = dictionary;
    }

    /// <summary>For tests and tools that run without the Application resources.</summary>
    public static void Use(ResourceDictionary dictionary) => _current = dictionary;

    public static string T(string key)
    {
        if (_current is not null && _current.Contains(key) && _current[key] is string direct)
        {
            return direct;
        }

        return Application.Current?.TryFindResource(key) as string ?? key;
    }

    /// <summary>
    /// Formats <paramref name="key"/>. When the first numeric argument is exactly one and a
    /// <c>key.One</c> variant exists, the singular wording is used ("1 day ago", not "1 days ago").
    /// </summary>
    public static string F(string key, params object?[] args)
    {
        if (IsOne(args.FirstOrDefault(IsNumber)) && Has(key + ".One"))
        {
            key += ".One";
        }

        return string.Format(Culture, T(key), args);
    }

    public static bool Has(string key) => _current?.Contains(key) == true || Application.Current?.TryFindResource(key) is string;

    private static bool IsNumber(object? value) => value is int or long or short or byte or uint or ulong or double or float or decimal;

    private static bool IsOne(object? value) => value is not null && IsNumber(value) && Convert.ToDouble(value, CultureInfo.InvariantCulture) == 1;

    /// <summary>"just now", "3 min ago", "1 day ago", "2 months ago"…</summary>
    public static string Ago(DateTimeOffset? when)
    {
        if (when is not { } stamp || stamp == default)
        {
            return T("Time.Never");
        }

        var delta = DateTimeOffset.Now - stamp;
        if (delta < TimeSpan.FromMinutes(1))
        {
            return T("Time.JustNow");
        }

        if (delta < TimeSpan.FromHours(1))
        {
            return F("Time.MinutesAgo", (int)delta.TotalMinutes);
        }

        if (delta < TimeSpan.FromDays(1))
        {
            return F("Time.HoursAgo", (int)delta.TotalHours);
        }

        if (delta < TimeSpan.FromDays(30))
        {
            return F("Time.DaysAgo", (int)delta.TotalDays);
        }

        if (delta < TimeSpan.FromDays(365))
        {
            return F("Time.MonthsAgo", (int)(delta.TotalDays / 30));
        }

        return F("Time.YearsAgo", (int)(delta.TotalDays / 365));
    }

    public static string Date(DateTimeOffset? when) =>
        when is { } stamp && stamp != default ? stamp.LocalDateTime.ToString("d MMM yyyy, HH:mm", Culture) : T("Time.Never");

    public static string ShortDate(DateTimeOffset? when) =>
        when is { } stamp && stamp != default ? stamp.LocalDateTime.ToString("d MMM yyyy", Culture) : "—";

    /// <summary>Human-readable duration for crack-time estimates.</summary>
    public static string Duration(double seconds)
    {
        if (double.IsInfinity(seconds) || seconds > 3.15e7 * 1e9)
        {
            return T("Crack.Eons");
        }

        if (seconds < 1)
        {
            return T("Crack.Instant");
        }

        if (seconds < 60)
        {
            return F("Crack.Seconds", Math.Round(seconds));
        }

        if (seconds < 3600)
        {
            return F("Crack.Minutes", Math.Round(seconds / 60));
        }

        if (seconds < 86400)
        {
            return F("Crack.Hours", Math.Round(seconds / 3600));
        }

        if (seconds < 3.15e7)
        {
            return F("Crack.Days", Math.Round(seconds / 86400));
        }

        var years = seconds / 3.15e7;
        if (years < 1000)
        {
            return F("Crack.Years", Math.Round(years));
        }

        if (years < 1e6)
        {
            return F("Crack.Millennia", Math.Round(years / 1000));
        }

        return T("Crack.Eons");
    }

    public static string Bytes(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => string.Format(Culture, "{0:0.#} KiB", bytes / 1024.0),
        < 1024L * 1024 * 1024 => string.Format(Culture, "{0:0.#} MiB", bytes / (1024.0 * 1024)),
        _ => string.Format(Culture, "{0:0.##} GiB", bytes / (1024.0 * 1024 * 1024)),
    };
}
