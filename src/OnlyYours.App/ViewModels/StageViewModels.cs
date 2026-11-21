using System.IO;
using System.Windows.Input;
using System.Windows.Threading;
using OnlyYours.App.Controls;
using OnlyYours.App.Infrastructure;
using OnlyYours.Core.Analysis;
using OnlyYours.Core.Legacy;
using OnlyYours.Core.Platform;
using OnlyYours.Core.Security.Crypto;
using OnlyYours.Core.Security.Memory;
using OnlyYours.Core.Settings;
using OnlyYours.Core.Vault;

namespace OnlyYours.App.ViewModels;

/// <summary>First run: create a vault, bring over BetterOnlyYours, or open an existing file.</summary>
public sealed class WelcomeViewModel : ObservableObject
{
    private readonly AppViewModel _app;

    public WelcomeViewModel(AppViewModel app, string? legacyPath)
    {
        _app = app;
        LegacyPath = legacyPath;
        if (legacyPath is not null)
        {
            var info = File.Exists(legacyPath) ? new FileInfo(legacyPath) : new FileInfo(legacyPath + ".bak");
            if (info.Exists)
            {
                LegacyDetail = Loc.F("Welcome.LegacyFound", info.FullName, Loc.Bytes(info.Length), Loc.Date(info.LastWriteTime));
            }

            LegacyHeader = LegacyVault.TryReadHeader(info.FullName)?.Describe();
        }

        CreateCommand = new RelayCommand(app.ShowCreate);
        MigrateCommand = new RelayCommand(() => app.ShowMigration(LegacyPath));
        OpenCommand = new AsyncCommand(app.OpenVaultFileAsync);
    }

    public string? LegacyPath { get; }

    public bool HasLegacy => LegacyPath is not null;

    public string? LegacyDetail { get; }

    public string? LegacyHeader { get; }

    public ICommand CreateCommand { get; }

    public ICommand MigrateCommand { get; }

    public ICommand OpenCommand { get; }
}

/// <summary>The lock screen: master password, Windows Hello or recovery key.</summary>
public sealed class LockViewModel : ObservableObject, IDisposable
{
    private readonly AppViewModel _app;
    private readonly VaultProbe _probe;
    private readonly DispatcherTimer _throttle = new() { Interval = TimeSpan.FromSeconds(1) };
    private SecretDraft _password = new();
    private SecretDraft _recovery = new();
    private bool _busy;
    private string? _error;
    private bool _useRecovery;
    private bool _helloAvailable;
    private int _cooldown;

    public LockViewModel(AppViewModel app, VaultProbe probe)
    {
        _app = app;
        _probe = probe;
        UnlockCommand = new AsyncCommand(UnlockAsync, () => !_busy && _cooldown == 0);
        HelloCommand = new AsyncCommand(HelloAsync, () => !_busy && HelloAvailable);
        ToggleRecoveryCommand = new RelayCommand(() =>
        {
            UseRecovery = !UseRecovery;
            Error = null;
        });
        OpenOtherCommand = new AsyncCommand(app.OpenVaultFileAsync);
        CreateNewCommand = new RelayCommand(app.ShowCreate);
        _throttle.Tick += (_, _) =>
        {
            if (_cooldown > 0)
            {
                _cooldown--;
                OnPropertyChanged(nameof(CooldownText));
                OnPropertyChanged(nameof(IsCoolingDown));
            }

            if (_cooldown == 0)
            {
                _throttle.Stop();
                CommandManager.InvalidateRequerySuggested();
            }
        };
        _ = DetectHelloAsync();
        StartCooldownIfNeeded();
    }

    public event Action? UnlockStarted;

    public event Action? UnlockFailed;

    public event Action? UnlockSucceeded;

    public bool JustLocked { get; init; }

    public SecretDraft Password => _password;

    public SecretDraft Recovery => _recovery;

    public string VaultName => _app.VaultDisplayName;

    public string VaultPath => _probe.Path;

    public string Details
    {
        get
        {
            var parts = new List<string>();
            if (_probe.Header?.PasswordSlot?.Kdf is { } kdf)
            {
                parts.Add(kdf.Describe());
            }

            parts.Add("ChaCha20-Poly1305 + AES-256-GCM");
            if (_probe.Modified is { } modified)
            {
                parts.Add(Loc.F("Lock.Modified", Loc.Ago(modified)));
            }

            return string.Join("  ·  ", parts);
        }
    }

    public string? LastUnlockText => _app.Settings.LastUnlock is { } last ? Loc.F("Lock.LastUnlock", Loc.Ago(last)) : null;

    public bool HasRecoverySlot => _probe.Header?.RecoverySlot is not null;

    public bool HasHelloSlot => _probe.Header?.HelloSlot is not null;

    public bool HelloAvailable
    {
        get => _helloAvailable;
        private set => Set(ref _helloAvailable, value);
    }

    public bool IsBusy
    {
        get => _busy;
        private set => Set(ref _busy, value);
    }

    public string? Error
    {
        get => _error;
        private set
        {
            if (Set(ref _error, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrEmpty(_error);

    public bool UseRecovery
    {
        get => _useRecovery;
        set => Set(ref _useRecovery, value);
    }

    public bool IsCoolingDown => _cooldown > 0;

    public string CooldownText => Loc.F("Lock.Cooldown", _cooldown);

    public string FailedText => _app.Settings.FailedUnlocks > 0 ? Loc.F("Lock.Failed", _app.Settings.FailedUnlocks) : string.Empty;

    public ICommand UnlockCommand { get; }

    public ICommand HelloCommand { get; }

    public ICommand ToggleRecoveryCommand { get; }

    public ICommand OpenOtherCommand { get; }

    public ICommand CreateNewCommand { get; }

    private async Task DetectHelloAsync()
    {
        if (!HasHelloSlot)
        {
            return;
        }

        HelloAvailable = await _app.Hello.IsAvailableAsync();
    }

    private void StartCooldownIfNeeded()
    {
        // After five failures each attempt is delayed exponentially (2 s, 4 s … 60 s).
        var failures = _app.Settings.FailedUnlocks;
        if (failures >= 5)
        {
            _cooldown = (int)Math.Min(60, Math.Pow(2, failures - 4));
            _throttle.Start();
        }
    }

    private async Task UnlockAsync()
    {
        var draft = UseRecovery ? _recovery : _password;
        if (draft.IsEmpty)
        {
            Error = Loc.T(UseRecovery ? "Lock.EnterRecovery" : "Lock.EnterPassword");
            UnlockFailed?.Invoke();
            return;
        }

        await RunUnlock(() => UseRecovery
            ? VaultSession.OpenWithRecoveryAsync(_app.Storage, _recovery)
            : VaultSession.OpenAsync(_app.Storage, _password));
    }

    private Task HelloAsync() => RunUnlock(() => VaultSession.OpenWithHelloAsync(_app.Storage, _app.Hello));

    private async Task RunUnlock(Func<Task<VaultSession>> open)
    {
        IsBusy = true;
        Error = null;
        UnlockStarted?.Invoke();
        var started = DateTime.UtcNow;
        try
        {
            var session = await open();
            _password.Clear();
            _recovery.Clear();
            UnlockSucceeded?.Invoke();

            // Let the dial finish turning before the vault door opens.
            var minimum = Motion.Time(560);
            var elapsed = DateTime.UtcNow - started;
            if (elapsed < minimum)
            {
                await Task.Delay(minimum - elapsed);
            }

            _app.OnUnlocked(session);
            if (session.UnlockedWith == UnlockMethod.RecoveryKey)
            {
                _app.Toast(Loc.T("Lock.RecoveryUsed"), Loc.T("Lock.RecoveryUsedDetail"), ToastKind.Warning, TimeSpan.FromSeconds(10));
            }
        }
        catch (VaultException error) when (error.Error == VaultError.Cancelled)
        {
            Error = null;
            UnlockFailed?.Invoke();
        }
        catch (Exception error)
        {
            if (error is VaultException { Error: VaultError.InvalidPassword or VaultError.InvalidRecoveryKey })
            {
                _app.Settings.FailedUnlocks++;
                _app.SaveSettings();
                OnPropertyChanged(nameof(FailedText));
                StartCooldownIfNeeded();
            }

            Error = Errors.Describe(error);
            UnlockFailed?.Invoke();
        }
        finally
        {
            IsBusy = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public void Dispose()
    {
        _throttle.Stop();
        _password.Dispose();
        _recovery.Dispose();
    }
}

/// <summary>Creating a new vault: name and master password → recovery kit → Windows Hello → done.</summary>
public sealed class CreateVaultViewModel : ObservableObject, IDisposable, ISecretSource
{
    private readonly AppViewModel _app;
    private readonly SecretDraft _password = new();
    private readonly SecretDraft _confirm = new();
    private string _name;
    private int _step;
    private bool _busy;
    private string? _error;
    private StrengthReport _strength = StrengthReport.Empty;
    private KdfStrength _kdf = KdfStrength.High;
    private SecureCharBuffer? _recoveryCode;
    private VaultSession? _session;
    private bool _acknowledged;
    private bool _helloAvailable;
    private bool _codeRevealed = true;

    public CreateVaultViewModel(AppViewModel app)
    {
        _app = app;
        _name = Loc.T("Create.DefaultName");
        _kdf = AppViewModel.StrengthOf(app.Settings.KdfStrength);
        _password.Changed += (_, _) => UpdateStrength();
        _confirm.Changed += (_, _) => OnPropertyChanged(nameof(ConfirmState));
        CreateCommand = new AsyncCommand(CreateAsync, () => !_busy);
        BackCommand = new RelayCommand(() =>
        {
            if (_step == 0)
            {
                if (_app.Storage.Exists)
                {
                    _app.ShowLock();
                }
                else
                {
                    _app.ShowWelcome();
                }
            }
        });
        CopyCodeCommand = new RelayCommand(() =>
        {
            if (_recoveryCode is not null)
            {
                _app.Clipboard.CopySecret(_recoveryCode.Span, Loc.T("Clipboard.RecoveryKey"));
                _app.Toast(Loc.T("Toast.Copied"), Loc.T("Clipboard.RecoveryKey"), ToastKind.Success);
            }
        });
        SaveCodeCommand = new RelayCommand(SaveCode);
        ContinueCommand = new AsyncCommand(ContinueAsync, () => _step != 1 || _acknowledged);
        EnableHelloCommand = new AsyncCommand(EnableHelloAsync);
        FinishCommand = new RelayCommand(Finish);
        _ = DetectHello();
    }

    public SecretDraft Password => _password;

    public SecretDraft Confirm => _confirm;

    public string Name
    {
        get => _name;
        set => Set(ref _name, value);
    }

    /// <summary>0 = credentials, 1 = recovery kit, 2 = Windows Hello, 3 = done.</summary>
    public int Step
    {
        get => _step;
        private set
        {
            if (Set(ref _step, value))
            {
                OnPropertyChanged(nameof(StepTitle));
            }
        }
    }

    public string StepTitle => Loc.T($"Create.Step{_step}");

    public bool IsBusy
    {
        get => _busy;
        private set => Set(ref _busy, value);
    }

    public string? Error
    {
        get => _error;
        private set
        {
            if (Set(ref _error, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrEmpty(_error);

    public int StrengthLevel => (int)_strength.Level;

    public string StrengthText => _strength.Level == Core.Analysis.StrengthLevel.Empty ? Loc.T("Create.StrengthHint") : Loc.T("Strength." + _strength.Level);

    public string StrengthAdvice => _strength.Hints.Count == 0 ? string.Empty : string.Join("  ", _strength.Hints.Take(2).Select(h => Loc.T("Hint." + h)));

    public string CrackText => _strength.Level == Core.Analysis.StrengthLevel.Empty
        ? string.Empty
        : Loc.F("Create.CrackTime", Loc.Duration(_strength.SecondsToCrack(KdfGuessRate(KdfParameters.FromStrength(_kdf)))));

    public string ConfirmState => _confirm.IsEmpty ? string.Empty : _password.ContentEquals(_confirm) ? Loc.T("Create.Match") : Loc.T("Create.NoMatch");

    public bool ConfirmMatches => !_confirm.IsEmpty && _password.ContentEquals(_confirm);

    public KdfStrength Kdf
    {
        get => _kdf;
        set
        {
            if (Set(ref _kdf, value))
            {
                OnPropertyChanged(nameof(KdfDescription));
                OnPropertyChanged(nameof(CrackText));
            }
        }
    }

    public string KdfDescription => KdfParameters.FromStrength(_kdf).Describe();

    public bool Acknowledged
    {
        get => _acknowledged;
        set
        {
            if (Set(ref _acknowledged, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool CodeRevealed
    {
        get => _codeRevealed;
        set => Set(ref _codeRevealed, value);
    }

    public bool HelloAvailable
    {
        get => _helloAvailable;
        private set => Set(ref _helloAvailable, value);
    }

    public bool HelloEnabled => _session?.HasHello == true;

    public ICommand CreateCommand { get; }

    public ICommand BackCommand { get; }

    public ICommand CopyCodeCommand { get; }

    public ICommand SaveCodeCommand { get; }

    public ICommand ContinueCommand { get; }

    public ICommand EnableHelloCommand { get; }

    public ICommand FinishCommand { get; }

    public SecureCharBuffer? OpenSecret() => _recoveryCode is { IsDisposed: false } code ? SecureCharBuffer.CopyOf(code.Span) : null;

    /// <summary>Guesses per second a well-funded attacker achieves against these Argon2id settings.</summary>
    public static double KdfGuessRate(KdfParameters kdf) => 2.0e6 / Math.Max(1, kdf.MemoryMiB * kdf.Iterations);

    private async Task DetectHello() => HelloAvailable = await _app.Hello.IsAvailableAsync();

    private void UpdateStrength()
    {
        using (var lease = _password.Read())
        {
            _strength = PasswordStrength.Evaluate(lease.Chars);
        }

        OnPropertyChanged(nameof(StrengthLevel));
        OnPropertyChanged(nameof(StrengthText));
        OnPropertyChanged(nameof(StrengthAdvice));
        OnPropertyChanged(nameof(CrackText));
        OnPropertyChanged(nameof(ConfirmState));
        OnPropertyChanged(nameof(ConfirmMatches));
    }

    private async Task CreateAsync()
    {
        Error = null;
        if (string.IsNullOrWhiteSpace(Name))
        {
            Error = Loc.T("Create.NameRequired");
            return;
        }

        if (_password.Length < 8)
        {
            Error = Loc.T("Create.TooShort");
            return;
        }

        if (!_password.ContentEquals(_confirm))
        {
            Error = Loc.T("Error.PasswordMismatch");
            return;
        }

        if (_strength.Level <= Core.Analysis.StrengthLevel.Weak &&
            !await _app.ConfirmAsync(Loc.T("Create.WeakTitle"), Loc.T("Create.WeakMessage"), Loc.T("Create.UseAnyway"), danger: true))
        {
            return;
        }

        IsBusy = true;
        try
        {
            if (_app.Storage.Exists)
            {
                var path = UniquePath(_app.Paths.DataDirectory, Name);
                _app.UseVaultFile(path);
            }

            var (session, code) = await VaultSession.CreateAsync(_app.Storage, Name.Trim(), _password, KdfParameters.FromStrength(_kdf));
            _session = session;
            _recoveryCode = code;
            _password.Clear();
            _confirm.Clear();
            _app.Settings.KdfStrength = _kdf.ToString().ToLowerInvariant();
            OnPropertyChanged(nameof(Version));
            Step = 1;
        }
        catch (Exception error)
        {
            Error = Errors.Describe(error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    internal static string UniquePath(string directory, string name)
    {
        var safe = string.Concat(name.Where(c => !Path.GetInvalidFileNameChars().Contains(c))).Trim();
        if (safe.Length == 0)
        {
            safe = "vault";
        }

        var path = Path.Combine(directory, safe + AppPaths.VaultExtension);
        var index = 2;
        while (File.Exists(path))
        {
            path = Path.Combine(directory, $"{safe} ({index++}){AppPaths.VaultExtension}");
        }

        return path;
    }

    /// <summary>Bumped when the recovery code changes so the secret view reloads.</summary>
    public int Version => _recoveryCode is null ? 0 : 1;

    private void SaveCode()
    {
        if (_recoveryCode is null)
        {
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog { FileName = Loc.T("Recovery.FileName"), DefaultExt = ".txt", Filter = "Text|*.txt" };
        if (dialog.ShowDialog() == true)
        {
            RecoveryKit.Write(dialog.FileName, _recoveryCode, Name);
            _app.Toast(Loc.T("Recovery.Saved"), dialog.FileName, ToastKind.Success);
        }
    }

    private Task ContinueAsync()
    {
        if (_step == 1)
        {
            _recoveryCode?.Dispose();
            _recoveryCode = null;
            Step = HelloAvailable ? 2 : 3;
        }
        else if (_step == 2)
        {
            Step = 3;
        }

        return Task.CompletedTask;
    }

    private async Task EnableHelloAsync()
    {
        if (_session is null)
        {
            return;
        }

        try
        {
            await _session.EnableHelloAsync(_app.Hello);
            OnPropertyChanged(nameof(HelloEnabled));
            Step = 3;
        }
        catch (Exception error)
        {
            Error = Errors.Describe(error);
        }
    }

    private void Finish()
    {
        if (_session is null)
        {
            return;
        }

        var session = _session;
        _session = null;
        _app.OnUnlocked(session, fromCreation: true);
        _app.Toast(Loc.T("Create.Welcome"), Loc.T("Create.WelcomeDetail"), ToastKind.Success);
    }

    public void Dispose()
    {
        _password.Dispose();
        _confirm.Dispose();
        _recoveryCode?.Dispose();
        _session?.Dispose();
    }
}

/// <summary>Migration from BetterOnlyYours: decrypt the legacy file, convert, re-encrypt with OnlyYours' crypto.</summary>
public sealed class MigrationViewModel : ObservableObject, IDisposable, ISecretSource
{
    private readonly AppViewModel _app;
    private readonly SecretDraft _legacyPassword = new();
    private readonly SecretDraft _newPassword = new();
    private readonly SecretDraft _confirm = new();
    private string? _path;
    private int _step;
    private bool _busy;
    private string? _error;
    private LegacyPayload? _payload;
    private bool _reusePassword = true;
    private KdfStrength _kdf = KdfStrength.High;
    private MigrationReport? _report;
    private SecureCharBuffer? _recoveryCode;
    private VaultSession? _session;
    private bool _acknowledged;
    private string _progressText = string.Empty;
    private int _progressStage;

    public MigrationViewModel(AppViewModel app, string? path)
    {
        _app = app;
        _path = path ?? AppPaths.FindLegacyVault();
        _kdf = AppViewModel.StrengthOf(app.Settings.KdfStrength);
        BrowseCommand = new RelayCommand(Browse);
        DecryptCommand = new AsyncCommand(DecryptAsync, () => !_busy && _path is not null);
        MigrateCommand = new AsyncCommand(MigrateAsync, () => !_busy);
        BackCommand = new RelayCommand(() =>
        {
            if (_step == 0 || _step == 1)
            {
                _app.ShowWelcome();
            }
        });
        CopyCodeCommand = new RelayCommand(() =>
        {
            if (_recoveryCode is not null)
            {
                _app.Clipboard.CopySecret(_recoveryCode.Span, Loc.T("Clipboard.RecoveryKey"));
                _app.Toast(Loc.T("Toast.Copied"), Loc.T("Clipboard.RecoveryKey"), ToastKind.Success);
            }
        });
        SaveCodeCommand = new RelayCommand(SaveCode);
        FinishCommand = new RelayCommand(Finish, () => _acknowledged);
        _newPassword.Changed += (_, _) => OnPropertyChanged(nameof(StrengthLevel));
    }

    public SecretDraft LegacyPassword => _legacyPassword;

    public SecretDraft NewPassword => _newPassword;

    public SecretDraft Confirm => _confirm;

    public string? SourcePath
    {
        get => _path;
        private set
        {
            if (Set(ref _path, value))
            {
                OnPropertyChanged(nameof(SourceDetail));
                OnPropertyChanged(nameof(HasSource));
            }
        }
    }

    public bool HasSource => _path is not null;

    public string SourceDetail
    {
        get
        {
            if (_path is null)
            {
                return Loc.T("Migrate.NoSource");
            }

            var header = LegacyVault.TryReadHeader(File.Exists(_path) ? _path : _path + ".bak");
            var info = new FileInfo(File.Exists(_path) ? _path : _path + ".bak");
            return info.Exists ? $"{Loc.Bytes(info.Length)} · {Loc.Date(info.LastWriteTime)}{(header is null ? string.Empty : " · " + header.Describe())}" : Loc.T("Migrate.NoSource");
        }
    }

    /// <summary>0 = source + password, 1 = new protection, 2 = migrating, 3 = report + recovery kit.</summary>
    public int Step
    {
        get => _step;
        private set => Set(ref _step, value);
    }

    public bool IsBusy
    {
        get => _busy;
        private set => Set(ref _busy, value);
    }

    public string? Error
    {
        get => _error;
        private set
        {
            if (Set(ref _error, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrEmpty(_error);

    public bool ReusePassword
    {
        get => _reusePassword;
        set => Set(ref _reusePassword, value);
    }

    public KdfStrength Kdf
    {
        get => _kdf;
        set
        {
            if (Set(ref _kdf, value))
            {
                OnPropertyChanged(nameof(KdfDescription));
            }
        }
    }

    public string KdfDescription => KdfParameters.FromStrength(_kdf).Describe();

    public int StrengthLevel
    {
        get
        {
            using var lease = _newPassword.Read();
            return (int)PasswordStrength.Evaluate(lease.Chars).Level;
        }
    }

    public string LegacyDescription => _payload?.Header.Describe() ?? string.Empty;

    public MigrationReport? Report
    {
        get => _report;
        private set => Set(ref _report, value);
    }

    public string ProgressText
    {
        get => _progressText;
        private set => Set(ref _progressText, value);
    }

    public int ProgressStage
    {
        get => _progressStage;
        private set => Set(ref _progressStage, value);
    }

    public bool Acknowledged
    {
        get => _acknowledged;
        set
        {
            if (Set(ref _acknowledged, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public int Version => _recoveryCode is null ? 0 : 1;

    public ICommand BrowseCommand { get; }

    public ICommand DecryptCommand { get; }

    public ICommand MigrateCommand { get; }

    public ICommand BackCommand { get; }

    public ICommand CopyCodeCommand { get; }

    public ICommand SaveCodeCommand { get; }

    public ICommand FinishCommand { get; }

    public SecureCharBuffer? OpenSecret() => _recoveryCode is { IsDisposed: false } code ? SecureCharBuffer.CopyOf(code.Span) : null;

    private void Browse()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = $"{Loc.T("File.LegacyFilter")}|*.plf;*.plf.bak|{Loc.T("File.AllFilter")}|*.*", CheckFileExists = true };
        if (dialog.ShowDialog() == true)
        {
            SourcePath = dialog.FileName.EndsWith(".bak", StringComparison.OrdinalIgnoreCase) ? dialog.FileName[..^4] : dialog.FileName;
            Error = null;
        }
    }

    private async Task DecryptAsync()
    {
        if (_path is null)
        {
            return;
        }

        if (_legacyPassword.IsEmpty)
        {
            Error = Loc.T("Migrate.EnterPassword");
            return;
        }

        IsBusy = true;
        Error = null;
        try
        {
            _payload?.Dispose();
            _payload = await LegacyVault.OpenAsync(_path, _legacyPassword);
            OnPropertyChanged(nameof(LegacyDescription));
            Step = 1;
        }
        catch (Exception error)
        {
            Error = error is VaultException { Error: VaultError.InvalidPassword } ? Loc.T("Migrate.WrongPassword") : Errors.Describe(error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task MigrateAsync()
    {
        if (_payload is null)
        {
            Step = 0;
            return;
        }

        var password = _reusePassword ? _legacyPassword : _newPassword;
        if (!_reusePassword)
        {
            if (_newPassword.Length < 8)
            {
                Error = Loc.T("Create.TooShort");
                return;
            }

            if (!_newPassword.ContentEquals(_confirm))
            {
                Error = Loc.T("Error.PasswordMismatch");
                return;
            }
        }

        IsBusy = true;
        Error = null;
        Step = 2;
        try
        {
            ProgressStage = 1;
            ProgressText = Loc.T("Migrate.Progress1");
            await Task.Delay(Motion.Time(450));

            var name = Loc.T("Migrate.VaultName");
            if (_app.Storage.Exists)
            {
                _app.UseVaultFile(CreateVaultViewModel.UniquePath(_app.Paths.DataDirectory, name));
            }

            ProgressStage = 2;
            ProgressText = Loc.T("Migrate.Progress2");
            var migrator = new LegacyMigrator();
            var payload = _payload;
            var (session, code) = await VaultSession.CreateFromDocumentAsync(
                _app.Storage,
                new Core.Vault.Model.VaultDocument(),
                (cipher, _) => migrator.Build(payload!, cipher, name),
                password,
                KdfParameters.FromStrength(_kdf));

            ProgressStage = 3;
            ProgressText = Loc.T("Migrate.Progress3");
            var settingsPath = AppPaths.LegacySettingsPath(_path!);
            if (settingsPath is not null)
            {
                LegacySettings.Apply(settingsPath, _app.Settings);
                Motion.Level = Motion.Parse(_app.Settings.Motion);
            }

            _app.Settings.KdfStrength = _kdf.ToString().ToLowerInvariant();
            _app.Settings.LegacyMigrationDismissed = true;
            _app.SaveSettings();
            await Task.Delay(Motion.Time(400));

            _session = session;
            _recoveryCode = code;
            Report = migrator.Report;
            _payload.Dispose();
            _payload = null;
            _legacyPassword.Clear();
            _newPassword.Clear();
            _confirm.Clear();
            OnPropertyChanged(nameof(Version));
            Step = 3;
        }
        catch (Exception error)
        {
            Error = Errors.Describe(error);
            Step = 1;
        }
        finally
        {
            IsBusy = false;
            ProgressStage = 0;
        }
    }

    private void SaveCode()
    {
        if (_recoveryCode is null)
        {
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog { FileName = Loc.T("Recovery.FileName"), DefaultExt = ".txt", Filter = "Text|*.txt" };
        if (dialog.ShowDialog() == true)
        {
            RecoveryKit.Write(dialog.FileName, _recoveryCode, Loc.T("Migrate.VaultName"));
            _app.Toast(Loc.T("Recovery.Saved"), dialog.FileName, ToastKind.Success);
        }
    }

    private void Finish()
    {
        if (_session is null)
        {
            return;
        }

        var session = _session;
        _session = null;
        _recoveryCode?.Dispose();
        _recoveryCode = null;
        _app.OnUnlocked(session, fromCreation: true);
        _app.Toast(Loc.T("Migrate.Done"), Loc.F("Migrate.DoneDetail", Report?.Entries ?? 0), ToastKind.Success);
    }

    public void Dispose()
    {
        _legacyPassword.Dispose();
        _newPassword.Dispose();
        _confirm.Dispose();
        _payload?.Dispose();
        _recoveryCode?.Dispose();
        _session?.Dispose();
    }
}
