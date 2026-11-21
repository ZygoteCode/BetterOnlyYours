using System.IO;
using OnlyYours.App.Infrastructure;
using OnlyYours.App.ViewModels;
using OnlyYours.Core.Otp;
using OnlyYours.Core.Security.Memory;
using OnlyYours.Core.Vault;
using OnlyYours.Core.Vault.Model;
using Xunit;

namespace OnlyYours.App.Tests;

internal static class Flow
{
    public const string Password = "Copper-Lantern-Orbit-42!";

    /// <summary>Walks the real creation wizard and lands in the shell.</summary>
    public static async Task<ShellViewModel> CreateVault(AppViewModel app, string name = "Personal")
    {
        app.ShowCreate();
        var create = Assert.IsType<CreateVaultViewModel>(app.Stage);
        create.Name = name;
        create.Password.Set(Password);
        create.Confirm.Set(Password);
        await ((AsyncCommand)create.CreateCommand).ExecuteAsync(null);
        Assert.Null(create.Error);
        Assert.Equal(1, create.Step);
        create.Acknowledged = true;
        await ((AsyncCommand)create.ContinueCommand).ExecuteAsync(null);
        if (create.Step == 2)
        {
            await ((AsyncCommand)create.ContinueCommand).ExecuteAsync(null);
        }

        Assert.Equal(3, create.Step);
        create.FinishCommand.Execute(null);
        return Assert.IsType<ShellViewModel>(app.Stage);
    }

    public static Entry Login(VaultSession session, string title, string user, string password, string? url = null, string[]? tags = null, bool favorite = false, string? notes = null)
    {
        using var secret = SecretDraft.From(password);
        using var notesDraft = SecretDraft.From(notes ?? string.Empty);
        return session.Apply(new EntryEdit
        {
            Kind = EntryKind.Login,
            Title = title,
            Favorite = favorite,
            Tags = [.. tags ?? []],
            Fields =
            [
                new FieldEdit { Kind = FieldKind.Username, Text = user },
                new FieldEdit { Kind = FieldKind.Password, Secret = secret, SecretChanged = true },
                new FieldEdit { Kind = FieldKind.Url, Text = url },
                new FieldEdit { Kind = FieldKind.Notes, Secret = notesDraft, SecretChanged = true },
            ],
        });
    }

    /// <summary>A realistic vault: logins, a card, a note, TOTP, history, tags, a trashed item.</summary>
    public static void Seed(VaultSession session)
    {
        var github = Login(session, "GitHub", "ada@example.com", "Tr0ub4dor&3-horse-staple", "https://github.com/login", ["dev", "work"], favorite: true, notes: "SSH keys on the laptop.");
        Assert.True(OtpAuthUri.TryParse("otpauth://totp/GitHub:ada?secret=JBSWY3DPEHPK3PXP&issuer=GitHub", null, out var setup, out _));
        using (setup)
        {
            session.SetTotp(github.Id, setup!);
        }

        using (var newer = SecretDraft.From("Tr0ub4dor&3-horse-staple-2026"))
        {
            session.Apply(new EntryEdit
            {
                EntryId = github.Id,
                Kind = github.Kind,
                Title = github.Title,
                Favorite = true,
                Tags = [.. github.Tags],
                Fields = [.. github.Fields.Select(f => f.Kind == FieldKind.Password
                    ? new FieldEdit { Id = f.Id, Kind = f.Kind, Secret = newer, SecretChanged = true }
                    : new FieldEdit { Id = f.Id, Kind = f.Kind, Label = f.Label, Flags = f.Flags, Text = f.Text })],
            });
        }

        Login(session, "Barclays Bank", "ada.lovelace", "password", "https://barclays.co.uk", ["bank"]);
        Login(session, "Mail", "ada@mail.example", "password", "https://mail.example.com", ["work"]);
        Login(session, "Netflix", "ada@example.com", "qN7#vL2@xP9!mR4$", "https://netflix.com", ["home"]);
        Login(session, "Amazon", "ada@example.com", "Zq8!rT5#wY2@pL7$mN4%", "https://amazon.it", favorite: true);

        using (var number = SecretDraft.From("4111 1111 1111 1111"))
        using (var cvv = SecretDraft.From("123"))
        {
            session.Apply(new EntryEdit
            {
                Kind = EntryKind.PaymentCard,
                Title = "Visa Gold",
                Fields =
                [
                    new FieldEdit { Kind = FieldKind.CardHolder, Text = "Ada Lovelace" },
                    new FieldEdit { Kind = FieldKind.CardNumber, Secret = number, SecretChanged = true },
                    new FieldEdit { Kind = FieldKind.CardExpiry, Text = "12/29" },
                    new FieldEdit { Kind = FieldKind.CardCvv, Secret = cvv, SecretChanged = true },
                ],
            });
        }

        using (var note = SecretDraft.From("Combinazione cassaforte: 31-07-15\nCodice allarme: 4711"))
        {
            session.Apply(new EntryEdit
            {
                Kind = EntryKind.SecureNote,
                Title = "Home",
                Tags = ["home"],
                Fields = [new FieldEdit { Kind = FieldKind.Notes, Secret = note, SecretChanged = true }],
            });
        }

        var old = Login(session, "Old forum", "ada1815", "hunter2");
        session.MoveToTrash(old.Id);
    }
}

public class ViewModelFlowTests
{
    [Fact]
    public async Task CreateLockAndUnlockThroughTheRealViewModels()
    {
        using var t = TestApp.Create();
        await Ui.Run(async () =>
        {
            var app = t.Vm;
            app.Start();
            Assert.IsType<WelcomeViewModel>(app.Stage);

            var shell = await Flow.CreateVault(app);
            Assert.True(app.IsUnlocked);
            Assert.True(File.Exists(Path.Combine(t.Root, "vault.onlyyours")));
            Assert.Equal("Personal", shell.VaultName);
            Assert.Contains(app.Toasts, toast => toast.Title == Loc.T("Create.Welcome"));

            await app.LockAsync("Lock.Reason.Idle");
            var lockVm = Assert.IsType<LockViewModel>(app.Stage);
            Assert.False(app.IsUnlocked);
            Assert.True(lockVm.JustLocked);

            lockVm.Password.Set("not the password");
            await ((AsyncCommand)lockVm.UnlockCommand).ExecuteAsync(null);
            Assert.True(lockVm.HasError);
            Assert.False(app.IsUnlocked);
            Assert.Equal(1, app.Settings.FailedUnlocks);

            lockVm.Password.Set(Flow.Password);
            await ((AsyncCommand)lockVm.UnlockCommand).ExecuteAsync(null);
            Assert.True(app.IsUnlocked);
            Assert.IsType<ShellViewModel>(app.Stage);
            Assert.Equal(0, app.Settings.FailedUnlocks);
        });
    }

    [Fact]
    public async Task WeakPasswordsNeedConfirmationAndMismatchesAreCaught()
    {
        using var t = TestApp.Create();
        await Ui.Run(async () =>
        {
            t.Vm.ShowCreate();
            var create = (CreateVaultViewModel)t.Vm.Stage!;
            create.Password.Set("short");
            create.Confirm.Set("short");
            await ((AsyncCommand)create.CreateCommand).ExecuteAsync(null);
            Assert.Equal(Loc.T("Create.TooShort"), create.Error);

            create.Password.Set("password123");
            create.Confirm.Set("password124");
            Assert.False(create.ConfirmMatches);
            await ((AsyncCommand)create.CreateCommand).ExecuteAsync(null);
            Assert.Equal(Loc.T("Error.PasswordMismatch"), create.Error);

            create.Confirm.Set("password123");
            var pending = ((AsyncCommand)create.CreateCommand).ExecuteAsync(null);
            await Ui.Settle();
            var dialog = Assert.Single(t.Vm.Dialogs);
            Assert.True(dialog.IsDanger);
            dialog.Close(false);
            await pending;
            Assert.Equal(0, create.Step);
            Assert.False(File.Exists(Path.Combine(t.Root, "vault.onlyyours")));
        });
    }

    [Fact]
    public async Task ShellSearchFiltersAndDetail()
    {
        using var t = TestApp.Create();
        await Ui.Run(async () =>
        {
            var shell = await Flow.CreateVault(t.Vm);
            Flow.Seed(shell.Session);
            shell.RefreshList();
            Assert.Equal(7, shell.Items.Count);

            shell.Query = "bank";
            await Ui.Settle();
            Assert.Equal("Barclays Bank", Assert.Single(shell.Items).Entry.Title);

            shell.Query = string.Empty;
            await Ui.Settle();
            var favorites = shell.Rail.First(r => r.Filter == FilterKind.Favorites);
            shell.SelectFilter(favorites);
            Assert.Equal(2, shell.Items.Count);
            Assert.Equal(2, favorites.Count);

            shell.SelectFilter(shell.Rail.First(r => r.Filter == FilterKind.All));
            shell.Selected = shell.Items.First(i => i.Entry.Title == "GitHub");
            var detail = Assert.IsType<EntryDetailViewModel>(shell.Detail);
            Assert.True(detail.HasTotp);
            Assert.Matches("^[0-9]{3} [0-9]{3}$", detail.Totp!.Code);
            Assert.Single(shell.Selected.Entry.History);

            for (var i = 0; i < 100 && shell.Health.Analyzed == 0; i++)
            {
                await Task.Delay(20);
                await Ui.Settle(1);
            }

            Assert.True(shell.Health.Reused.Count >= 2);
            Assert.True(shell.HealthBadge > 0);
        });
    }

    [Fact]
    public async Task EditorCreatesAndEditsEntries()
    {
        using var t = TestApp.Create();
        await Ui.Run(async () =>
        {
            var shell = await Flow.CreateVault(t.Vm);
            shell.NewEntryCommand.Execute(EntryKind.Login);
            var editor = Assert.IsType<EntryEditorViewModel>(shell.Editor);
            Assert.True(editor.IsNew);
            editor.Title = "New site";
            editor.Fields.First(f => f.Kind == FieldKind.Username).Text = "ada";
            editor.Fields.First(f => f.Kind == FieldKind.Password).Secret!.Set("Una-Password-Robusta-77");
            await ((AsyncCommand)shell.SaveEditCommand).ExecuteAsync(null);
            Assert.Null(shell.Editor);

            var entry = Assert.Single(shell.Session.Entries);
            Assert.Equal("New site", entry.Title);
            Assert.Equal("ada", entry.Username);
            using (var plain = shell.Session.Reveal(entry, entry.PrimaryPassword!))
            {
                Assert.Equal("Una-Password-Robusta-77", plain.Span.ToString());
            }

            shell.Selected = shell.Items.Single();
            shell.EditCommand.Execute(null);
            var edit = Assert.IsType<EntryEditorViewModel>(shell.Editor);
            Assert.False(edit.IsNew);
            edit.Title = string.Empty;
            await ((AsyncCommand)shell.SaveEditCommand).ExecuteAsync(null);
            Assert.True(edit.HasError);
            Assert.NotNull(shell.Editor);
            edit.Title = "Renamed";
            await ((AsyncCommand)shell.SaveEditCommand).ExecuteAsync(null);
            Assert.Equal("Renamed", entry.Title);
        });
    }

    [Fact]
    public async Task TrashFlowHonoursTheConfirmationSetting()
    {
        using var t = TestApp.Create(s => s.ConfirmDelete = false);
        await Ui.Run(async () =>
        {
            var shell = await Flow.CreateVault(t.Vm);
            Flow.Login(shell.Session, "To delete", "x", "y");
            shell.RefreshList();
            shell.Selected = shell.Items.Single();
            await ((AsyncCommand)shell.TrashCommand).ExecuteAsync(null);
            Assert.Empty(shell.Session.Entries);
            Assert.Single(shell.Session.Document.Trash);
        });
    }

    [Fact]
    public async Task GeneratorProducesFreshSecretsIntoHistory()
    {
        using var t = TestApp.Create();
        await Ui.Run(async () =>
        {
            var shell = await Flow.CreateVault(t.Vm);
            shell.Navigate(ShellPage.Generator);
            var generator = Assert.IsType<GeneratorViewModel>(shell.PageViewModel);
            generator.Length = 32;
            generator.RegenerateCommand.Execute(null);
            generator.RegenerateCommand.Execute(null);
            await Ui.Settle();
            Assert.True(generator.HasHistory);
            using var secret = generator.OpenSecret();
            Assert.Equal(32, secret!.Length);
        });
    }

    [Fact]
    public async Task SettingsApplyImmediatelyAndPersist()
    {
        using var t = TestApp.Create();
        await Ui.Run(async () =>
        {
            var shell = await Flow.CreateVault(t.Vm);
            shell.Navigate(ShellPage.Settings);
            var settings = Assert.IsType<SettingsViewModel>(shell.PageViewModel);
            settings.Theme = "light";
            Assert.False(ThemeManager.IsDark);
            settings.Accent = "jade";
            Assert.Equal("jade", ThemeManager.Accent);
            Assert.Equal("Unlock", Loc.T("Lock.Unlock"));
            settings.AutoLockMinutes = 5;
            t.Vm.FlushSettings();

            var saved = new Core.Settings.SettingsStore(Path.Combine(t.Root, "settings.json")).Load();
            Assert.Equal("light", saved.Theme);
            Assert.Equal("jade", saved.Accent);
            Assert.Equal(5, saved.AutoLockMinutes);

            settings.Theme = "dark";
            settings.Accent = "aurum";
        });
    }

    [Fact]
    public async Task PaletteFindsEntriesAndCommands()
    {
        using var t = TestApp.Create();
        await Ui.Run(async () =>
        {
            var shell = await Flow.CreateVault(t.Vm);
            Flow.Seed(shell.Session);
            shell.RefreshList();
            t.Vm.OpenPalette();
            var palette = t.Vm.Palette!;
            palette.Query = "git";
            await Ui.Settle();
            Assert.Equal("GitHub", palette.Results.First().Title);

            palette.Query = "gener";
            await Ui.Settle();
            Assert.Contains(palette.Results, i => i.Title == Loc.T("Palette.Generator"));
            var completed = false;
            palette.Completed += () => completed = true;
            palette.Execute(palette.Results.First(i => i.Title == Loc.T("Palette.Generator")), System.Windows.Input.ModifierKeys.None);
            Assert.Equal(ShellPage.Generator, shell.Page);

            // The view closes the palette (in-app or the global quick-search window) on Completed.
            Assert.True(completed);
        });
    }

    [Fact]
    public async Task MigrationFromAGenuineLegacyVault()
    {
        using var t = TestApp.Create();
        var legacy = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Legacy", "v3-argon2id.plf");
        await Ui.Run(async () =>
        {
            t.Vm.ShowMigration(legacy);
            var migration = Assert.IsType<MigrationViewModel>(t.Vm.Stage);
            Assert.True(migration.HasSource);

            migration.LegacyPassword.Set("wrong");
            await ((AsyncCommand)migration.DecryptCommand).ExecuteAsync(null);
            Assert.True(migration.HasError);

            migration.LegacyPassword.Set("correct horse battery staple ✓");
            await ((AsyncCommand)migration.DecryptCommand).ExecuteAsync(null);
            Assert.False(migration.HasError);
            Assert.Equal(1, migration.Step);

            migration.ReusePassword = true;
            await ((AsyncCommand)migration.MigrateCommand).ExecuteAsync(null);
            Assert.NotNull(migration.Report);
            Assert.Equal(6, migration.Report!.Entries);
            migration.Acknowledged = true;
            migration.FinishCommand.Execute(null);
            var shell = Assert.IsType<ShellViewModel>(t.Vm.Stage);
            Assert.Equal(6, shell.Session.Entries.Count());

            // The legacy settings.json next to the fixture was imported (reduceMotion → reduced).
            Assert.Equal("reduced", t.Vm.Settings.Motion);
            Motion.Level = MotionLevel.Off;
        });

        // Reopen with the same (reused) password.
        using var draft = SecretDraft.From("correct horse battery staple ✓");
        using var reopened = await VaultSession.OpenAsync(new VaultStorage(Path.Combine(t.Root, "vault.onlyyours")), draft);
        Assert.Equal(6, reopened.Entries.Count());
    }
}

public class HealthRefreshTests
{
    [Fact]
    public async Task TheSecurityCenterFollowsTheBackgroundAnalysis()
    {
        using var t = TestApp.Create();
        await Ui.Run(async () =>
        {
            var shell = await Flow.CreateVault(t.Vm);
            Flow.Seed(shell.Session);
            shell.RefreshList();

            // Open the page before the debounced analysis has finished.
            shell.Navigate(ShellPage.Security);
            var security = Assert.IsType<SecurityViewModel>(shell.PageViewModel);
            for (var i = 0; i < 150 && shell.Health.Analyzed == 0; i++)
            {
                await Task.Delay(20);
                await Ui.Settle(1);
            }

            await Ui.Settle();
            Assert.True(shell.Health.Analyzed > 0, "health never analysed");
            Assert.Same(security, shell.PageViewModel);
            Assert.True(security.Score < 100, $"score {security.Score}");
            Assert.Contains(security.Categories, c => c.Key == "reused" && c.Count >= 2);
            Assert.Contains(shell.Health.Analyzed.ToString(System.Globalization.CultureInfo.CurrentCulture), security.Summary);

            // The health filter list fills in once the analysis is there.
            shell.ShowHealthCategory("reused", "x");
            Assert.True(shell.Items.Count >= 2, $"{shell.Items.Count} reused items listed");
        });
    }
}

public class StageNotificationTests
{
    [Fact]
    public async Task ShellFollowsTheStageForTitleBarBindings()
    {
        using var t = TestApp.Create();
        await Ui.Run(async () =>
        {
            var changed = new List<string?>();
            t.Vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
            t.Vm.Start();
            await Flow.CreateVault(t.Vm);
            Assert.Contains(nameof(AppViewModel.Shell), changed);
            Assert.NotNull(t.Vm.Shell);

            changed.Clear();
            await t.Vm.LockAsync(null);
            Assert.Contains(nameof(AppViewModel.Shell), changed);
            Assert.Null(t.Vm.Shell);
        });
    }
}
