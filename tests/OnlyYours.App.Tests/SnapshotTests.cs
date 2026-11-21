using System.IO;
using System.Windows;
using OnlyYours.App.Infrastructure;
using OnlyYours.App.ViewModels;
using OnlyYours.Core.Vault.Model;
using Xunit;

namespace OnlyYours.App.Tests;

/// <summary>
/// Renders every screen of the real <see cref="MainWindow"/> — welcome, wizard, lock, shell,
/// every page, editor, palette, dialogs, toasts, migration — in both themes and at two window
/// sizes. The PNGs land in tests/OnlyYours.App.Tests/Snapshots for visual review; the test
/// itself fails on binding errors, blank renders or exceptions.
/// </summary>
public class SnapshotTests
{
    private static readonly string LegacyFixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Legacy", "v3-argon2id.plf");

    [Theory]
    [InlineData("dark", "aurum", 1280, 820)]
    [InlineData("light", "aurum", 1280, 820)]
    [InlineData("dark", "jade", 1000, 660)]
    [InlineData("light", "copper", 1000, 660)]
    public async Task EveryScreenRendersCleanly(string theme, string accent, int width, int height)
    {
        using var t = TestApp.Create(s =>
        {
            s.Theme = theme;
            s.Accent = accent;
        });

        var shots = new List<(string Name, double Detail)>();
        var blackText = new List<string>();
        await Ui.Run(async () =>
        {
            Ui.Theme(theme, accent);
            Ui.TakeBindingErrors();
            Ui.TakeExceptions();
            var app = t.Vm;
            var window = new MainWindow(app)
            {
                Width = width,
                Height = height,
                Left = -32000,
                Top = -32000,
                WindowStartupLocation = WindowStartupLocation.Manual,
                ShowActivated = false,
                ShowInTaskbar = false,
            };
            window.ClosesWithoutExit = true;
            window.Show();
            var prefix = $"{theme}-{accent}-{width}x{height}";

            async Task Shot(string name)
            {
                await Ui.Settle();
                var root = (FrameworkElement)window.Content;
                shots.Add((name, Ui.Detail(root)));
                blackText.AddRange(Ui.SystemColoredText(root).Select(text => $"{name}: {text}"));
                Ui.Capture(root, $"{prefix}-{name}");
            }

            try
            {
                app.Start();
                await Shot("01-welcome");

                app.Show(new WelcomeViewModel(app, LegacyFixture));
                await Shot("02-welcome-legacy");

                app.ShowCreate();
                var create = (CreateVaultViewModel)app.Stage!;
                await Shot("03-create");

                create.Name = "Personal";
                create.Password.Set(Flow.Password);
                create.Confirm.Set("Copper-Lantern-Orbit-4");
                await Shot("04-create-typing");

                create.Confirm.Set(Flow.Password);
                await ((AsyncCommand)create.CreateCommand).ExecuteAsync(null);
                await Shot("05-create-recovery");

                create.Acknowledged = true;
                await ((AsyncCommand)create.ContinueCommand).ExecuteAsync(null);
                if (create.Step == 2)
                {
                    await Shot("06-create-hello");
                    await ((AsyncCommand)create.ContinueCommand).ExecuteAsync(null);
                }

                await Shot("07-create-done");
                create.FinishCommand.Execute(null);
                foreach (var toast in app.Toasts.ToList())
                {
                    toast.DismissCommand.Execute(null);
                }

                var shell = (ShellViewModel)app.Stage!;
                await Shot("08-shell-empty");

                Flow.Seed(shell.Session);
                shell.RefreshList();
                for (var i = 0; i < 150 && shell.Health.Analyzed == 0; i++)
                {
                    await Task.Delay(20);
                    await Ui.Settle(1);
                }

                await Shot("09-shell-list");

                shell.Selected = shell.Items.First(i => i.Entry.Title == "GitHub");
                await Shot("10-detail-login");

                shell.Selected = shell.Items.First(i => i.Entry.Title == "Visa Gold");
                await Shot("11-detail-card");

                shell.Query = "bank";
                await Shot("12-search");
                shell.Query = string.Empty;

                shell.NewEntryCommand.Execute(EntryKind.Login);
                await Shot("13-editor-new");
                shell.Editor!.CancelCommand.Execute(null);

                shell.Selected = shell.Items.First(i => i.Entry.Title == "GitHub");
                shell.EditCommand.Execute(null);
                await Shot("14-editor-edit");
                shell.Editor!.CancelCommand.Execute(null);

                shell.ShowHealthCategory("reused", Loc.T("Security.Cat.reused"));
                await Shot("15-health-filter");
                shell.SelectFilter(shell.Rail.First(r => r.Filter == FilterKind.All));

                foreach (var (page, name) in new[]
                {
                    (ShellPage.Generator, "16-generator"),
                    (ShellPage.Security, "17-security"),
                    (ShellPage.Sharing, "18-sharing"),
                    (ShellPage.Trash, "19-trash"),
                    (ShellPage.Transfer, "20-transfer"),
                    (ShellPage.Settings, "21-settings"),
                })
                {
                    shell.Navigate(page);
                    await Shot(name);
                }

                shell.Navigate(ShellPage.Vault);
                app.OpenPalette();
                app.Palette!.Query = "git";
                await Shot("22-palette");
                app.ClosePalette();

                var confirm = app.ConfirmAsync(Loc.T("Entry.TrashTitle"), Loc.F("Entry.TrashMessage", "GitHub", 30), Loc.T("Entry.MoveToTrash"), danger: true, iconKey: "Icon.Trash");
                await Shot("23-dialog-confirm");
                app.Dialogs[0].Close(false);
                await confirm;

                var ask = app.AskPasswordAsync(Loc.T("ChangePassword.Title"), Loc.T("ChangePassword.New"), Loc.T("ChangePassword.Apply"), confirm: true, strength: true);
                await Shot("24-dialog-password");
                app.Dialogs[0].Close(null);
                (await ask)?.Dispose();

                app.Toast(Loc.F("Toast.CopiedField", Loc.T("FieldKind.Password")), Loc.F("Toast.ClearsIn", 30), ToastKind.Success);
                app.Toast(Loc.T("Save.Failed"), Loc.T("VaultError.IoFailure"), ToastKind.Error);
                await Shot("25-toasts");
                foreach (var toast in app.Toasts.ToList())
                {
                    toast.DismissCommand.Execute(null);
                }

                await app.LockAsync("Lock.Reason.Idle");
                await Shot("26-lock");

                var lockVm = (LockViewModel)app.Stage!;
                lockVm.Password.Set("wrong-password");
                await ((AsyncCommand)lockVm.UnlockCommand).ExecuteAsync(null);
                await Shot("27-lock-error");

                app.ShowMigration(LegacyFixture);
                await Shot("28-migration");
                var migration = (MigrationViewModel)app.Stage!;
                migration.LegacyPassword.Set("correct horse battery staple ✓");
                await ((AsyncCommand)migration.DecryptCommand).ExecuteAsync(null);
                await Shot("29-migration-options");
            }
            finally
            {
                window.Close();
                Motion.Level = MotionLevel.Off;
                Ui.Theme("dark", "aurum");
            }
        });

        var exceptions = Ui.TakeExceptions();
        Assert.True(exceptions.Count == 0, string.Join(Environment.NewLine + Environment.NewLine, exceptions.Take(5)));
        var errors = Ui.TakeBindingErrors();
        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors.Take(15)));
        Assert.True(blackText.Count == 0, "System-black text: " + string.Join(" | ", blackText.Distinct().Take(20)));
        foreach (var (name, detail) in shots)
        {
            Assert.True(detail > 0.01, $"{name} looks blank ({detail:P2} detail)");
        }
    }

    [Fact]
    public async Task TheOpeningSequenceIsFilmedFrameByFrame()
    {
        using var t = TestApp.Create(s => s.Intro = "cinematic");
        var frames = new List<(double Time, double Detail)>();
        await Ui.Run(async () =>
        {
            Motion.Level = MotionLevel.Cinematic;
            var app = t.Vm;
            app.Start();
            var window = new MainWindow(app)
            {
                Width = 1280,
                Height = 820,
                Left = -32000,
                Top = -32000,
                WindowStartupLocation = WindowStartupLocation.Manual,
                ShowActivated = false,
                ShowInTaskbar = false,
            };
            window.ClosesWithoutExit = true;
            window.Show();
            try
            {
                for (var i = 0; i < 50 && window.Intro?.IsPlaying != true; i++)
                {
                    await Task.Delay(20);
                    await Ui.Settle(1);
                }

                Assert.True(window.Intro?.IsPlaying == true, "the intro never started");
                foreach (var time in new[] { 0.3, 0.75, 1.0, 1.4, 1.9, 2.3, 2.7, 2.95, 3.4, 3.9, 4.4, 4.9, 5.3 })
                {
                    window.Intro!.FreezeAt(TimeSpan.FromSeconds(time));
                    await Ui.Settle(2);
                    var root = (FrameworkElement)window.Content;
                    frames.Add((time, Ui.Detail(root)));
                    Ui.Capture(root, $"intro-{time:0.00}s".Replace(',', '.'));
                }
            }
            finally
            {
                window.Close();
                Motion.Level = MotionLevel.Off;
            }
        });

        // From the spark on, every frame has visible content.
        Assert.All(frames.Where(f => f.Time >= 0.75), f => Assert.True(f.Detail > 0.005, $"frame {f.Time}s is blank"));
    }

    [Fact]
    public async Task TheMatchCutLandsOnTheHeroSealInRealTime()
    {
        using var t = TestApp.Create(s => s.Intro = "cinematic");
        await Ui.Run(async () =>
        {
            Motion.Level = MotionLevel.Cinematic;
            var app = t.Vm;
            app.Start();
            var window = new MainWindow(app)
            {
                Width = 1280,
                Height = 820,
                Left = -32000,
                Top = -32000,
                WindowStartupLocation = WindowStartupLocation.Manual,
                ShowActivated = false,
                ShowInTaskbar = false,
                ClosesWithoutExit = true,
            };
            window.Show();
            try
            {
                for (var i = 0; i < 100 && window.Intro?.IsPlaying != true; i++)
                {
                    await Task.Delay(10);
                }

                var clock = System.Diagnostics.Stopwatch.StartNew();
                foreach (var at in new[] { 4.6, 4.95, 5.15, 5.35 })
                {
                    var wait = TimeSpan.FromSeconds(at) - clock.Elapsed;
                    if (wait > TimeSpan.Zero)
                    {
                        await Task.Delay(wait);
                    }

                    Ui.Capture((FrameworkElement)window.Content, $"intro-live-{at:0.0}s".Replace(',', '.'));
                }

                Assert.True(window.Intro!.MatchCutStarted, "the match cut never started");

                // The sequence hands over to the real screen and disappears.
                for (var i = 0; i < 300 && window.Intro?.Visibility == Visibility.Visible; i++)
                {
                    await Task.Delay(20);
                }

                Ui.Capture((FrameworkElement)window.Content, "intro-live-end");
                Assert.Equal(Visibility.Collapsed, window.Intro!.Visibility);
                Assert.True(clock.Elapsed < TimeSpan.FromSeconds(8), $"intro lasted {clock.Elapsed.TotalSeconds:0.0}s");
            }
            finally
            {
                window.Close();
                Motion.Level = MotionLevel.Off;
            }
        });
    }
}
