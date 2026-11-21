using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using OnlyYours.App.Infrastructure;
using Xunit;

namespace OnlyYours.App.Tests;

public class LocalizationTests
{
    private static ResourceDictionary Strings() =>
        new() { Source = new Uri("pack://application:,,,/OnlyYours;component/Localization/Strings.en.xaml") };

    private static string SourceDir => Path.Combine(Ui.RepoRoot, "src", "OnlyYours.App");

    [Fact]
    public async Task TheDictionaryIsCompleteAndWellFormed()
    {
        var en = await Ui.Run(Strings);
        var keys = en.Keys.Cast<string>().ToHashSet();
        Assert.True(keys.Count > 700);
        foreach (var key in keys)
        {
            var value = (string)en[key];
            Assert.False(string.IsNullOrWhiteSpace(value), key);
            Assert.DoesNotMatch("[àèìòù]", value);
            if (key.EndsWith(".One", StringComparison.Ordinal))
            {
                var plural = key[..^4];
                Assert.True(keys.Contains(plural), $"{key} has no plural form");

                // The singular never needs a placeholder that the plural lacks.
                Assert.True(Placeholders(value).IsSubsetOf(Placeholders((string)en[plural])), key);
            }
        }
    }

    [Fact]
    public async Task EveryKeyUsedByTheUiExists()
    {
        var en = await Ui.Run(Strings);
        var keys = en.Keys.Cast<string>().ToHashSet();
        var missing = new List<string>();
        var tokenPrefixes = new[] { "Brush.", "Color.", "Icon.", "Font.", "Size.", "Radius.", "Pad.", "Theme.", "Button.", "Text.", "Card", "Chip", "Glass", "Input", "List", "Toggle", "Field", "Scroll", "Combo", "Slider", "Tab", "Menu", "Tool", "Segment" };
        foreach (var file in Directory.EnumerateFiles(SourceDir, "*.xaml", SearchOption.AllDirectories).Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")))
        {
            foreach (Match match in Regex.Matches(File.ReadAllText(file), @"DynamicResource\s+([A-Za-z][A-Za-z0-9.]*)"))
            {
                var key = match.Groups[1].Value;
                if (!keys.Contains(key) && !tokenPrefixes.Any(key.StartsWith))
                {
                    missing.Add($"{Path.GetFileName(file)}: {key}");
                }
            }
        }

        foreach (var file in Directory.EnumerateFiles(SourceDir, "*.cs", SearchOption.AllDirectories).Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")))
        {
            foreach (Match match in Regex.Matches(File.ReadAllText(file), @"Loc\.[TF]\(""([^""]+)"""))
            {
                if (!match.Groups[1].Value.EndsWith('.') && !keys.Contains(match.Groups[1].Value))
                {
                    missing.Add($"{Path.GetFileName(file)}: {match.Groups[1].Value}");
                }
            }
        }

        Assert.True(missing.Count == 0, string.Join(Environment.NewLine, missing));
    }

    [Theory]
    [InlineData(typeof(Core.Vault.Model.EntryKind), "Kind.")]
    [InlineData(typeof(Core.Vault.Model.FieldKind), "FieldKind.")]
    [InlineData(typeof(Core.Analysis.StrengthLevel), "Strength.")]
    [InlineData(typeof(Core.Analysis.StrengthHint), "Hint.")]
    [InlineData(typeof(Core.Vault.VaultError), "VaultError.")]
    [InlineData(typeof(Core.Vault.Model.AuditKind), "Audit.")]
    [InlineData(typeof(Core.ImportExport.CsvFormat), "CsvFormat.")]
    public async Task EveryEnumValueIsTranslated(Type enumType, string prefix)
    {
        var en = await Ui.Run(Strings);
        foreach (var name in Enum.GetNames(enumType))
        {
            Assert.True(en.Contains(prefix + name), $"{prefix}{name}");
        }
    }

    [Fact]
    public async Task EnglishWordingSingularsAndFormats()
    {
        await Ui.Run(() =>
        {
            Assert.Equal("Unlock", Loc.T("Lock.Unlock"));
            Assert.Equal("1 day ago", Loc.Ago(DateTimeOffset.Now.AddDays(-1.2)));
            Assert.Equal("3 days ago", Loc.Ago(DateTimeOffset.Now.AddDays(-3)));
            Assert.Equal("1 entry", Loc.F("Status.Entries", 1));
            Assert.Equal("2 entries", Loc.F("Status.Entries", 2));
            Assert.Equal("1 minute", Loc.F("Settings.Minutes", 1));
            Assert.Equal("\"GitHub\" will stay in the trash for 1 day, then it will be deleted forever.", Loc.F("Entry.TrashMessage", "GitHub", 1));
            Assert.Equal("1 second", Loc.Duration(1.2));
            Assert.Equal("1.5 KiB", Loc.Bytes(1536));
            var noon = new DateTime(2026, 11, 21, 12, 0, 0, DateTimeKind.Local);
            Assert.Equal("21 Nov 2026", Loc.ShortDate(new DateTimeOffset(noon)));
        });
    }

    /// <summary>The product is OnlyYours: the working name must not survive anywhere.</summary>
    [Fact]
    public void NoTraceOfTheOldName()
    {
        var oldName = "Sig" + "illo";
        var skipped = new[] { $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", $"{Path.DirectorySeparatorChar}build{Path.DirectorySeparatorChar}", ".dart_tool", "Snapshots", "Fixtures" };
        var binary = new[] { ".png", ".ico", ".plf", ".bak", ".onlyyours", ".oypkg", ".dll", ".exe", ".pdb" };
        var files = new[] { "src", "tests", "tools", "docs" }
            .Select(folder => Path.Combine(Ui.RepoRoot, folder))
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
            .Concat(Directory.EnumerateFiles(Ui.RepoRoot))
            .Where(f => !skipped.Any(f.Contains) && !binary.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .Where(f => !f.EndsWith($"Words{Path.DirectorySeparatorChar}it.txt", StringComparison.Ordinal)); // a genuine Italian word
        var offenders = files.Where(f => File.ReadAllText(f).Contains(oldName, StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    private static HashSet<string> Placeholders(string text) => Regex.Matches(text, @"\{(\d)\}").Select(m => m.Groups[1].Value).ToHashSet();
}

public class ThemeTests
{
    public static TheoryData<bool, string> Variants()
    {
        var data = new TheoryData<bool, string>();
        foreach (var dark in new[] { true, false })
        {
            foreach (var accent in new[] { "aurum", "copper", "platinum", "jade" })
            {
                data.Add(dark, accent);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public async Task TextIsReadableInEveryThemeAndAccent(bool dark, string accent)
    {
        await Ui.Run(() =>
        {
            var d = ThemeManager.Build(dark, accent);
            Color C(string name) => (Color)d["Color." + name];

            foreach (var surface in new[] { "Window", "Surface", "SurfaceRaised", "Input" })
            {
                Assert.True(ThemeManager.Contrast(C("Text"), C(surface)) >= 7, $"Text on {surface}");
                Assert.True(ThemeManager.Contrast(C("TextMuted"), C(surface)) >= 4.5, $"TextMuted on {surface}: {ThemeManager.Contrast(C("TextMuted"), C(surface)):0.00}");
            }

            Assert.True(ThemeManager.Contrast(C("TextFaint"), C("Window")) >= 3, "TextFaint");
            Assert.True(ThemeManager.Contrast(C("TextOnAccent"), C("Accent")) >= 4.5, $"TextOnAccent: {ThemeManager.Contrast(C("TextOnAccent"), C("Accent")):0.00}");
            Assert.True(ThemeManager.Contrast(C("TextAccent"), C("Window")) >= 4.5, $"TextAccent: {ThemeManager.Contrast(C("TextAccent"), C("Window")):0.00}");
            foreach (var tone in new[] { "Danger", "Success", "Warning", "Info" })
            {
                Assert.True(ThemeManager.Contrast(C(tone), C("Window")) >= 3, tone);
            }
        });
    }

    [Fact]
    public async Task ApplyingAThemeSwapsBrushesLive()
    {
        await Ui.Run(() =>
        {
            Ui.Theme("light", "jade");
            var light = (SolidColorBrush)Application.Current.Resources["Brush.Window"];
            Ui.Theme("dark", "aurum");
            var dark = (SolidColorBrush)Application.Current.Resources["Brush.Window"];
            Assert.NotEqual(light.Color, dark.Color);
            Assert.True(ThemeManager.IsDark);
            Assert.Equal("aurum", ThemeManager.Accent);
        });
    }

    [Fact]
    public void ContrastFormulaMatchesWcag()
    {
        Assert.Equal(21, ThemeManager.Contrast(Colors.Black, Colors.White), 1);
        Assert.Equal(1, ThemeManager.Contrast(Colors.Gray, Colors.Gray), 3);
    }
}

public class FormatStringTests
{
    [Fact]
    public async Task EveryFormattedKeyHasPlaceholders()
    {
        var en = await Ui.Run(() => new ResourceDictionary { Source = new Uri("pack://application:,,,/OnlyYours;component/Localization/Strings.en.xaml") });
        var source = Path.Combine(Ui.RepoRoot, "src", "OnlyYours.App");
        var wrong = new List<string>();
        foreach (var file in Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories).Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")))
        {
            foreach (Match match in Regex.Matches(File.ReadAllText(file), @"Loc\.F\(""([^""]+)"""))
            {
                var key = match.Groups[1].Value;
                if (en.Contains(key) && !((string)en[key]).Contains("{0}", StringComparison.Ordinal))
                {
                    wrong.Add($"{Path.GetFileName(file)}: {key}");
                }
            }
        }

        Assert.True(wrong.Count == 0, string.Join(Environment.NewLine, wrong));
    }
}
