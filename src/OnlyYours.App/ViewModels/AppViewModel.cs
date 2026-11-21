using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using OnlyYours.App.Controls;
using OnlyYours.App.Infrastructure;
using OnlyYours.App.Services;
using OnlyYours.Core.Platform;
using OnlyYours.Core.Security.Crypto;
using OnlyYours.Core.Security.Memory;
using OnlyYours.Core.Settings;
using OnlyYours.Core.Vault;
using OnlyYours.Core.Vault.Model;

namespace OnlyYours.App.ViewModels;

/// <summary>
/// Root of the application state: settings, the current vault file and session, the
/// active screen ("stage"), toasts, dialogs and the shell services. Owns the unlock and
/// lock choreography.
/// </summary>
public sealed class AppViewModel : ObservableObject
{
    private readonly DispatcherTimer _settingsSave = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private object? _stage;
    private StageTransition _transition = StageTransition.Fade;
    private VaultSession? _session;
    private VaultStorage _storage;
    private PaletteViewModel? _palette;

    public AppViewModel(AppPaths paths, SettingsStore store, AppSettings settings)
    {
        Paths = paths;
        Store = store;
        Settings = settings;
        _storage = CreateStorage(ResolveInitialVaultPath());
        _settingsSave.Tick += (_, _) =>
        {
            _settingsSave.Stop();
            Store.TrySave(Settings);
        };

        Clipboard.ClearAfterSeconds = () => Settings.ClipboardClearSeconds;
        Clipboard.ExcludeFromHistory = () => Settings.ClipboardExcludeHistory;
        AutoLock.IdleMinutes = () => Settings.AutoLockMinutes;
        AutoLock.OnWindowsLock = () => Settings.LockOnWindowsLock;
        AutoLock.OnSleep = () => Settings.LockOnSleep;
        AutoLock.LockRequested += reason => _ = LockAsync(reason);
        Dialogs.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasDialog));
        AppServices.App = this;
        AppServices.Clipboard = Clipboard;
    }

    public AppPaths Paths { get; }

    public SettingsStore Store { get; }

    public AppSettings Settings { get; }

    public ClipboardService Clipboard { get; } = new();

    public AutoLockService AutoLock { get; } = new();

    public AutoTypeService AutoType { get; } = new();

    public WindowsHelloService Hello { get; } = new();

    public HotkeyService? Hotkeys { get; set; }

    public TrayIcon? Tray { get; set; }

    public ObservableCollection<ToastViewModel> Toasts { get; } = [];

    public ObservableCollection<DialogViewModel> Dialogs { get; } = [];

    public bool HasDialog => Dialogs.Count > 0;

    public object? Stage
    {
        get => _stage;
        private set
        {
            if (Set(ref _stage, value))
            {
                // Title-bar commands bind through Shell.*: they must follow the stage.
                OnPropertyChanged(nameof(Shell));
            }
        }
    }

    public StageTransition StageTransition
    {
        get => _transition;
        private set => Set(ref _transition, value);
    }

    public VaultSession? Session
    {
        get => _session;
        private set
        {
            if (Set(ref _session, value))
            {
                OnPropertyChanged(nameof(IsUnlocked));
            }
        }
    }

    public bool IsUnlocked => _session is not null;

    public VaultStorage Storage => _storage;

    public ShellViewModel? Shell => Stage as ShellViewModel;

    public PaletteViewModel? Palette
    {
        get => _palette;
        set
        {
            if (Set(ref _palette, value))
            {
                OnPropertyChanged(nameof(HasPalette));
            }
        }
    }

    public bool HasPalette => _palette is not null;

    public string VaultDisplayName
    {
        get
        {
            if (_session is { } session && !string.IsNullOrWhiteSpace(session.Document.Name))
            {
                return session.Document.Name;
            }

            var probe = _storage.Probe();
            if (probe.Header is { } header && Settings.VaultNames.TryGetValue(header.VaultId.ToString("N"), out var name) && !string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            return Path.GetFileNameWithoutExtension(_storage.VaultPath);
        }
    }

    /// <summary>Raised when the main window should come to the front (hotkey, tray, second launch).</summary>
    public event Action? ActivateRequested;

    /// <summary>Raised when the quick-search window should open.</summary>
    public event Action? QuickSearchRequested;

    // ============================================================== startup

    public void Start()
    {
        var probe = _storage.Probe();
        if (probe.Exists && probe.Header is not null)
        {
            Show(new LockViewModel(this, probe), StageTransition.None);
            return;
        }

        if (probe.Exists && probe.IsLegacyFormat)
        {
            Show(new WelcomeViewModel(this, probe.Path), StageTransition.None);
            return;
        }

        Show(new WelcomeViewModel(this, AppPaths.FindLegacyVault()), StageTransition.None);
    }

    private string ResolveInitialVaultPath()
    {
        if (Paths.VaultOverride is { } forced)
        {
            return forced;
        }

        if (!string.IsNullOrWhiteSpace(Settings.LastVaultPath) && File.Exists(Settings.LastVaultPath))
        {
            return Settings.LastVaultPath;
        }

        return Paths.DefaultVaultPath;
    }

    private VaultStorage CreateStorage(string path)
    {
        var isDefault = string.Equals(Path.GetFullPath(path), Path.GetFullPath(Paths.DefaultVaultPath), StringComparison.OrdinalIgnoreCase);
        return new VaultStorage(path, isDefault ? Paths.BackupDirectory : null) { BackupRetention = Settings.BackupRetention };
    }

    public void UseVaultFile(string path)
    {
        _storage = CreateStorage(path);
        OnPropertyChanged(nameof(Storage));
        OnPropertyChanged(nameof(VaultDisplayName));
    }

    // ============================================================= stages

    public void Show(object stage, StageTransition transition = StageTransition.Rise)
    {
        var previous = _stage;
        StageTransition = transition;
        Stage = stage;
        if (previous is IDisposable disposable && !ReferenceEquals(previous, stage))
        {
            disposable.Dispose();
        }
    }

    public void ShowWelcome() => Show(new WelcomeViewModel(this, AppPaths.FindLegacyVault()), StageTransition.Rise);

    public void ShowLock() => Show(new LockViewModel(this, _storage.Probe()), StageTransition.Rise);

    public void ShowCreate() => Show(new CreateVaultViewModel(this), StageTransition.Rise);

    public void ShowMigration(string? legacyPath) => Show(new MigrationViewModel(this, legacyPath), StageTransition.Rise);

    public async Task OpenVaultFileAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = $"{Loc.T("File.VaultFilter")}|*{AppPaths.VaultExtension}|{Loc.T("File.LegacyFilter")}|*.plf|{Loc.T("File.AllFilter")}|*.*",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        if (Path.GetExtension(dialog.FileName).Equals(".plf", StringComparison.OrdinalIgnoreCase))
        {
            ShowMigration(dialog.FileName);
            return;
        }

        var storage = CreateStorage(dialog.FileName);
        var probe = storage.Probe();
        if (probe.IsLegacyFormat)
        {
            ShowMigration(dialog.FileName);
            return;
        }

        if (probe.Header is null)
        {
            await AlertAsync(Loc.T("Error.NotAVault.Title"), Loc.T("Error.NotAVault"));
            return;
        }

        if (IsUnlocked)
        {
            await LockAsync(null);
        }

        UseVaultFile(dialog.FileName);
        Show(new LockViewModel(this, probe), StageTransition.Rise);
    }

    // ======================================================= unlock / lock

    public void OnUnlocked(VaultSession session, bool fromCreation = false)
    {
        Session = session;
        session.KeepPasswordHistory = Settings.KeepPasswordHistory;
        var id = session.VaultId.ToString("N");

        if (Settings.HighestWriteCounters.TryGetValue(id, out var highest) && highest > session.WriteCounter)
        {
            session.Document.AddAudit(AuditKind.RollbackDetected, $"{session.WriteCounter} < {highest}");
            Toast(Loc.T("Rollback.Title"), Loc.T("Rollback.Detail"), ToastKind.Warning, TimeSpan.FromSeconds(12));
        }

        Settings.HighestWriteCounters[id] = Math.Max(highest, session.WriteCounter);
        Settings.VaultNames[id] = session.Document.Name;
        Settings.LastVaultPath = session.Path;
        Settings.RecentVaults.RemoveAll(p => string.Equals(p, session.Path, StringComparison.OrdinalIgnoreCase));
        Settings.RecentVaults.Insert(0, session.Path);
        if (Settings.RecentVaults.Count > 8)
        {
            Settings.RecentVaults.RemoveRange(8, Settings.RecentVaults.Count - 8);
        }

        Settings.LastUnlock = DateTimeOffset.Now;
        Settings.FailedUnlocks = 0;
        Settings.FirstRunCompleted = true;
        SaveSettings();

        session.SaveCompleted += OnSaveCompleted;
        if (!fromCreation)
        {
            session.Document.AddAudit(session.UnlockedWith switch
            {
                UnlockMethod.RecoveryKey => AuditKind.UnlockedWithRecovery,
                UnlockMethod.WindowsHello => AuditKind.UnlockedWithHello,
                _ => AuditKind.Unlocked,
            });
            session.RequestSaveDebounced(TimeSpan.FromSeconds(3));
        }

        session.PurgeExpiredTrash(TimeSpan.FromDays(Settings.TrashRetentionDays));
        Show(new ShellViewModel(this, session), StageTransition.SealOpen);
        AutoLock.Arm();

        var preferred = KdfParameters.FromStrength(StrengthOf(Settings.KdfStrength));
        if (session.PasswordKdf is { } kdf && kdf.IsWeakerThan(preferred) && !fromCreation)
        {
            Toast(Loc.T("Kdf.UpgradeAvailable"), Loc.F("Kdf.UpgradeDetail", kdf.Describe(), preferred.Describe()), ToastKind.Info, TimeSpan.FromSeconds(9));
        }
    }

    public static KdfStrength StrengthOf(string value) => value switch
    {
        "standard" => KdfStrength.Standard,
        "extreme" => KdfStrength.Extreme,
        _ => KdfStrength.High,
    };

    private void OnSaveCompleted(object? sender, SaveResult result)
    {
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            if (sender is not VaultSession session)
            {
                return;
            }

            var id = session.VaultId.ToString("N");
            if (result.Success)
            {
                Settings.HighestWriteCounters[id] = Math.Max(Settings.HighestWriteCounters.GetValueOrDefault(id), result.WriteCounter);
                Settings.VaultNames[id] = session.Document.Name;
                SaveSettings();
            }
            else if (!session.IsDisposed)
            {
                Toast(Loc.T("Save.Failed"), Errors.Describe(result.Error), ToastKind.Error, TimeSpan.FromSeconds(10), Loc.T("Save.Retry"), new RelayCommand(() => session.RequestSave()));
            }

            Shell?.OnSaveStateChanged();
        });
    }

    public async Task LockAsync(string? reasonKey)
    {
        var session = _session;
        if (session is null)
        {
            return;
        }

        Session = null;
        AutoLock.Disarm();
        Palette = null;
        foreach (var dialog in Dialogs.ToList())
        {
            dialog.Close(null);
        }

        Dialogs.Clear();
        Clipboard.ClearIfOurs();
        session.SaveCompleted -= OnSaveCompleted;
        Show(new LockViewModel(this, _storage.Probe()) { JustLocked = true }, StageTransition.SealClose);
        try
        {
            await session.CloseAsync(TimeSpan.FromSeconds(2));
        }
        catch (Exception error)
        {
            AppErrors.Report(error);
        }

        if (reasonKey is not null)
        {
            Toast(Loc.T("Lock.Locked"), Loc.T(reasonKey), ToastKind.Info);
        }
    }

    // ============================================================ feedback

    public void Toast(string title, string? detail = null, ToastKind kind = ToastKind.Info, TimeSpan? duration = null, string? actionText = null, System.Windows.Input.ICommand? action = null)
    {
        var toast = new ToastViewModel(title, detail, kind, duration ?? TimeSpan.FromSeconds(kind == ToastKind.Error ? 8 : 4.5), Dismiss)
        {
            ActionText = actionText,
            ActionCommand = action,
        };
        Toasts.Add(toast);
        while (Toasts.Count > 4)
        {
            Dismiss(Toasts[0]);
        }
    }

    private void Dismiss(ToastViewModel toast)
    {
        toast.Stop();
        Toasts.Remove(toast);
    }

    public Task<object?> ShowDialog(DialogViewModel dialog)
    {
        dialog.Closed += closed => Dialogs.Remove(closed);
        Dialogs.Add(dialog);
        return dialog.Result;
    }

    public async Task<bool> ConfirmAsync(string title, string message, string? confirmText = null, bool danger = false, string iconKey = "Icon.Info")
    {
        var result = await ShowDialog(new ConfirmDialogViewModel
        {
            Title = title,
            Message = message,
            ConfirmText = confirmText ?? Loc.T("Common.Confirm"),
            IsDanger = danger,
            IconKey = danger ? "Icon.Warning" : iconKey,
        });
        return result is true;
    }

    public Task AlertAsync(string title, string message) => ShowDialog(new ConfirmDialogViewModel
    {
        Title = title,
        Message = message,
        ConfirmText = Loc.T("Common.Ok"),
        ShowCancel = false,
        IconKey = "Icon.Info",
    });

    public async Task<SecretDraft?> AskPasswordAsync(string title, string message, string? confirmText = null, bool confirm = false, bool strength = false)
    {
        var result = await ShowDialog(new PasswordDialogViewModel(confirm, strength)
        {
            Title = title,
            Message = message,
            ConfirmText = confirmText ?? Loc.T("Common.Continue"),
            IconKey = "Icon.Key",
        });
        return result as SecretDraft;
    }

    public async Task<string?> AskTextAsync(string title, string label, string initial = "", string? placeholder = null, bool multiline = false)
    {
        var result = await ShowDialog(new TextInputDialogViewModel
        {
            Title = title,
            Label = label,
            Text = initial,
            Placeholder = placeholder ?? string.Empty,
            Multiline = multiline,
            IconKey = "Icon.Edit",
        });
        return result as string;
    }

    public void SaveSettings()
    {
        _settingsSave.Stop();
        _settingsSave.Start();
    }

    public void FlushSettings()
    {
        _settingsSave.Stop();
        Store.TrySave(Settings);
    }

    // ======================================================= window events

    public void RequestActivate() => ActivateRequested?.Invoke();

    public void RequestQuickSearch()
    {
        if (IsUnlocked)
        {
            QuickSearchRequested?.Invoke();
        }
        else
        {
            RequestActivate();
        }
    }

    public void OpenPalette()
    {
        if (Shell is { } shell)
        {
            Palette = new PaletteViewModel(this, shell);
        }
    }

    public void ClosePalette() => Palette = null;

    /// <summary>Global auto-type: finds the entry matching the focused window and types it.</summary>
    public async Task GlobalAutoTypeAsync()
    {
        if (Shell is not { } shell)
        {
            RequestActivate();
            return;
        }

        var target = Interop.Native.GetForegroundWindow();
        var title = AutoTypeService.WindowTitle(target);
        var match = shell.FindForWindow(title);
        if (match is null)
        {
            Toast(Loc.T("AutoType.NoMatch"), title, ToastKind.Warning);
            QuickSearchRequested?.Invoke();
            return;
        }

        await shell.AutoTypeAsync(match, target);
    }

    // ============================================================ shutdown

    public async Task ShutdownAsync()
    {
        AutoLock.Disarm();
        var session = _session;
        Session = null;
        if (session is not null)
        {
            try
            {
                await session.CloseAsync(TimeSpan.FromMilliseconds(1500));
            }
            catch (Exception error)
            {
                AppErrors.Report(error);
            }
        }

        Clipboard.ClearIfOurs();
        (Stage as IDisposable)?.Dispose();
        FlushSettings();
        AutoLock.Dispose();
        AutoType.Dispose();
        Hotkeys?.Dispose();
        Tray?.Dispose();
        SecureMemory.WipeAll();
    }
}

/// <summary>Maps vault errors to localized, actionable text.</summary>
public static class Errors
{
    public static string Describe(Exception? error) => error switch
    {
        VaultException vault => Loc.T("VaultError." + vault.Error),
        UnauthorizedAccessException => Loc.T("VaultError.AccessDenied"),
        IOException => Loc.T("VaultError.IoFailure"),
        OperationCanceledException => Loc.T("VaultError.Cancelled"),
        null => Loc.T("VaultError.IoFailure"),
        _ => error.Message,
    };
}
