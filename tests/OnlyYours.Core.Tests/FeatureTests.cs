using OnlyYours.Core.Analysis;
using OnlyYours.Core.Generators;
using OnlyYours.Core.ImportExport;
using OnlyYours.Core.Search;
using OnlyYours.Core.Settings;
using OnlyYours.Core.Vault;
using OnlyYours.Core.Vault.Model;
using Xunit;

namespace OnlyYours.Core.Tests;

public class PasswordGeneratorTests
{
    [Fact]
    public void CharactersRespectEveryOption()
    {
        var settings = new GeneratorSettings { Length = 64, Lowercase = true, Uppercase = true, Digits = true, Symbols = true, AvoidAmbiguous = true, ExcludeCharacters = "{}[]" };
        for (var i = 0; i < 50; i++)
        {
            using var secret = PasswordGenerator.Generate(settings);
            var text = secret.Value.Span.ToString();
            Assert.Equal(64, text.Length);
            Assert.Contains(text, char.IsLower);
            Assert.Contains(text, char.IsUpper);
            Assert.Contains(text, char.IsDigit);
            Assert.Contains(text, c => !char.IsLetterOrDigit(c));
            Assert.DoesNotContain(text, c => "Il1O0{}[]".Contains(c));
            Assert.True(secret.EntropyBits > 300);
        }
    }

    [Fact]
    public void DigitsOnlyPoolAndEntropy()
    {
        using var secret = PasswordGenerator.Generate(new GeneratorSettings { Length = 20, Lowercase = false, Uppercase = false, Digits = true, Symbols = false });
        Assert.All(secret.Value.Span.ToString(), c => Assert.True(char.IsDigit(c)));
        Assert.InRange(secret.EntropyBits, 66, 67);
    }

    [Theory]
    [InlineData("it")]
    [InlineData("en")]
    public void PassphrasesUseTheWordList(string language)
    {
        var settings = new GeneratorSettings { Mode = GeneratorMode.Passphrase, Words = 6, Separator = ".", CapitalizeWords = true, AppendNumber = true, WordList = language };
        using var secret = PasswordGenerator.Generate(settings);
        var parts = secret.Value.Span.ToString().Split('.');

        // Six words, then the appended two-digit number as its own group.
        Assert.Equal(7, parts.Length);
        Assert.Matches("^[0-9]{2}$", parts[^1]);
        var words = PasswordGenerator.WordList(language).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.True(words.Count > 1000);
        foreach (var part in parts[..^1])
        {
            Assert.True(char.IsUpper(part[0]));
            Assert.Contains(part, words);
        }

        Assert.True(secret.EntropyBits > 60);
    }

    [Fact]
    public void PinsAreNeverTrivial()
    {
        Assert.True(PasswordGenerator.IsTrivialPin("1234"));
        Assert.True(PasswordGenerator.IsTrivialPin("0000"));
        Assert.True(PasswordGenerator.IsTrivialPin("987654"));
        Assert.False(PasswordGenerator.IsTrivialPin("583920"));
        for (var i = 0; i < 200; i++)
        {
            using var pin = PasswordGenerator.Pin(4);
            Assert.Equal(4, pin.Value.Length);
            Assert.False(PasswordGenerator.IsTrivialPin(pin.Value.Span));
        }
    }

    [Fact]
    public void OutputIsUniformlySpread()
    {
        var counts = new int[10];
        var settings = new GeneratorSettings { Length = 100, Lowercase = false, Uppercase = false, Digits = true, Symbols = false };
        for (var i = 0; i < 100; i++)
        {
            using var secret = PasswordGenerator.Generate(settings);
            foreach (var c in secret.Value.Span)
            {
                counts[c - '0']++;
            }
        }

        // 10 000 samples, 1 000 expected per digit: a biased generator falls far outside.
        Assert.All(counts, c => Assert.InRange(c, 850, 1150));
    }
}

public class PasswordStrengthTests
{
    [Theory]
    [InlineData("password", StrengthLevel.VeryWeak)]
    [InlineData("123456", StrengthLevel.VeryWeak)]
    [InlineData("qwertyuiop", StrengthLevel.VeryWeak)]
    [InlineData("correct-horse-battery-staple-orbit", StrengthLevel.Excellent)]
    [InlineData("q7#Lp2!vZ9@mW4$e", StrengthLevel.Excellent)]
    public void RatesTypicalPasswords(string password, StrengthLevel atLeastOrExactly)
    {
        var report = PasswordStrength.Evaluate(password);
        if (atLeastOrExactly == StrengthLevel.VeryWeak)
        {
            Assert.Equal(StrengthLevel.VeryWeak, report.Level);
        }
        else
        {
            Assert.True(report.Level >= atLeastOrExactly - 1, $"{password}: {report.Level} ({report.EntropyBits:0.0} bits)");
        }
    }

    [Fact]
    public void ExplainsWhatIsWrong()
    {
        Assert.Contains(StrengthHint.CommonPassword, PasswordStrength.Evaluate("password").Hints);
        Assert.Contains(StrengthHint.KeyboardPattern, PasswordStrength.Evaluate("Qwertyuiop!x").Hints);
        Assert.Contains(StrengthHint.OnlyDigits, PasswordStrength.Evaluate("80417263").Hints);
        Assert.Contains(StrengthHint.TooShort, PasswordStrength.Evaluate("aB3$x").Hints);
        Assert.Contains(StrengthHint.DateOrYear, PasswordStrength.Evaluate("Giulia1987!").Hints);
        Assert.Equal(StrengthReport.Empty, PasswordStrength.Evaluate(string.Empty));
    }

    [Fact]
    public void LongerRandomPasswordsScoreHigher()
    {
        var short8 = PasswordStrength.Evaluate("k9#Lp2!v");
        var long20 = PasswordStrength.Evaluate("k9#Lp2!vZ9@mW4$eQ1&x");
        Assert.True(long20.EntropyBits > short8.EntropyBits + 30);
        Assert.True(long20.SecondsToCrack(1e12) > short8.SecondsToCrack(1e12));
    }
}

public class FuzzySearchTests
{
    [Fact]
    public void MatchesSubstringsAcronymsAndRejectsNoise()
    {
        Assert.True(FuzzySearch.MatchToken("hub", "GitHub") > 0);
        Assert.True(FuzzySearch.MatchToken("gh", "GitHub") > 0);
        Assert.Equal(-1, FuzzySearch.MatchToken("xyz", "GitHub"));
        Assert.True(FuzzySearch.MatchToken("git", "GitHub") > FuzzySearch.MatchToken("ihb", "GitHub"));
        Assert.Equal(["git", "hub"], FuzzySearch.Tokenize("  git\thub "));
        Assert.Equal(-1, FuzzySearch.Match(["git", "nope"], "GitHub"));
    }

    [Fact]
    public void HighlightPositionsPointAtTheMatch()
    {
        var positions = new List<int>();
        Assert.True(FuzzySearch.Match(["hub"], "GitHub", positions) > 0);
        Assert.Equal([3, 4, 5], positions);
    }

    [Fact]
    public void EntriesRankByTitleThenOtherFields()
    {
        var byTitle = new Entry { Title = "Ledger Ada", Fields = [new Field { Kind = FieldKind.Username, Text = "x" }] };
        var byUser = new Entry { Title = "Mail", Fields = [new Field { Kind = FieldKind.Username, Text = "ledger@ada.example" }] };
        var byTag = new Entry { Title = "Other", Tags = ["ledger"] };
        var none = new Entry { Title = "Nothing" };
        string[] tokens = ["ledger"];
        Assert.True(EntrySearch.Score(byTitle, tokens) > EntrySearch.Score(byUser, tokens));
        Assert.True(EntrySearch.Score(byUser, tokens) > EntrySearch.Score(byTag, tokens));
        Assert.True(EntrySearch.Score(byTag, tokens) > 0);
        Assert.True(EntrySearch.Score(none, tokens) <= 0);
    }
}

public class CsvTests
{
    [Theory]
    [InlineData("name,url,username,password,note", CsvFormat.Chrome)]
    [InlineData("url,username,password,httpRealm,formActionOrigin,guid,timeCreated,timeLastUsed,timePasswordChanged", CsvFormat.Firefox)]
    [InlineData("folder,favorite,type,name,notes,fields,reprompt,login_uri,login_username,login_password,login_totp", CsvFormat.Bitwarden)]
    [InlineData("Title,Url,Username,Password,OTPAuth,Favorite,Archived,Tags,Notes", CsvFormat.OnePassword)]
    [InlineData("\"Group\",\"Title\",\"Username\",\"Password\",\"URL\",\"Notes\",\"TOTP\",\"Icon\",\"Last Modified\",\"Created\"", CsvFormat.KeePass)]
    [InlineData("url,username,password,totp,extra,name,grouping,fav", CsvFormat.LastPass)]
    [InlineData("type,name,url,email,username,password,note,totp,createTime,modifyTime,vault", CsvFormat.ProtonPass)]
    [InlineData("username,username2,username3,title,password,note,url,category,otpSecret", CsvFormat.Dashlane)]
    [InlineData("kind,title,username,password,url,totp,notes,tags,favorite,created,modified", CsvFormat.OnlyYours)]
    [InlineData("titolo,sito web,utente,password", CsvFormat.Generic)]
    public void DetectsTheSourceManager(string header, CsvFormat expected)
    {
        using var document = CsvDocument.Parse(header + "\r\nx,y,z,w\r\n");
        var plan = CsvImporter.Detect(document);
        Assert.Equal(expected, plan.Format);
        Assert.True(plan.IsUsable);
        Assert.Equal(1, plan.DataRows);
    }

    [Fact]
    public void ParsesQuotesNewlinesAndBom()
    {
        var content = "\uFEFFtitle,notes\r\n\"Quoted, with comma\",\"line 1\nline \"\"2\"\"\"\r\nplain,last";
        using var document = CsvDocument.Parse(content);
        Assert.Equal(3, document.Rows.Count);
        Assert.Equal("title", document.Text(document.Rows[0][0]));
        Assert.Equal("Quoted, with comma", document.Text(document.Rows[1][0]));
        using var notes = document.Value(document.Rows[1][1]);
        Assert.Equal("line 1\nline \"2\"", notes.Span.ToString());
        Assert.Equal("last", document.Text(document.Rows[2][1]));
    }

    [Fact]
    public void DetectsSemicolonSeparatedFiles()
    {
        using var document = CsvDocument.Parse("name;url;username;password\r\nA;https://a.it;ada;pw\r\n");
        Assert.Equal(';', document.Delimiter);
        Assert.Equal(4, document.Rows[1].Length);
    }

    [Fact]
    public void WriterQuotesRiskyCellsWithoutAlteringThem()
    {
        using var writer = new CsvWriter();
        writer.Cell("=SUM(A1)");
        writer.Cell("plain");
        writer.Cell("a,b");
        writer.Cell("say \"hi\"");
        writer.EndRow();
        Assert.Equal("\"=SUM(A1)\",plain,\"a,b\",\"say \"\"hi\"\"\"\r\n", writer.Content.ToString());
    }

    [Fact]
    public async Task ImportsBitwardenWithTotpAndSkipsDuplicates()
    {
        using var dir = new TempDir();
        var (session, _) = await T.CreateVault(dir);
        using (session)
        {
            const string csv =
                "folder,favorite,type,name,notes,fields,reprompt,login_uri,login_username,login_password,login_totp\r\n" +
                "Work,1,login,GitHub,\"note, with comma\",,0,https://github.com,ada,\"p@ss,word\",otpauth://totp/GitHub:ada?secret=JBSWY3DPEHPK3PXP\r\n" +
                ",,note,Secret note,just text,,0,,,,\r\n" +
                "Work,1,login,GitHub,dup,,0,https://github.com,ada,\"p@ss,word\",\r\n";
            using var document = CsvDocument.Parse(csv);
            var plan = CsvImporter.Detect(document);
            var entries = CsvImporter.Build(document, plan, session, skipDuplicates: true, out var report);
            session.AddEntries(entries);

            Assert.Equal(CsvFormat.Bitwarden, report.Format);
            Assert.Equal(2, report.Imported);
            Assert.Equal(1, report.Duplicates);
            Assert.Equal(1, report.WithTotp);

            var github = session.Entries.Single(e => e.Title == "GitHub");
            Assert.Equal(EntryKind.Login, github.Kind);
            Assert.True(github.Favorite);
            Assert.Contains("Work", github.Tags);
            Assert.Equal("ada", github.Username);
            Assert.Equal("p@ss,word", T.Reveal(session, github, github.PrimaryPassword!));
            Assert.True(github.HasTotp);
            var note = session.Entries.Single(e => e.Title == "Secret note");
            Assert.Equal(EntryKind.SecureNote, note.Kind);
        }
    }

    [Fact]
    public async Task ExportThenImportRoundTrips()
    {
        using var dir = new TempDir();
        var (session, _) = await T.CreateVault(dir);
        using (session)
        {
            T.AddLogin(session, "=Formula", "@user", "-pw,\"quoted\"\n2nd line", "https://x.it", "notes; here");
            T.AddLogin(session, "Plain", "u", "p");
            var path = dir.File("export.csv");
            CsvExporter.Export(session, session.Entries.ToList(), path);

            using var target = new TempDir();
            var (other, _) = await T.CreateVault(target);
            using (other)
            {
                using var document = CsvDocument.Load(path);
                var plan = CsvImporter.Detect(document);
                Assert.Equal(CsvFormat.OnlyYours, plan.Format);
                other.AddEntries(CsvImporter.Build(document, plan, other, skipDuplicates: false, out _));
                var formula = other.Entries.Single(e => e.Title == "=Formula");
                Assert.Equal("@user", formula.Username);
                Assert.Equal("-pw,\"quoted\"\n2nd line", T.Reveal(other, formula, formula.PrimaryPassword!));
                Assert.Equal("notes; here", T.Reveal(other, formula, formula.FirstField(FieldKind.Notes)!));
            }
        }
    }
}

public class VaultHealthTests
{
    [Fact]
    public async Task FindsWeakReusedOldExpiredAndMissingTotp()
    {
        using var dir = new TempDir();
        var (session, _) = await T.CreateVault(dir);
        using (session)
        {
            var weak = T.AddLogin(session, "Weak", "a", "password");
            var reusedA = T.AddLogin(session, "ReusedA", "a", "k9#Lp2!vZ9@mW4$eQ1&x");
            var reusedB = T.AddLogin(session, "ReusedB", "b", "k9#Lp2!vZ9@mW4$eQ1&x");
            var old = T.AddLogin(session, "Old", "c", "Zq8!rT5#wY2@pL7$mN4%");
            old.PrimaryPassword!.Modified = DateTimeOffset.Now.AddYears(-2);
            var expired = T.AddLogin(session, "Expired", "d", "Gh3^jK8&lM1*nB6(vC9)");
            expired.Expires = DateTimeOffset.Now.AddDays(-1);
            var empty = T.AddLogin(session, "Empty", "e", string.Empty);

            var report = VaultHealth.Analyze(session, session.Entries.ToList(), DateTimeOffset.Now);
            Assert.Contains(weak.Id, report.Weak);
            Assert.Contains(reusedA.Id, report.Reused);
            Assert.Contains(reusedB.Id, report.Reused);
            Assert.Single(report.ReusedGroups);
            Assert.Contains(old.Id, report.Old);
            Assert.Contains(expired.Id, report.Expired);
            Assert.Contains(empty.Id, report.EmptyPassword);
            Assert.Contains(weak.Id, report.MissingTotp);
            Assert.InRange(report.Score, 0, 99);
            Assert.True(report.For(reusedA.Id)!.IsReused);
        }
    }

    [Fact]
    public async Task AHealthyVaultScoresHigh()
    {
        using var dir = new TempDir();
        var (session, _) = await T.CreateVault(dir);
        using (session)
        {
            T.AddLogin(session, "A", "a", "k9#Lp2!vZ9@mW4$eQ1&x-long-and-unique");
            var report = VaultHealth.Analyze(session, session.Entries.ToList(), DateTimeOffset.Now);
            Assert.Empty(report.Weak);
            Assert.Empty(report.Reused);
            Assert.True(report.Score >= 80, $"score {report.Score}");
        }
    }
}

public class SettingsStoreTests
{
    [Fact]
    public void SavesAndLoadsEveryValue()
    {
        using var dir = new TempDir();
        var store = new SettingsStore(dir.File("settings.json"));
        var settings = store.Load();
        Assert.Equal("cinematic", settings.Intro);
        settings.Theme = "light";
        settings.Accent = "jade";
        settings.AutoLockMinutes = 15;
        settings.Generator.Length = 40;
        settings.HighestWriteCounters["abc"] = 42;
        settings.RecentVaults.Add(@"C:\x\vault.onlyyours");
        store.Save(settings);

        var loaded = new SettingsStore(dir.File("settings.json")).Load();
        Assert.Equal("light", loaded.Theme);
        Assert.Equal("jade", loaded.Accent);
        Assert.Equal(15, loaded.AutoLockMinutes);
        Assert.Equal(40, loaded.Generator.Length);
        Assert.Equal(42UL, loaded.HighestWriteCounters["abc"]);
        Assert.Single(loaded.RecentVaults);
    }

    [Fact]
    public void ACorruptFileFallsBackToDefaults()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.File("settings.json"), "{ this is not json");
        var settings = new SettingsStore(dir.File("settings.json")).Load();
        Assert.Equal("dark", settings.Theme);
    }

    [Fact]
    public void UnknownSettingsArePreserved()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.File("settings.json"), "{\"theme\":\"light\",\"futureOption\":{\"a\":1}}");
        var store = new SettingsStore(dir.File("settings.json"));
        var settings = store.Load();
        Assert.Equal("light", settings.Theme);
        store.Save(settings);
        Assert.Contains("futureOption", File.ReadAllText(dir.File("settings.json")));
    }

    [Fact]
    public void OutOfRangeValuesAreClamped()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.File("settings.json"), "{\"autoLockMinutes\":-5,\"clipboardClearSeconds\":99999,\"generator\":{\"length\":100000}}");
        var settings = new SettingsStore(dir.File("settings.json")).Load();
        Assert.True(settings.AutoLockMinutes >= 0);
        Assert.True(settings.ClipboardClearSeconds <= 600);
        Assert.InRange(settings.Generator.Length, GeneratorSettings.MinLength, GeneratorSettings.MaxLength);
    }
}
