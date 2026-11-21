using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using OnlyYours.App.Infrastructure;
using OnlyYours.App.Services;
using OnlyYours.Core.Security.Crypto;
using OnlyYours.Core.Settings;

namespace OnlyYours.App.ViewModels;

/// <summary>Preferences, applied live and saved immediately.</summary>
public sealed class SettingsViewModel : ObservableObject
{
    private readonly AppViewModel _app;
    private readonly ShellViewModel? _shell;

    public SettingsViewModel(AppViewModel app, ShellViewModel? shell)
    {
        _app = app;
        _shell = shell;
        OpenDataFolderCommand = new RelayCommand(() => Process.Start(new ProcessStartInfo("explorer.exe", $"\"{app.Paths.DataDirectory}\"") { UseShellExecute = true }));
        RenameVaultCommand = new AsyncCommand(RenameAsync, () => shell is not null);
        ApplyHotkeysCommand = new RelayCommand(() => OnlyYours.App.App.ApplyBehavior(app));
    }

    private AppSettings S => _app.Settings;

    public IReadOnlyList<ChoiceOption> Themes { get; } =
    [
        new(Loc.T("Settings.Theme.Dark"), Loc.T("Settings.Theme.DarkDesc"), "Icon.Moon", "dark"),
        new(Loc.T("Settings.Theme.Light"), Loc.T("Settings.Theme.LightDesc"), "Icon.Sun", "light"),
        new(Loc.T("Settings.Theme.System"), Loc.T("Settings.Theme.SystemDesc"), "Icon.Palette", "system"),
    ];

    public IReadOnlyList<ChoiceOption> Accents { get; } =
    [
        new(Loc.T("Accent.aurum"), "#E3B261", "Icon.Seal", "aurum"),
        new(Loc.T("Accent.copper"), "#DE8E5E", "Icon.Seal", "copper"),
        new(Loc.T("Accent.platinum"), "#CBD0D8", "Icon.Seal", "platinum"),
        new(Loc.T("Accent.jade"), "#5FC2A0", "Icon.Seal", "jade"),
    ];

    public IReadOnlyList<ChoiceOption> MotionLevels { get; } =
    [
        new(Loc.T("Settings.Motion.Cinematic"), string.Empty, "Icon.Sparkle", "cinematic"),
        new(Loc.T("Settings.Motion.Reduced"), string.Empty, "Icon.Minus", "reduced"),
        new(Loc.T("Settings.Motion.Off"), string.Empty, "Icon.Close", "off"),
    ];

    public IReadOnlyList<ChoiceOption> IntroModes { get; } =
    [
        new(Loc.T("Settings.Intro.Cinematic"), string.Empty, "Icon.Sparkle", "cinematic"),
        new(Loc.T("Settings.Intro.Short"), string.Empty, "Icon.Bolt", "short"),
        new(Loc.T("Settings.Intro.Off"), string.Empty, "Icon.Close", "off"),
    ];

    public IReadOnlyList<ChoiceOption> AutoLockOptions { get; } = AppSettings.AutoLockChoices
        .Select(m => new ChoiceOption(m == 0 ? Loc.T("Settings.Never") : Loc.F("Settings.Minutes", m), string.Empty, "Icon.Hourglass", m))
        .ToList();

    public IReadOnlyList<ChoiceOption> ClipboardOptions { get; } = AppSettings.ClipboardChoices
        .Select(s => new ChoiceOption(s == 0 ? Loc.T("Settings.Never") : Loc.F("Settings.Seconds", s), string.Empty, "Icon.Clipboard", s))
        .ToList();

    public IReadOnlyList<ChoiceOption> KdfOptions { get; } =
    [
        new(Loc.T("Kdf.Standard"), KdfParameters.Standard.Describe(), "Icon.Shield", "standard"),
        new(Loc.T("Kdf.High"), KdfParameters.High.Describe(), "Icon.ShieldCheck", "high"),
        new(Loc.T("Kdf.Extreme"), KdfParameters.Extreme.Describe(), "Icon.Bolt", "extreme"),
    ];

    public string Theme
    {
        get => S.Theme;
        set => Apply(() => S.Theme = value, appearance: true);
    }

    public string Accent
    {
        get => S.Accent;
        set => Apply(() => S.Accent = value, appearance: true);
    }

    public string MotionLevel
    {
        get => S.Motion;
        set => Apply(() => S.Motion = value, appearance: true);
    }

    public string Intro
    {
        get => S.Intro;
        set => Apply(() => S.Intro = value);
    }

    public int AutoLockMinutes
    {
        get => S.AutoLockMinutes;
        set => Apply(() => S.AutoLockMinutes = value);
    }

    public bool LockOnWindowsLock
    {
        get => S.LockOnWindowsLock;
        set => Apply(() => S.LockOnWindowsLock = value);
    }

    public bool LockOnSleep
    {
        get => S.LockOnSleep;
        set => Apply(() => S.LockOnSleep = value);
    }

    public bool LockOnMinimize
    {
        get => S.LockOnMinimize;
        set => Apply(() => S.LockOnMinimize = value);
    }

    public int ClipboardClearSeconds
    {
        get => S.ClipboardClearSeconds;
        set => Apply(() => S.ClipboardClearSeconds = value);
    }

    public bool ClipboardExcludeHistory
    {
        get => S.ClipboardExcludeHistory;
        set => Apply(() => S.ClipboardExcludeHistory = value);
    }

    public bool ScreenCaptureProtection
    {
        get => S.ScreenCaptureProtection;
        set => Apply(() => S.ScreenCaptureProtection = value, behavior: true);
    }

    public bool ProcessHardening
    {
        get => S.ProcessHardening;
        set => Apply(() => S.ProcessHardening = value);
    }

    public bool ConfirmDelete
    {
        get => S.ConfirmDelete;
        set => Apply(() => S.ConfirmDelete = value);
    }

    public bool KeepPasswordHistory
    {
        get => S.KeepPasswordHistory;
        set => Apply(() =>
        {
            S.KeepPasswordHistory = value;
            if (_app.Session is { } session)
            {
                session.KeepPasswordHistory = value;
            }
        });
    }

    public bool ColorizePasswords
    {
        get => S.ColorizePasswords;
        set => Apply(() => S.ColorizePasswords = value);
    }

    public bool MinimizeToTray
    {
        get => S.MinimizeToTray;
        set => Apply(() => S.MinimizeToTray = value, behavior: true);
    }

    public bool CloseToTray
    {
        get => S.CloseToTray;
        set => Apply(() => S.CloseToTray = value, behavior: true);
    }

    public bool StartWithWindows
    {
        get => S.StartWithWindows;
        set => Apply(() =>
        {
            S.StartWithWindows = value;
            StartupRegistration.Set(value, S.StartMinimized);
        });
    }

    public bool StartMinimized
    {
        get => S.StartMinimized;
        set => Apply(() =>
        {
            S.StartMinimized = value;
            if (S.StartWithWindows)
            {
                StartupRegistration.Set(true, value);
            }
        });
    }

    public bool GlobalHotkeys
    {
        get => S.GlobalHotkeys;
        set => Apply(() => S.GlobalHotkeys = value, behavior: true);
    }

    public int AutoTypeDelayMs
    {
        get => S.AutoTypeDelayMs;
        set => Apply(() => S.AutoTypeDelayMs = Math.Clamp(value, 0, 250));
    }

    public int TrashRetentionDays
    {
        get => S.TrashRetentionDays;
        set => Apply(() => S.TrashRetentionDays = Math.Clamp(value, 1, 365));
    }

    public int BackupRetention
    {
        get => S.BackupRetention;
        set => Apply(() =>
        {
            S.BackupRetention = Math.Clamp(value, 1, 200);
            _app.Storage.BackupRetention = S.BackupRetention;
        });
    }

    public string KdfStrength
    {
        get => S.KdfStrength;
        set => Apply(() => S.KdfStrength = value);
    }

    public string HotkeyStatus => _app.Hotkeys is { Failures.Count: > 0 } hotkeys
        ? Loc.F("Settings.HotkeyFailed", string.Join(", ", hotkeys.Failures))
        : Loc.F("Settings.HotkeyOk", HotkeyGesture.QuickSearch, HotkeyGesture.AutoType);

    public string DataFolder => _app.Paths.DataDirectory;

    public string VaultFile => _app.Storage.VaultPath;

    public string VaultName => _shell?.VaultName ?? _app.VaultDisplayName;

    public string Version => typeof(SettingsViewModel).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    public string Runtime => $".NET {Environment.Version} · {(System.Runtime.Intrinsics.X86.Avx2.IsSupported ? "AVX2" : "SSE")} · {Environment.ProcessorCount} threads";

    public ICommand OpenDataFolderCommand { get; }

    public ICommand RenameVaultCommand { get; }

    public ICommand ApplyHotkeysCommand { get; }

    private void Apply(Action change, bool appearance = false, bool behavior = false)
    {
        change();
        S.Normalize();
        _app.SaveSettings();
        if (appearance)
        {
            OnlyYours.App.App.ApplyAppearance(S);
        }

        if (behavior)
        {
            OnlyYours.App.App.ApplyBehavior(_app);
        }

        OnPropertyChanged(string.Empty);
    }

    private async Task RenameAsync()
    {
        if (_shell is null)
        {
            return;
        }

        var name = await _app.AskTextAsync(Loc.T("Settings.RenameTitle"), Loc.T("Settings.RenameLabel"), _shell.VaultName);
        if (!string.IsNullOrWhiteSpace(name))
        {
            _shell.Session.Rename(name);
            OnPropertyChanged(nameof(VaultName));
        }
    }
}
