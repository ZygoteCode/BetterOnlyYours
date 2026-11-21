using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace OnlyYours.App.Infrastructure;

/// <summary>
/// The OnlyYours palette. Two grounds — <b>Obsidian</b> (dark, the signature look) and
/// <b>Ivory</b> (light) — and four metal accents: <b>Gold</b> (the brand), Copper,
/// Platinum and Jade. Every colour is exposed both as a <see cref="Color"/>
/// (<c>Color.*</c>) and as a frozen brush (<c>Brush.*</c>); views only use
/// <c>DynamicResource</c>, so switching theme or accent repaints everything live.
/// </summary>
public static class ThemeManager
{
    private static ResourceDictionary? _current;

    public static event EventHandler? ThemeChanged;

    public static bool IsDark { get; private set; } = true;

    public static string Accent { get; private set; } = "aurum";

    public static bool ResolveDark(string setting)
    {
        if (setting == "light")
        {
            return false;
        }

        if (setting == "dark")
        {
            return true;
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is not int light || light == 0;
        }
        catch
        {
            return true;
        }
    }

    public static void Apply(ResourceDictionary root, string theme, string accent)
    {
        var dictionary = Build(ResolveDark(theme), accent);
        if (_current is not null)
        {
            var index = root.MergedDictionaries.IndexOf(_current);
            if (index >= 0)
            {
                root.MergedDictionaries[index] = dictionary;
            }
            else
            {
                root.MergedDictionaries.Insert(0, dictionary);
            }
        }
        else
        {
            root.MergedDictionaries.Insert(0, dictionary);
        }

        _current = dictionary;
        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }

    public static ResourceDictionary Build(bool dark, string accent)
    {
        IsDark = dark;
        Accent = accent;
        var d = new ResourceDictionary();
        var p = dark ? DarkPalette() : LightPalette();
        var a = AccentPalette(accent, dark);

        foreach (var (key, color) in p)
        {
            Add(d, key, color);
        }

        Add(d, "Accent", a.Main);
        Add(d, "AccentBright", a.Bright);
        Add(d, "AccentDeep", a.Deep);
        Add(d, "AccentSoft", WithAlpha(a.Main, dark ? 0x22 : 0x26));
        Add(d, "AccentSoftHover", WithAlpha(a.Main, dark ? 0x33 : 0x38));
        Add(d, "AccentGlow", WithAlpha(a.Main, 0x5C));
        Add(d, "AccentLine", WithAlpha(a.Main, dark ? 0x66 : 0x88));
        Add(d, "Selection", WithAlpha(a.Main, dark ? 0x55 : 0x45));
        // Ink or white, whichever reads better on this metal (gold and jade need ink even on ivory).
        var ink = Color.FromRgb(0x0F, 0x0A, 0x04);
        Add(d, "TextOnAccent", Contrast(ink, a.Main) >= Contrast(Colors.White, a.Main) ? ink : Colors.White);
        Add(d, "TextAccent", dark ? a.Bright : a.Deep);

        var gradient = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1),
            GradientStops =
            {
                new GradientStop(dark ? a.Bright : Lighten(a.Main, 0.18), 0),
                new GradientStop(a.Main, 0.5),
                new GradientStop(a.Deep, 1),
            },
        };
        gradient.Freeze();
        d["Brush.AccentGradient"] = gradient;

        var gradientHover = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1),
            GradientStops =
            {
                new GradientStop(Lighten(a.Bright, 0.1), 0),
                new GradientStop(Lighten(a.Main, 0.08), 0.5),
                new GradientStop(a.Main, 1),
            },
        };
        gradientHover.Freeze();
        d["Brush.AccentGradientHover"] = gradientHover;

        var metal = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1),
            GradientStops =
            {
                new GradientStop(Lighten(a.Bright, 0.25), 0),
                new GradientStop(a.Bright, 0.4),
                new GradientStop(a.Main, 0.7),
                new GradientStop(a.Deep, 1),
            },
        };
        metal.Freeze();
        d["Brush.Metal"] = metal;

        d["Theme.IsDark"] = dark;
        return d;
    }

    private static List<(string, Color)> DarkPalette() =>
    [
        ("Window", Rgb(0x0B, 0x0A, 0x09)),
        ("Rail", Rgb(0x10, 0x0E, 0x0C)),
        ("Surface", Rgb(0x16, 0x13, 0x10)),
        ("SurfaceRaised", Rgb(0x1C, 0x18, 0x14)),
        ("SurfaceHover", Rgb(0x22, 0x1E, 0x19)),
        ("SurfacePressed", Rgb(0x2A, 0x25, 0x1F)),
        ("Input", Rgb(0x13, 0x11, 0x0E)),
        ("InputHover", Rgb(0x18, 0x15, 0x12)),
        ("Line", Rgb(0x29, 0x24, 0x1E)),
        ("LineStrong", Rgb(0x3A, 0x33, 0x2A)),
        ("Text", Rgb(0xF3, 0xEC, 0xE0)),
        ("TextMuted", Rgb(0xB5, 0xAB, 0x9C)),
        ("TextFaint", Rgb(0x84, 0x7A, 0x6D)),
        ("TextDisabled", Rgb(0x58, 0x50, 0x47)),
        ("Danger", Rgb(0xE8, 0x6A, 0x50)),
        ("DangerSoft", Rgb(0x2E, 0x17, 0x12)),
        ("Success", Rgb(0x66, 0xC9, 0x9C)),
        ("SuccessSoft", Rgb(0x12, 0x26, 0x1B)),
        ("Warning", Rgb(0xF2, 0xA8, 0x45)),
        ("WarningSoft", Rgb(0x2B, 0x1F, 0x0F)),
        ("Info", Rgb(0x7A, 0xAE, 0xE2)),
        ("InfoSoft", Rgb(0x12, 0x1D, 0x2A)),
        ("Scrim", Color.FromArgb(0xB8, 0x05, 0x04, 0x03)),
        ("Shadow", Color.FromArgb(0xFF, 0x00, 0x00, 0x00)),
        ("Highlight", Color.FromArgb(0x0E, 0xFF, 0xF4, 0xE0)),
    ];

    private static List<(string, Color)> LightPalette() =>
    [
        ("Window", Rgb(0xF5, 0xF0, 0xE7)),
        ("Rail", Rgb(0xEE, 0xE7, 0xDB)),
        ("Surface", Rgb(0xFB, 0xF8, 0xF2)),
        ("SurfaceRaised", Rgb(0xFF, 0xFD, 0xF9)),
        ("SurfaceHover", Rgb(0xF0, 0xEA, 0xDF)),
        ("SurfacePressed", Rgb(0xE6, 0xDD, 0xCE)),
        ("Input", Rgb(0xFF, 0xFD, 0xF9)),
        ("InputHover", Rgb(0xFA, 0xF6, 0xEF)),
        ("Line", Rgb(0xE0, 0xD6, 0xC5)),
        ("LineStrong", Rgb(0xC9, 0xBB, 0xA4)),
        ("Text", Rgb(0x1E, 0x19, 0x13)),
        ("TextMuted", Rgb(0x5A, 0x50, 0x44)),
        ("TextFaint", Rgb(0x86, 0x7A, 0x6A)),
        ("TextDisabled", Rgb(0xB3, 0xA8, 0x98)),
        ("Danger", Rgb(0xC0, 0x44, 0x2C)),
        ("DangerSoft", Rgb(0xF8, 0xE1, 0xDA)),
        ("Success", Rgb(0x2C, 0x8A, 0x5E)),
        ("SuccessSoft", Rgb(0xDF, 0xF1, 0xE6)),
        ("Warning", Rgb(0xB0, 0x6C, 0x12)),
        ("WarningSoft", Rgb(0xF8, 0xEB, 0xD3)),
        ("Info", Rgb(0x2C, 0x68, 0xA8)),
        ("InfoSoft", Rgb(0xDE, 0xEA, 0xF6)),
        ("Scrim", Color.FromArgb(0x8C, 0x2A, 0x22, 0x18)),
        ("Shadow", Color.FromArgb(0xFF, 0x3A, 0x2C, 0x18)),
        ("Highlight", Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF)),
    ];

    private readonly record struct AccentSet(Color Main, Color Bright, Color Deep);

    private static AccentSet AccentPalette(string accent, bool dark) => (accent, dark) switch
    {
        ("copper", true) => new(Rgb(0xDE, 0x8E, 0x5E), Rgb(0xF5, 0xB8, 0x92), Rgb(0xA4, 0x58, 0x2F)),
        ("copper", false) => new(Rgb(0xB0, 0x5E, 0x30), Rgb(0xCC, 0x7A, 0x48), Rgb(0x7E, 0x3E, 0x18)),
        ("platinum", true) => new(Rgb(0xCB, 0xD0, 0xD8), Rgb(0xF1, 0xF3, 0xF6), Rgb(0x8A, 0x91, 0x9C)),
        ("platinum", false) => new(Rgb(0x5E, 0x67, 0x74), Rgb(0x7C, 0x86, 0x94), Rgb(0x3E, 0x45, 0x50)),
        ("jade", true) => new(Rgb(0x5F, 0xC2, 0xA0), Rgb(0x97, 0xE4, 0xC8), Rgb(0x2D, 0x89, 0x6C)),
        ("jade", false) => new(Rgb(0x1F, 0x7F, 0x60), Rgb(0x2F, 0x9A, 0x79), Rgb(0x16, 0x5E, 0x47)),
        (_, true) => new(Rgb(0xE3, 0xB2, 0x61), Rgb(0xF8, 0xD9, 0x8F), Rgb(0xB0, 0x80, 0x36)),
        (_, false) => new(Rgb(0xA4, 0x72, 0x28), Rgb(0xC4, 0x8D, 0x3B), Rgb(0x78, 0x50, 0x16)),
    };

    private static void Add(ResourceDictionary dictionary, string name, Color color)
    {
        dictionary["Color." + name] = color;
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        dictionary["Brush." + name] = brush;
    }

    private static Color Rgb(byte r, byte g, byte b) => Color.FromRgb(r, g, b);

    public static Color WithAlpha(Color color, int alpha) => Color.FromArgb((byte)alpha, color.R, color.G, color.B);

    public static Color Lighten(Color color, double amount) => Color.FromArgb(
        color.A,
        (byte)Math.Min(255, color.R + ((255 - color.R) * amount)),
        (byte)Math.Min(255, color.G + ((255 - color.G) * amount)),
        (byte)Math.Min(255, color.B + ((255 - color.B) * amount)));

    /// <summary>WCAG relative luminance contrast ratio (used by the theme tests).</summary>
    public static double Contrast(Color a, Color b)
    {
        static double Channel(byte value)
        {
            var c = value / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        static double Luminance(Color c) => (0.2126 * Channel(c.R)) + (0.7152 * Channel(c.G)) + (0.0722 * Channel(c.B));
        var la = Luminance(a);
        var lb = Luminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }
}
