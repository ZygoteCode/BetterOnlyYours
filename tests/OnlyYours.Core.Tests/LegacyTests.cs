using System.Text.Json;
using OnlyYours.Core.Legacy;
using OnlyYours.Core.Otp;
using OnlyYours.Core.Settings;
using OnlyYours.Core.Vault;
using OnlyYours.Core.Vault.Model;
using Xunit;

namespace OnlyYours.Core.Tests;

/// <summary>
/// Migration from BetterOnlyYours, verified against files written by the legacy app's own
/// code (tools/legacy-fixtures) — every format version, KDF, TOTP box and edge case.
/// </summary>
public class LegacyMigrationTests
{
    private static readonly JsonElement Manifest = JsonDocument.Parse(File.ReadAllText(T.FixturePath("Legacy", "manifest.json"))).RootElement;

    public static TheoryData<string> Vaults()
    {
        var data = new TheoryData<string>();
        foreach (var vault in Manifest.GetProperty("vaults").EnumerateArray())
        {
            data.Add(vault.GetProperty("file").GetString()!);
        }

        return data;
    }

    private static JsonElement Fixture(string file) =>
        Manifest.GetProperty("vaults").EnumerateArray().First(v => v.GetProperty("file").GetString() == file);

    private static string PathOf(string file) => T.FixturePath(["Legacy", .. file.Split('/')]);

    [Theory]
    [MemberData(nameof(Vaults))]
    public void HeaderIsRecognized(string file)
    {
        var fixture = Fixture(file);
        var header = LegacyVault.TryReadHeader(PathOf(file));
        if (fixture.TryGetProperty("recoveredFromBackup", out _))
        {
            Assert.Null(header); // the main file is deliberately truncated
            return;
        }

        Assert.NotNull(header);
        Assert.Equal(fixture.GetProperty("version").GetInt32(), header!.Version);
        Assert.Equal(fixture.GetProperty("kdf").GetString() == "argon2id" ? LegacyKdf.Argon2id : LegacyKdf.Pbkdf2Sha256, header.Kdf);
        Assert.Equal(fixture.GetProperty("iterations").GetUInt32(), header.Iterations);
    }

    [Theory]
    [MemberData(nameof(Vaults))]
    public async Task WrongPasswordIsRejected(string file)
    {
        using var wrong = T.Draft("definitely not the password");
        var error = await Assert.ThrowsAsync<VaultException>(() => LegacyVault.OpenAsync(PathOf(file), wrong));
        Assert.Equal(VaultError.InvalidPassword, error.Error);
    }

    [Theory]
    [MemberData(nameof(Vaults))]
    public async Task MigratesEveryEntryFaithfully(string file)
    {
        var fixture = Fixture(file);
        using var dir = new TempDir();
        var source = PathOf(file);
        var before = File.ReadAllBytes(source);

        using var legacyPassword = T.Draft(fixture.GetProperty("password").GetString()!);
        using var payload = await LegacyVault.OpenAsync(source, legacyPassword);
        Assert.Equal(fixture.TryGetProperty("recoveredFromBackup", out var fromBackup) && fromBackup.GetBoolean(), payload.RecoveredFromBackup);

        var migrator = new LegacyMigrator();
        using var newPassword = T.Draft("new OnlyYours password");
        var (created, code) = await VaultSession.CreateFromDocumentAsync(
            T.Storage(dir),
            new VaultDocument(),
            (cipher, _) => migrator.Build(payload, cipher, "Migrated"),
            newPassword,
            T.FastKdf);
        code.Dispose();
        created.Dispose();

        // The legacy file is only ever read.
        Assert.Equal(before, File.ReadAllBytes(source));

        using var session = await T.Open(dir, "new OnlyYours password");
        var expected = fixture.GetProperty("entries").EnumerateArray().ToList();
        Assert.Equal(expected.Count, session.Entries.Count());
        Assert.Equal(expected.Count, migrator.Report.Entries);
        Assert.Contains(session.Document.Audit, a => a.Kind == AuditKind.Migrated);

        foreach (var want in expected)
        {
            var title = want.GetProperty("title").GetString()!;
            var entry = Assert.Single(session.Entries, e => e.Title == title);
            var username = want.GetProperty("username").GetString()!;
            var password = want.GetProperty("password").GetString()!;
            var url = want.GetProperty("url").GetString()!;
            var notes = want.GetProperty("notes").GetString()!;
            var history = want.GetProperty("history").EnumerateArray().ToList();
            var hasTotp = want.TryGetProperty("totp", out var totp);
            var isLogin = username.Length > 0 || password.Length > 0 || url.Length > 0 || hasTotp || history.Count > 0;

            Assert.Equal(isLogin ? EntryKind.Login : EntryKind.SecureNote, entry.Kind);
            Assert.Equal(want.GetProperty("favorite").GetBoolean(), entry.Favorite);
            Assert.Equal(
                want.GetProperty("tags").EnumerateArray().Select(t => t.GetString()!).Order(StringComparer.CurrentCultureIgnoreCase),
                entry.Tags);

            if (username.Length > 0)
            {
                Assert.Equal(username, entry.Username);
            }

            if (url.Length > 0)
            {
                Assert.Equal(url, entry.Url);
            }

            if (password.Length > 0)
            {
                Assert.Equal(password, T.Reveal(session, entry, entry.PrimaryPassword!));
            }

            if (notes.Length > 0)
            {
                var notesField = entry.FirstField(FieldKind.Notes)!;
                Assert.NotNull(notesField.Sealed);
                Assert.Equal(notes, T.Reveal(session, entry, notesField));
            }

            if (want.TryGetProperty("createdAt", out var createdAt))
            {
                Assert.Equal(createdAt.GetInt64(), entry.Created.ToUnixTimeMilliseconds());
            }

            if (want.TryGetProperty("updatedAt", out var updatedAt))
            {
                Assert.Equal(updatedAt.GetInt64(), entry.Modified.ToUnixTimeMilliseconds());
            }

            foreach (var custom in want.GetProperty("fields").EnumerateArray())
            {
                var label = custom.GetProperty("label").GetString();
                var value = custom.GetProperty("value").GetString()!;
                var field = Assert.Single(entry.Fields, f => f.Label == label);
                var secret = custom.TryGetProperty("secret", out var s) && s.GetBoolean();
                Assert.Equal(secret ? FieldKind.CustomSecret : FieldKind.CustomText, field.Kind);
                Assert.Equal(value, secret ? T.Reveal(session, entry, field) : field.Text);
            }

            foreach (var extra in want.GetProperty("extra").EnumerateObject())
            {
                var field = Assert.Single(entry.Fields, f => f.Label == extra.Name);
                Assert.Equal(extra.Value.ValueKind == JsonValueKind.String ? extra.Value.GetString() : extra.Value.GetRawText(), field.Text);
            }

            Assert.Equal(history.Count, entry.History.Count);
            foreach (var record in history)
            {
                var replacedAt = record.GetProperty("replacedAt").GetInt64();
                var item = Assert.Single(entry.History, h => h.ReplacedAt.ToUnixTimeMilliseconds() == replacedAt);
                using var plain = session.RevealHistory(entry, item);
                Assert.Equal(record.GetProperty("password").GetString(), plain.Span.ToString());
                Assert.Equal(entry.PrimaryPassword!.Id, item.FieldId);
            }

            if (hasTotp)
            {
                Assert.True(entry.HasTotp);
                var config = session.TotpConfigOf(entry, entry.Totp!)!;
                Assert.Equal(totp.GetProperty("digits").GetInt32(), config.Digits);
                Assert.Equal(totp.GetProperty("period").GetInt32(), config.Period);
                Assert.Equal(TotpConfig.ParseAlgorithm(totp.GetProperty("algorithm").GetString()), config.Algorithm);
                foreach (var sample in totp.GetProperty("codes").EnumerateArray())
                {
                    var at = DateTimeOffset.FromUnixTimeMilliseconds(sample.GetProperty("unixMs").GetInt64());
                    Assert.Equal(sample.GetProperty("code").GetString(), session.GenerateTotp(entry, entry.Totp!, at)!.Value.Value);
                }
            }
            else
            {
                Assert.False(entry.HasTotp);
            }
        }

        if (fixture.TryGetProperty("lastUsed", out var lastUsed))
        {
            foreach (var use in lastUsed.EnumerateObject())
            {
                var entry = session.Entries.Single(e => e.Title == use.Name);
                Assert.Equal(use.Value.GetInt64(), entry.LastUsed!.Value.ToUnixTimeMilliseconds());
            }
        }

        Assert.Equal(expected.Count(e => e.TryGetProperty("totp", out _)), migrator.Report.TotpMigrated);
        Assert.Equal(0, migrator.Report.TotpFailed);
    }

    [Fact]
    public async Task ReportCountsTheRichFixture()
    {
        using var password = T.Draft(Fixture("v3-argon2id.plf").GetProperty("password").GetString()!);
        using var payload = await LegacyVault.OpenAsync(PathOf("v3-argon2id.plf"), password);
        using var cipher = FieldCipher.CreateEphemeral();
        var migrator = new LegacyMigrator();
        var document = migrator.Build(payload, cipher, "x");
        var report = migrator.Report;
        Assert.Equal(6, report.Entries);
        Assert.Equal(5, report.Logins);
        Assert.Equal(1, report.PlainNoteEntries);
        Assert.Equal(1, report.Favorites);
        Assert.Equal(2, report.TotpMigrated);
        Assert.Equal(2, report.HistoryItems);
        Assert.Equal(2, report.CustomFields);
        Assert.Contains("Argon2id", report.SourceDescription, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(6, document.Entries.Count);
    }

    [Fact]
    public void LegacySettingsAreCarriedOver()
    {
        var settings = new AppSettings();
        var applied = LegacySettings.Apply(T.FixturePath("Legacy", "settings.json"), settings);
        Assert.True(applied > 5);
        var expected = Manifest.GetProperty("settings");

        // Legacy offered 0/1/5/15/30/60 minutes: all valid in OnlyYours.
        Assert.Equal(expected.GetProperty("autoLockMinutes").GetInt32(), settings.AutoLockMinutes);

        // Legacy 15 s is not a OnlyYours choice: the nearest, shorter (safer) one wins.
        Assert.Equal(15, expected.GetProperty("clipboardClearSeconds").GetInt32());
        Assert.Equal(10, settings.ClipboardClearSeconds);
        Assert.Equal("reduced", settings.Motion);
        Assert.False(settings.ConfirmDelete);
        Assert.True(settings.KeepPasswordHistory);
        Assert.False(settings.GlobalHotkeys);
        Assert.Equal(GeneratorMode.Passphrase, settings.Generator.Mode);
        Assert.Equal(6, settings.Generator.Words);
        Assert.Equal(".", settings.Generator.Separator);
        Assert.True(settings.Generator.CapitalizeWords);
        Assert.False(settings.Generator.AppendNumber);
        Assert.False(settings.Generator.Symbols);
    }

    [Fact]
    public void NonLegacyFilesAreRejectedCleanly()
    {
        Assert.False(LegacyVault.LooksLegacy("hello world"u8));
        Assert.Throws<VaultException>(() => LegacyVault.ParseHeader([0xFF, 0xFE, 0x0D, 0x0A, 9, 0, 0, 0]));
        Assert.Throws<VaultException>(() => LegacyVault.ParseHeader([0xFF, 0xFE, 0x0D, 0x0A]));
    }
}
