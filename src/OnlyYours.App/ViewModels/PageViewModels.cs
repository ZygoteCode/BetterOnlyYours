using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using OnlyYours.App.Controls;
using OnlyYours.App.Infrastructure;
using OnlyYours.App.Services;
using OnlyYours.Core.Analysis;
using OnlyYours.Core.Generators;
using OnlyYours.Core.ImportExport;
using OnlyYours.Core.Legacy;
using OnlyYours.Core.Platform;
using OnlyYours.Core.Security.Crypto;
using OnlyYours.Core.Security.Memory;
using OnlyYours.Core.Settings;
using OnlyYours.Core.Sharing;
using OnlyYours.Core.Vault;
using OnlyYours.Core.Vault.Model;

namespace OnlyYours.App.ViewModels;

// ================================================================== generator

public sealed class GeneratedItemViewModel : ObservableObject, ISecretSource, IDisposable
{
    private readonly SecureCharBuffer _value;
    private bool _revealed;

    public GeneratedItemViewModel(SecureCharBuffer value, double entropy, AppViewModel app)
    {
        _value = value;
        Entropy = Loc.F("Generator.Bits", Math.Round(entropy));
        When = DateTime.Now.ToString("HH:mm:ss", Loc.Culture);
        CopyCommand = new RelayCommand(() =>
        {
            app.Clipboard.CopySecret(_value.Span, Loc.T("Clipboard.Generated"));
            app.Toast(Loc.T("Toast.Copied"), Loc.T("Clipboard.Generated"), ToastKind.Success, TimeSpan.FromSeconds(2.5));
        });
        RevealCommand = new RelayCommand(() => IsRevealed = !IsRevealed);
    }

    public string Entropy { get; }

    public string When { get; }

    public bool IsRevealed
    {
        get => _revealed;
        set => Set(ref _revealed, value);
    }

    public ICommand CopyCommand { get; }

    public ICommand RevealCommand { get; }

    public SecureCharBuffer? OpenSecret() => _value.IsDisposed ? null : SecureCharBuffer.CopyOf(_value.Span);

    public void Dispose() => _value.Dispose();
}

public sealed class GeneratorViewModel : ObservableObject, ISecretSource, IDisposable
{
    private readonly ShellViewModel _shell;
    private readonly GeneratorSettings _options;
    private GeneratedSecret? _current;
    private StrengthReport _strength = StrengthReport.Empty;
    private int _version;

    public GeneratorViewModel(ShellViewModel shell)
    {
        _shell = shell;
        _options = shell.App.Settings.Generator.Clone();
        RegenerateCommand = new RelayCommand(Regenerate);
        CopyCommand = new RelayCommand(Copy);
        UseCommand = new RelayCommand(UseInNewEntry);
        ClearHistoryCommand = new RelayCommand(() =>
        {
            foreach (var item in History)
            {
                item.Dispose();
            }

            History.Clear();
        });
        Regenerate();
    }

    public GeneratorMode Mode
    {
        get => _options.Mode;
        set
        {
            if (_options.Mode != value)
            {
                _options.Mode = value;
                Changed();
            }
        }
    }

    public bool IsCharacters => Mode == GeneratorMode.Characters;

    public bool IsPassphrase => Mode == GeneratorMode.Passphrase;

    public bool IsPin => Mode == GeneratorMode.Pin;

    public int Length
    {
        get => _options.Length;
        set
        {
            if (_options.Length != value)
            {
                _options.Length = Math.Clamp(value, GeneratorSettings.MinLength, GeneratorSettings.MaxLength);
                Changed();
            }
        }
    }

    public int Words
    {
        get => _options.Words;
        set
        {
            if (_options.Words != value)
            {
                _options.Words = Math.Clamp(value, GeneratorSettings.MinWords, GeneratorSettings.MaxWords);
                Changed();
            }
        }
    }

    public int PinLength
    {
        get => _options.PinLength;
        set
        {
            if (_options.PinLength != value)
            {
                _options.PinLength = Math.Clamp(value, GeneratorSettings.MinPin, GeneratorSettings.MaxPin);
                Changed();
            }
        }
    }

    public bool Lowercase
    {
        get => _options.Lowercase;
        set => Toggle(() => _options.Lowercase = value);
    }

    public bool Uppercase
    {
        get => _options.Uppercase;
        set => Toggle(() => _options.Uppercase = value);
    }

    public bool Digits
    {
        get => _options.Digits;
        set => Toggle(() => _options.Digits = value);
    }

    public bool Symbols
    {
        get => _options.Symbols;
        set => Toggle(() => _options.Symbols = value);
    }

    public bool AvoidAmbiguous
    {
        get => _options.AvoidAmbiguous;
        set => Toggle(() => _options.AvoidAmbiguous = value);
    }

    public string ExcludeCharacters
    {
        get => _options.ExcludeCharacters;
        set => Toggle(() => _options.ExcludeCharacters = value ?? string.Empty);
    }

    public bool CapitalizeWords
    {
        get => _options.CapitalizeWords;
        set => Toggle(() => _options.CapitalizeWords = value);
    }

    public bool AppendNumber
    {
        get => _options.AppendNumber;
        set => Toggle(() => _options.AppendNumber = value);
    }

    public string Separator
    {
        get => _options.Separator;
        set => Toggle(() => _options.Separator = string.IsNullOrEmpty(value) ? "-" : value[..Math.Min(3, value.Length)]);
    }

    public bool ItalianWords
    {
        get => _options.WordList == "it";
        set => Toggle(() => _options.WordList = value ? "it" : "en");
    }

    public int Version => _version;

    public string EntropyText => _current is null ? string.Empty : Loc.F("Generator.Bits", Math.Round(_current.EntropyBits));

    public int StrengthLevel => (int)_strength.Level;

    public string StrengthText => Loc.T("Strength." + _strength.Level);

    public string CrackText => _strength.Level == Core.Analysis.StrengthLevel.Empty
        ? string.Empty
        : Loc.F("Generator.CrackTime", Loc.Duration(_strength.SecondsToCrack(1e12)));

    public string LengthText => _current is null ? string.Empty : Loc.F("Generator.LengthValue", _current.Value.Length);

    public ObservableCollection<GeneratedItemViewModel> History { get; } = [];

    public bool HasHistory => History.Count > 0;

    public ICommand RegenerateCommand { get; }

    public ICommand CopyCommand { get; }

    public ICommand UseCommand { get; }

    public ICommand ClearHistoryCommand { get; }

    /// <summary>Raised after each generation (the view plays the decryption effect).</summary>
    public event Action? Generated;

    public SecureCharBuffer? OpenSecret() => _current is { } current && !current.Value.IsDisposed ? SecureCharBuffer.CopyOf(current.Value.Span) : null;

    private void Toggle(Action change)
    {
        change();
        Changed();
    }

    private void Changed()
    {
        _options.Normalize();
        _shell.App.Settings.Generator = _options.Clone();
        _shell.App.SaveSettings();
        OnPropertyChanged(string.Empty);
        Regenerate();
    }

    public void Regenerate()
    {
        if (_current is not null)
        {
            History.Insert(0, new GeneratedItemViewModel(SecureCharBuffer.CopyOf(_current.Value.Span), _current.EntropyBits, _shell.App));
            while (History.Count > 12)
            {
                History[^1].Dispose();
                History.RemoveAt(History.Count - 1);
            }

            _current.Dispose();
        }

        _current = PasswordGenerator.Generate(_options);
        _strength = PasswordStrength.Evaluate(_current.Value.Span);
        _version++;
        OnPropertyChanged(nameof(Version));
        OnPropertyChanged(nameof(EntropyText));
        OnPropertyChanged(nameof(StrengthLevel));
        OnPropertyChanged(nameof(StrengthText));
        OnPropertyChanged(nameof(CrackText));
        OnPropertyChanged(nameof(LengthText));
        OnPropertyChanged(nameof(HasHistory));
        Generated?.Invoke();
    }

    private void Copy()
    {
        if (_current is null)
        {
            return;
        }

        _shell.App.Clipboard.CopySecret(_current.Value.Span, Loc.T("Clipboard.Generated"));
        _shell.App.Toast(Loc.T("Toast.Copied"), Loc.T("Clipboard.Generated"), ToastKind.Success, TimeSpan.FromSeconds(2.5));
    }

    private void UseInNewEntry()
    {
        if (_current is null)
        {
            return;
        }

        _shell.NewEntry(EntryKind.Login);
        if (_shell.Editor?.Fields.FirstOrDefault(f => f.Kind == FieldKind.Password) is { Secret: { } secret } field)
        {
            secret.Set(_current.Value.Span);
            field.IsRevealed = true;
        }
    }

    public void Dispose()
    {
        _current?.Dispose();
        foreach (var item in History)
        {
            item.Dispose();
        }
    }
}

// =================================================================== security

public sealed record HealthCategoryViewModel(string Key, string Title, string Description, string IconKey, int Count, string Tone, ICommand OpenCommand)
{
    public bool IsClean => Count == 0;
}

public sealed record InfoRow(string Label, string Value, string IconKey = "Icon.Info");

public sealed class BackupItemViewModel(BackupInfo backup, ICommand restore)
{
    public BackupInfo Backup { get; } = backup;

    public string When => Loc.Date(Backup.Created);

    public string Ago => Loc.Ago(Backup.Created);

    public string Size => Loc.Bytes(Backup.Size);

    public string Counter => $"#{Backup.WriteCounter}";

    public ICommand RestoreCommand { get; } = restore;
}

public sealed class SecurityViewModel : ObservableObject, IRefreshable
{
    private readonly ShellViewModel _shell;
    private bool _helloAvailable;

    public SecurityViewModel(ShellViewModel shell)
    {
        _shell = shell;
        ChangePasswordCommand = new AsyncCommand(ChangePasswordAsync);
        RecoveryCommand = new AsyncCommand(RegenerateRecoveryAsync);
        ToggleHelloCommand = new AsyncCommand(ToggleHelloAsync);
        StrengthenCommand = new AsyncCommand(StrengthenAsync);
        OpenFolderCommand = new RelayCommand(() => Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{shell.Session.Path}\"") { UseShellExecute = true }));
        OpenBackupsCommand = new RelayCommand(() =>
        {
            Directory.CreateDirectory(shell.Session.Storage.BackupDirectory);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{shell.Session.Storage.BackupDirectory}\"") { UseShellExecute = true });
        });
        DeleteBackupsCommand = new AsyncCommand(DeleteBackupsAsync);
        ClearHistoryCommand = new AsyncCommand(async () =>
        {
            if (await shell.App.ConfirmAsync(Loc.T("History.ClearAllTitle"), Loc.F("History.ClearAllMessage", shell.Session.HistoryCount), Loc.T("History.Clear"), danger: true))
            {
                shell.Session.ClearHistory();
                Refresh();
            }
        });
        _ = DetectHello();
        Refresh();
    }

    public int Score => _shell.Health.Score;

    public string ScoreCaption => Loc.T(Score switch
    {
        >= 90 => "Security.ScoreExcellent",
        >= 75 => "Security.ScoreGood",
        >= 50 => "Security.ScoreFair",
        _ => "Security.ScorePoor",
    });

    public string Summary => Loc.F("Security.Summary", _shell.Health.Analyzed, _shell.Health.IssueCount);

    public ObservableCollection<HealthCategoryViewModel> Categories { get; } = [];

    public ObservableCollection<InfoRow> Crypto { get; } = [];

    public ObservableCollection<InfoRow> Protections { get; } = [];

    public ObservableCollection<BackupItemViewModel> Backups { get; } = [];

    public ObservableCollection<InfoRow> Audit { get; } = [];

    public bool HasBackups => Backups.Count > 0;

    public bool HasRecovery => _shell.Session.HasRecoveryKey;

    public bool HasHello => _shell.Session.HasHello;

    public bool HelloAvailable
    {
        get => _helloAvailable;
        private set => Set(ref _helloAvailable, value);
    }

    public string HelloText => HasHello ? Loc.T("Security.HelloOn") : HelloAvailable ? Loc.T("Security.HelloOff") : Loc.T("Security.HelloUnavailable");

    public string KdfText => _shell.Session.PasswordKdf?.Describe() ?? "—";

    public bool CanStrengthen => _shell.Session.PasswordKdf is { } kdf && kdf.IsWeakerThan(KdfParameters.Extreme);

    public string HistoryText => Loc.F("Security.HistoryCount", _shell.Session.HistoryCount);

    public ICommand ChangePasswordCommand { get; }

    public ICommand RecoveryCommand { get; }

    public ICommand ToggleHelloCommand { get; }

    public ICommand StrengthenCommand { get; }

    public ICommand OpenFolderCommand { get; }

    public ICommand OpenBackupsCommand { get; }

    public ICommand DeleteBackupsCommand { get; }

    public ICommand ClearHistoryCommand { get; }

    private async Task DetectHello()
    {
        HelloAvailable = await _shell.App.Hello.IsAvailableAsync();
        OnPropertyChanged(nameof(HelloText));
    }

    public void Refresh()
    {
        var health = _shell.Health;
        Categories.Clear();
        void Add(string key, string icon, int count, string tone)
        {
            var title = Loc.T($"Security.Cat.{key}");
            Categories.Add(new HealthCategoryViewModel(key, title, Loc.T($"Security.Cat.{key}.Desc"), icon, count, count == 0 ? "ok" : tone, new RelayCommand(() => _shell.ShowHealthCategory(key, title))));
        }

        Add("weak", "Icon.Warning", health.Weak.Count, "warning");
        Add("reused", "Icon.Copy", health.Reused.Count, "danger");
        Add("old", "Icon.History", health.Old.Count, "warning");
        Add("expired", "Icon.Calendar", health.Expired.Count + health.ExpiringSoon.Count, "danger");
        Add("totp", "Icon.Timer", health.MissingTotp.Count, "info");
        Add("empty", "Icon.Key", health.EmptyPassword.Count, "info");

        var session = _shell.Session;
        Crypto.Clear();
        Crypto.Add(new InfoRow(Loc.T("Security.Kdf"), session.PasswordKdf?.Describe() ?? "—", "Icon.Chip"));
        Crypto.Add(new InfoRow(Loc.T("Security.Cipher"), "ChaCha20-Poly1305 ⟶ AES-256-GCM", "Icon.Layers"));
        Crypto.Add(new InfoRow(Loc.T("Security.Commitment"), "HMAC-SHA-512 · HKDF-SHA-512", "Icon.ShieldCheck"));
        Crypto.Add(new InfoRow(Loc.T("Security.Fields"), Loc.T("Security.FieldsValue"), "Icon.Lock"));
        Crypto.Add(new InfoRow(Loc.T("Security.Padding"), "PADMÉ", "Icon.Archive"));
        Crypto.Add(new InfoRow(Loc.T("Security.Sharing"), "ML-KEM-1024 + ECDH P-384 · ML-DSA-87 + ECDSA P-384", "Icon.Atom"));
        Crypto.Add(new InfoRow(Loc.T("Security.File"), $"{session.Path} · {Loc.Bytes(new FileInfo(session.Path).Exists ? new FileInfo(session.Path).Length : 0)}", "Icon.Folder"));
        Crypto.Add(new InfoRow(Loc.T("Security.Counter"), $"#{session.WriteCounter}", "Icon.Hash"));

        var memory = SecureMemory.Status;
        Protections.Clear();
        Protections.Add(new InfoRow(Loc.T("Security.Memory"), Loc.F("Security.MemoryValue", memory.Regions, Loc.Bytes(memory.LockedBytes), memory.ProtectedBuffers), "Icon.Chip"));
        Protections.Add(new InfoRow(Loc.T("Security.MemoryEncryption"), memory.HardwareProtection ? Loc.T("Security.Active") : Loc.T("Security.Unavailable"), "Icon.Lock"));
        Protections.Add(new InfoRow(Loc.T("Security.ProcessAccess"), ProcessHardening.MemoryAccessDenied ? Loc.T("Security.Active") : Loc.T("Security.Inactive"), "Icon.Shield"));
        Protections.Add(new InfoRow(Loc.T("Security.Mitigations"), ProcessHardening.MitigationsApplied && ProcessHardening.DllSearchRestricted ? Loc.T("Security.Active") : Loc.T("Security.Partial"), "Icon.Bolt"));
        Protections.Add(new InfoRow(Loc.T("Security.Capture"), _shell.App.Settings.ScreenCaptureProtection ? Loc.T("Security.Active") : Loc.T("Security.Inactive"), "Icon.EyeOff"));
        Protections.Add(new InfoRow(Loc.T("Security.Clipboard"), _shell.App.Settings.ClipboardExcludeHistory ? Loc.F("Security.ClipboardValue", _shell.App.Settings.ClipboardClearSeconds) : Loc.T("Security.Inactive"), "Icon.Clipboard"));

        Backups.Clear();
        foreach (var backup in session.ListBackups().Take(12))
        {
            Backups.Add(new BackupItemViewModel(backup, new AsyncCommand(() => RestoreAsync(backup))));
        }

        Audit.Clear();
        foreach (var item in session.Document.Audit.AsEnumerable().Reverse().Take(30))
        {
            Audit.Add(new InfoRow(Loc.T("Audit." + item.Kind), Loc.Ago(item.At), AuditIcon(item.Kind)));
        }

        OnPropertyChanged(string.Empty);
    }

    private static string AuditIcon(AuditKind kind) => kind switch
    {
        AuditKind.Unlocked or AuditKind.UnlockedWithHello => "Icon.Unlock",
        AuditKind.UnlockedWithRecovery or AuditKind.RecoveryKeyRegenerated => "Icon.Key",
        AuditKind.MasterPasswordChanged or AuditKind.KdfUpgraded => "Icon.ShieldCheck",
        AuditKind.HelloEnabled or AuditKind.HelloDisabled => "Icon.Fingerprint",
        AuditKind.Exported or AuditKind.PackageShared => "Icon.Export",
        AuditKind.Imported or AuditKind.Migrated or AuditKind.PackageReceived => "Icon.Import",
        AuditKind.RollbackDetected => "Icon.Warning",
        AuditKind.BackupRestored => "Icon.Restore",
        AuditKind.TrashEmptied => "Icon.Trash",
        _ => "Icon.Seal",
    };

    private async Task ChangePasswordAsync()
    {
        var app = _shell.App;
        using var current = await app.AskPasswordAsync(Loc.T("ChangePassword.Title"), Loc.T("ChangePassword.Current"), Loc.T("Common.Continue"));
        if (current is null)
        {
            return;
        }

        if (!await _shell.Session.VerifyPasswordAsync(current))
        {
            app.Toast(Loc.T("ChangePassword.Wrong"), null, ToastKind.Error);
            return;
        }

        using var next = await app.AskPasswordAsync(Loc.T("ChangePassword.Title"), Loc.T("ChangePassword.New"), Loc.T("ChangePassword.Apply"), confirm: true, strength: true);
        if (next is null)
        {
            return;
        }

        var rotate = await app.ConfirmAsync(Loc.T("ChangePassword.RotateTitle"), Loc.T("ChangePassword.RotateMessage"), Loc.T("ChangePassword.Rotate"), iconKey: "Icon.Rotate");
        try
        {
            var kdf = KdfParameters.FromStrength(AppViewModel.StrengthOf(app.Settings.KdfStrength));
            var code = await _shell.Session.ChangeMasterPasswordAsync(next, kdf, rotate);
            app.Toast(Loc.T("ChangePassword.Done"), rotate ? Loc.T("ChangePassword.DoneRotated") : null, ToastKind.Success);
            if (code is not null)
            {
                await app.ShowDialog(new RecoveryKeyDialogViewModel(code) { Title = Loc.T("Recovery.NewTitle"), Message = Loc.T("Recovery.NewMessage"), IconKey = "Icon.Key" });
            }

            if (_shell.Session.ListBackups().Count > 0 &&
                await app.ConfirmAsync(Loc.T("ChangePassword.BackupsTitle"), Loc.T("ChangePassword.BackupsMessage"), Loc.T("ChangePassword.DeleteBackups"), danger: true))
            {
                _shell.Session.Storage.DeleteAllBackups(_shell.Session.VaultId);
            }
        }
        catch (Exception error)
        {
            app.Toast(Loc.T("ChangePassword.Failed"), Errors.Describe(error), ToastKind.Error);
        }

        Refresh();
    }

    private async Task RegenerateRecoveryAsync()
    {
        var app = _shell.App;
        if (!await app.ConfirmAsync(Loc.T("Recovery.RegenerateTitle"), Loc.T("Recovery.RegenerateMessage"), Loc.T("Recovery.Regenerate"), iconKey: "Icon.Key"))
        {
            return;
        }

        var code = await _shell.Session.RegenerateRecoveryKeyAsync();
        await app.ShowDialog(new RecoveryKeyDialogViewModel(code) { Title = Loc.T("Recovery.NewTitle"), Message = Loc.T("Recovery.NewMessage"), IconKey = "Icon.Key" });
        Refresh();
    }

    private async Task ToggleHelloAsync()
    {
        var app = _shell.App;
        try
        {
            if (_shell.Session.HasHello)
            {
                await _shell.Session.DisableHelloAsync(app.Hello);
                app.Toast(Loc.T("Security.HelloDisabled"), null, ToastKind.Info);
            }
            else
            {
                await _shell.Session.EnableHelloAsync(app.Hello);
                app.Toast(Loc.T("Security.HelloEnabled"), null, ToastKind.Success);
            }
        }
        catch (VaultException error) when (error.Error == VaultError.Cancelled)
        {
        }
        catch (Exception error)
        {
            app.Toast(Loc.T("Security.HelloFailed"), Errors.Describe(error), ToastKind.Error);
        }

        Refresh();
    }

    private async Task StrengthenAsync()
    {
        var app = _shell.App;
        var choice = await app.ShowDialog(new ChoiceDialogViewModel(
        [
            new ChoiceOption(Loc.T("Kdf.Standard"), KdfParameters.Standard.Describe(), "Icon.Shield", KdfStrength.Standard),
            new ChoiceOption(Loc.T("Kdf.High"), KdfParameters.High.Describe(), "Icon.ShieldCheck", KdfStrength.High, true),
            new ChoiceOption(Loc.T("Kdf.Extreme"), KdfParameters.Extreme.Describe(), "Icon.Bolt", KdfStrength.Extreme),
        ])
        {
            Title = Loc.T("Kdf.Title"),
            Message = Loc.T("Kdf.Message"),
            IconKey = "Icon.Chip",
        });
        if (choice is not KdfStrength strength)
        {
            return;
        }

        using var password = await app.AskPasswordAsync(Loc.T("Kdf.Title"), Loc.T("Kdf.Confirm"));
        if (password is null)
        {
            return;
        }

        try
        {
            await _shell.Session.UpgradeKdfAsync(password, KdfParameters.FromStrength(strength));
            app.Settings.KdfStrength = strength.ToString().ToLowerInvariant();
            app.SaveSettings();
            app.Toast(Loc.T("Kdf.Done"), KdfParameters.FromStrength(strength).Describe(), ToastKind.Success);
        }
        catch (Exception error)
        {
            app.Toast(Loc.T("Kdf.Failed"), Errors.Describe(error), ToastKind.Error);
        }

        Refresh();
    }

    private async Task RestoreAsync(BackupInfo backup)
    {
        var app = _shell.App;
        if (!await app.ConfirmAsync(Loc.T("Backup.RestoreTitle"), Loc.F("Backup.RestoreMessage", Loc.Date(backup.Created)), Loc.T("Backup.Restore"), danger: true))
        {
            return;
        }

        try
        {
            await _shell.Session.RestoreBackupAsync(backup, null);
        }
        catch (VaultException)
        {
            using var password = await app.AskPasswordAsync(Loc.T("Backup.PasswordTitle"), Loc.T("Backup.PasswordMessage"));
            if (password is null)
            {
                return;
            }

            try
            {
                await _shell.Session.RestoreBackupAsync(backup, password);
            }
            catch (Exception error)
            {
                app.Toast(Loc.T("Backup.Failed"), Errors.Describe(error), ToastKind.Error);
                return;
            }
        }

        app.Toast(Loc.T("Backup.Restored"), Loc.Date(backup.Created), ToastKind.Success);
        Refresh();
    }

    private async Task DeleteBackupsAsync()
    {
        if (await _shell.App.ConfirmAsync(Loc.T("Backup.DeleteTitle"), Loc.T("Backup.DeleteMessage"), Loc.T("Backup.Delete"), danger: true))
        {
            var count = _shell.Session.Storage.DeleteAllBackups(_shell.Session.VaultId);
            _shell.App.Toast(Loc.F("Backup.Deleted", count), null, ToastKind.Info);
            Refresh();
        }
    }
}

// ==================================================================== sharing

public sealed class ContactViewModel(Contact contact, PublicIdentity? identity, ICommand remove, ICommand verify)
{
    public Contact Contact { get; } = contact;

    public string Name => Contact.Name;

    public string Fingerprint => identity?.FingerprintText ?? "—";

    public byte[] FingerprintBytes => identity?.Fingerprint ?? [];

    public bool Verified => Contact.Verified;

    public string Added => Loc.ShortDate(Contact.Added);

    public ICommand RemoveCommand { get; } = remove;

    public ICommand VerifyCommand { get; } = verify;
}

public sealed class SharingViewModel : ObservableObject, IRefreshable
{
    private readonly ShellViewModel _shell;
    private PublicIdentity? _identity;

    public SharingViewModel(ShellViewModel shell)
    {
        _shell = shell;
        CreateIdentityCommand = new AsyncCommand(CreateIdentityAsync);
        CopyIdCommand = new RelayCommand(() =>
        {
            if (_identity is not null)
            {
                shell.App.Clipboard.CopyText(_identity.ToText(), Loc.T("Sharing.IdLabel"));
                shell.App.Toast(Loc.T("Toast.Copied"), Loc.T("Sharing.IdLabel"), ToastKind.Success);
            }
        });
        SaveIdCommand = new RelayCommand(SaveId);
        AddContactCommand = new AsyncCommand(AddContactAsync);
        ShareCommand = new AsyncCommand(() => ShareEntriesAsync(null));
        OpenPackageCommand = new AsyncCommand(() => OpenPackageAsync(null));
        Refresh();
    }

    public bool IsSupported => IdentityKeys.IsSupported;

    public bool HasIdentity => _identity is not null;

    public bool NeedsIdentity => _identity is null && IsSupported;

    public string IdentityName => _identity?.DisplayName ?? string.Empty;

    public string Fingerprint => _identity?.FingerprintText ?? string.Empty;

    public byte[] FingerprintBytes => _identity?.Fingerprint ?? [];

    public string Created => _identity is null ? string.Empty : Loc.ShortDate(_identity.Created);

    public ObservableCollection<ContactViewModel> Contacts { get; } = [];

    public bool HasContacts => Contacts.Count > 0;

    public ICommand CreateIdentityCommand { get; }

    public ICommand CopyIdCommand { get; }

    public ICommand SaveIdCommand { get; }

    public ICommand AddContactCommand { get; }

    public ICommand ShareCommand { get; }

    public ICommand OpenPackageCommand { get; }

    public void Refresh()
    {
        _identity = IdentityKeys.PublicOf(_shell.Session);
        Contacts.Clear();
        foreach (var contact in _shell.Session.Document.Contacts.OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            PublicIdentity? identity = null;
            try
            {
                identity = PublicIdentity.Parse(contact.PublicBundle);
            }
            catch (VaultException)
            {
            }

            Contacts.Add(new ContactViewModel(
                contact,
                identity,
                new AsyncCommand(() => RemoveContactAsync(contact)),
                new RelayCommand(() =>
                {
                    contact.Verified = !contact.Verified;
                    _shell.Session.Commit();
                    Refresh();
                })));
        }

        OnPropertyChanged(string.Empty);
    }

    private async Task CreateIdentityAsync()
    {
        var name = await _shell.App.AskTextAsync(Loc.T("Sharing.CreateTitle"), Loc.T("Sharing.CreateLabel"), Environment.UserName);
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        try
        {
            await Task.Run(() => IdentityKeys.CreateIn(_shell.Session, name));
            _shell.App.Toast(Loc.T("Sharing.Created"), Loc.T("Sharing.CreatedDetail"), ToastKind.Success);
        }
        catch (Exception error)
        {
            _shell.App.Toast(Loc.T("Sharing.Failed"), Errors.Describe(error), ToastKind.Error);
        }

        Refresh();
    }

    private void SaveId()
    {
        if (_identity is null)
        {
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog { FileName = $"{_identity.DisplayName}{AppPaths.IdentityExtension}", Filter = $"OnlyYours ID|*{AppPaths.IdentityExtension}" };
        if (dialog.ShowDialog() == true)
        {
            File.WriteAllText(dialog.FileName, _identity.ToText());
            _shell.App.Toast(Loc.T("Sharing.IdSaved"), dialog.FileName, ToastKind.Success);
        }
    }

    private async Task AddContactAsync()
    {
        var app = _shell.App;
        string? text = null;
        var choice = await app.ShowDialog(new ChoiceDialogViewModel(
        [
            new ChoiceOption(Loc.T("Sharing.FromFile"), Loc.T("Sharing.FromFileDesc"), "Icon.Folder", "file", true),
            new ChoiceOption(Loc.T("Sharing.FromText"), Loc.T("Sharing.FromTextDesc"), "Icon.Text", "text"),
        ])
        {
            Title = Loc.T("Sharing.AddContact"),
            IconKey = "Icon.Users",
        });
        if (choice is "file")
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = $"OnlyYours ID|*{AppPaths.IdentityExtension}|{Loc.T("File.AllFilter")}|*.*" };
            if (dialog.ShowDialog() == true)
            {
                text = File.ReadAllText(dialog.FileName);
            }
        }
        else if (choice is "text")
        {
            text = await app.AskTextAsync(Loc.T("Sharing.AddContact"), Loc.T("Sharing.PasteId"), multiline: true);
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (!PublicIdentity.TryParseText(text, out var identity) || identity is null)
        {
            app.Toast(Loc.T("Sharing.InvalidId"), null, ToastKind.Error);
            return;
        }

        if (_identity is not null && identity.Fingerprint.SequenceEqual(_identity.Fingerprint))
        {
            app.Toast(Loc.T("Sharing.OwnId"), null, ToastKind.Warning);
            return;
        }

        var verified = await app.ConfirmAsync(
            Loc.T("Sharing.VerifyTitle"),
            Loc.F("Sharing.VerifyMessage", identity.DisplayName, identity.FingerprintText),
            Loc.T("Sharing.VerifyYes"),
            iconKey: "Icon.Fingerprint");
        _shell.Session.Document.Contacts.RemoveAll(c => c.PublicBundle.AsSpan().SequenceEqual(identity.Bundle));
        _shell.Session.Document.Contacts.Add(new Contact { Name = identity.DisplayName, PublicBundle = identity.Bundle, Added = DateTimeOffset.Now, Verified = verified });
        _shell.Session.Commit();
        app.Toast(Loc.T("Sharing.ContactAdded"), identity.DisplayName, ToastKind.Success);
        Refresh();
    }

    private async Task RemoveContactAsync(Contact contact)
    {
        if (await _shell.App.ConfirmAsync(Loc.T("Sharing.RemoveTitle"), Loc.F("Sharing.RemoveMessage", contact.Name), Loc.T("Common.Remove"), danger: true))
        {
            _shell.Session.Document.Contacts.Remove(contact);
            _shell.Session.Commit();
            Refresh();
        }
    }

    public async Task ShareEntriesAsync(IReadOnlyList<Entry>? preselected)
    {
        var app = _shell.App;
        if (_identity is null)
        {
            await app.AlertAsync(Loc.T("Sharing.NeedIdentityTitle"), Loc.T("Sharing.NeedIdentity"));
            return;
        }

        if (_shell.Session.Document.Contacts.Count == 0)
        {
            await app.AlertAsync(Loc.T("Sharing.NeedContactTitle"), Loc.T("Sharing.NeedContact"));
            return;
        }

        var entries = preselected?.ToList();
        if (entries is null || entries.Count == 0)
        {
            var all = _shell.Session.Document.Entries.OrderBy(e => e.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
            var picked = await app.ShowDialog(new SelectionDialogViewModel(all.Select((e, i) => new SelectableItem(e.Title, EntryText.Subtitle(e), EntryText.KindIcon(e.Kind), i) { IsSelected = false }))
            {
                Title = Loc.T("Sharing.PickEntries"),
                IconKey = "Icon.Share",
            });
            if (picked is not List<int> indexes)
            {
                return;
            }

            entries = indexes.Select(i => all[i]).ToList();
        }

        var contacts = _shell.Session.Document.Contacts;
        var recipient = await app.ShowDialog(new ChoiceDialogViewModel(contacts.Select(c =>
            new ChoiceOption(c.Name, (c.Verified ? "✓ " : "⚠ ") + PublicIdentity.Parse(c.PublicBundle).FingerprintText, "Icon.User", c)))
        {
            Title = Loc.T("Sharing.PickRecipient"),
            Message = Loc.F("Sharing.PickRecipientMessage", entries.Count),
            IconKey = "Icon.Users",
            ConfirmText = Loc.T("Sharing.Seal"),
        });
        if (recipient is not Contact contact)
        {
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog { FileName = $"{contact.Name} - {DateTime.Now:yyyy-MM-dd}{AppPaths.PackageExtension}", Filter = $"{Loc.T("File.PackageFilter")}|*{AppPaths.PackageExtension}" };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var target = PublicIdentity.Parse(contact.PublicBundle);
            var bytes = await Task.Run(() =>
            {
                using var keys = IdentityKeys.Load(_shell.Session);
                return SealedPackage.CreateForRecipient(_shell.Session, keys, target, entries, null);
            });
            await File.WriteAllBytesAsync(dialog.FileName, bytes);
            _shell.Session.Document.AddAudit(AuditKind.PackageShared, contact.Name);
            _shell.Session.Commit();
            app.Toast(Loc.T("Sharing.Sealed"), Loc.F("Sharing.SealedDetail", entries.Count, contact.Name), ToastKind.Success);
        }
        catch (Exception error)
        {
            app.Toast(Loc.T("Sharing.Failed"), Errors.Describe(error), ToastKind.Error);
        }
    }

    public async Task OpenPackageAsync(string? path)
    {
        var app = _shell.App;
        if (path is null)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = $"{Loc.T("File.PackageFilter")}|*{AppPaths.PackageExtension}|{Loc.T("File.AllFilter")}|*.*" };
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            path = dialog.FileName;
        }

        try
        {
            var bytes = await File.ReadAllBytesAsync(path);
            var info = SealedPackage.Inspect(bytes);
            OpenedPackage opened;
            if (info.Mode == PackageMode.Password)
            {
                using var password = await app.AskPasswordAsync(Loc.T("Package.PasswordTitle"), Loc.T("Package.PasswordMessage"));
                if (password is null)
                {
                    return;
                }

                opened = await Task.Run(() => SealedPackage.OpenWithPassword(bytes, password));
            }
            else
            {
                if (_identity is null)
                {
                    await app.AlertAsync(Loc.T("Sharing.NeedIdentityTitle"), Loc.T("Sharing.NeedIdentity"));
                    return;
                }

                opened = await Task.Run(() =>
                {
                    using var keys = IdentityKeys.Load(_shell.Session);
                    return SealedPackage.OpenForRecipient(bytes, keys);
                });
            }

            using (opened)
            {
                var sender = opened.Sender is { } s
                    ? (_shell.Session.Document.Contacts.FirstOrDefault(c => PublicIdentity.Parse(c.PublicBundle).Fingerprint.SequenceEqual(s.Fingerprint)) is { } known
                        ? Loc.F(known.Verified ? "Package.FromVerified" : "Package.FromKnown", known.Name, s.FingerprintText)
                        : Loc.F("Package.FromUnknown", s.DisplayName, s.FingerprintText))
                    : Loc.T("Package.PasswordProtected");
                var picked = await app.ShowDialog(new SelectionDialogViewModel(opened.Entries.Select((e, i) => new SelectableItem(e.Title, EntryText.KindName(e.Kind), EntryText.KindIcon(e.Kind), i)))
                {
                    Title = Loc.F("Package.Title", opened.Entries.Count),
                    Message = sender,
                    Note = opened.Message,
                    IconKey = "Icon.Package",
                    ConfirmText = Loc.T("Package.Import"),
                });
                if (picked is List<int> indexes)
                {
                    var imported = SealedPackage.ImportInto(opened, _shell.Session, indexes);
                    app.Toast(Loc.F("Package.Imported", imported.Count), null, ToastKind.Success);
                }
            }
        }
        catch (Exception error)
        {
            app.Toast(Loc.T("Package.Failed"), Errors.Describe(error), ToastKind.Error);
        }
    }
}

// ====================================================================== trash

public sealed class TrashItemViewModel(Entry entry, int retentionDays, ICommand restore, ICommand delete)
{
    public Entry Entry { get; } = entry;

    public string Title => Entry.Title;

    public string KindIcon => EntryText.KindIcon(Entry.Kind);

    public string Subtitle => EntryText.Subtitle(Entry);

    public string Deleted => Entry.Deleted is { } deleted ? Loc.Ago(deleted) : string.Empty;

    public string DaysLeft => Entry.Deleted is { } deleted ? Loc.F("Trash.DaysLeft", Math.Max(0, retentionDays - (int)(DateTimeOffset.Now - deleted).TotalDays)) : string.Empty;

    public ICommand RestoreCommand { get; } = restore;

    public ICommand DeleteCommand { get; } = delete;
}

public sealed class TrashViewModel : ObservableObject, IRefreshable
{
    private readonly ShellViewModel _shell;

    public TrashViewModel(ShellViewModel shell)
    {
        _shell = shell;
        EmptyCommand = new AsyncCommand(async () =>
        {
            if (await shell.App.ConfirmAsync(Loc.T("Trash.EmptyTitle"), Loc.F("Trash.EmptyMessage", shell.Session.Document.Trash.Count), Loc.T("Trash.Empty"), danger: true))
            {
                shell.Session.EmptyTrash();
                Refresh();
            }
        });
        Refresh();
    }

    public ObservableCollection<TrashItemViewModel> Items { get; } = [];

    public bool IsEmpty => Items.Count == 0;

    public string RetentionText => Loc.F("Trash.Retention", _shell.App.Settings.TrashRetentionDays);

    public ICommand EmptyCommand { get; }

    public void Refresh()
    {
        Items.Clear();
        foreach (var entry in _shell.Session.Document.Trash.OrderByDescending(e => e.Deleted))
        {
            Items.Add(new TrashItemViewModel(
                entry,
                _shell.App.Settings.TrashRetentionDays,
                new RelayCommand(() =>
                {
                    _shell.Session.RestoreFromTrash(entry.Id);
                    _shell.App.Toast(Loc.T("Trash.Restored"), entry.Title, ToastKind.Success);
                    Refresh();
                }),
                new AsyncCommand(async () =>
                {
                    if (await _shell.App.ConfirmAsync(Loc.T("Trash.DeleteTitle"), Loc.F("Trash.DeleteMessage", entry.Title), Loc.T("Trash.DeleteForever"), danger: true))
                    {
                        _shell.Session.Purge(entry.Id);
                        Refresh();
                    }
                })));
        }

        OnPropertyChanged(nameof(IsEmpty));
    }
}

// ============================================================ import / export

public sealed class TransferViewModel : ObservableObject
{
    private readonly ShellViewModel _shell;

    public TransferViewModel(ShellViewModel shell)
    {
        _shell = shell;
        ImportLegacyCommand = new AsyncCommand(ImportLegacyAsync);
        ImportCsvCommand = new AsyncCommand(ImportCsvAsync);
        OpenPackageCommand = new AsyncCommand(() => new SharingViewModel(shell).OpenPackageAsync(null));
        ExportPackageCommand = new AsyncCommand(ExportPackageAsync);
        ExportCsvCommand = new AsyncCommand(ExportCsvAsync);
        BackupCopyCommand = new RelayCommand(BackupCopy);
    }

    public ICommand ImportLegacyCommand { get; }

    public ICommand ImportCsvCommand { get; }

    public ICommand OpenPackageCommand { get; }

    public ICommand ExportPackageCommand { get; }

    public ICommand ExportCsvCommand { get; }

    public ICommand BackupCopyCommand { get; }

    private async Task ImportLegacyAsync()
    {
        var app = _shell.App;
        var path = AppPaths.FindLegacyVault();
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = $"{Loc.T("File.LegacyFilter")}|*.plf;*.plf.bak|{Loc.T("File.AllFilter")}|*.*",
            FileName = path ?? string.Empty,
            InitialDirectory = path is null ? null : Path.GetDirectoryName(path),
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        using var password = await app.AskPasswordAsync(Loc.T("Migrate.Title"), Loc.T("Migrate.PasswordPrompt"));
        if (password is null)
        {
            return;
        }

        try
        {
            using var payload = await LegacyVault.OpenAsync(dialog.FileName, password);
            var migrator = new LegacyMigrator();
            var document = migrator.Build(payload, _shell.Session.Fields, _shell.Session.Document.Name);
            var existing = _shell.Session.Document.Entries.Select(e => (e.Title, e.Username)).ToHashSet();
            var fresh = document.Entries.Where(e => !existing.Contains((e.Title, e.Username))).ToList();
            _shell.Session.AddEntries(fresh);
            _shell.Session.Document.AddAudit(AuditKind.Imported, "BetterOnlyYours");
            app.Toast(Loc.F("Transfer.Imported", fresh.Count), Loc.F("Transfer.Skipped", document.Entries.Count - fresh.Count), ToastKind.Success);
        }
        catch (Exception error)
        {
            app.Toast(Loc.T("Transfer.Failed"), Errors.Describe(error), ToastKind.Error);
        }
    }

    private async Task ImportCsvAsync()
    {
        var app = _shell.App;
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = $"CSV|*.csv;*.txt|{Loc.T("File.AllFilter")}|*.*" };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            using var document = CsvDocument.Load(dialog.FileName);
            var plan = CsvImporter.Detect(document);
            if (!plan.IsUsable || plan.DataRows <= 0)
            {
                app.Toast(Loc.T("Transfer.CsvUnknown"), null, ToastKind.Error);
                return;
            }

            if (!await app.ConfirmAsync(Loc.T("Transfer.CsvTitle"), Loc.F("Transfer.CsvMessage", plan.DataRows, Loc.T("CsvFormat." + plan.Format)), Loc.T("Transfer.Import"), iconKey: "Icon.Import"))
            {
                return;
            }

            var entries = CsvImporter.Build(document, plan, _shell.Session, skipDuplicates: true, out var report);
            _shell.Session.AddEntries(entries);
            _shell.Session.Document.AddAudit(AuditKind.Imported, "CSV " + plan.Format);
            app.Toast(Loc.F("Transfer.Imported", report.Imported), Loc.F("Transfer.CsvReport", report.Duplicates, report.WithTotp, report.Skipped), ToastKind.Success, TimeSpan.FromSeconds(7));
            if (await app.ConfirmAsync(Loc.T("Transfer.DeleteCsvTitle"), Loc.T("Transfer.DeleteCsvMessage"), Loc.T("Transfer.DeleteCsv"), danger: true))
            {
                SecureDelete(dialog.FileName);
            }
        }
        catch (Exception error)
        {
            app.Toast(Loc.T("Transfer.Failed"), Errors.Describe(error), ToastKind.Error);
        }
    }

    private async Task ExportPackageAsync()
    {
        var app = _shell.App;
        var all = _shell.Session.Document.Entries.OrderBy(e => e.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
        if (all.Count == 0)
        {
            app.Toast(Loc.T("Transfer.NothingToExport"), null, ToastKind.Warning);
            return;
        }

        var picked = await app.ShowDialog(new SelectionDialogViewModel(all.Select((e, i) => new SelectableItem(e.Title, EntryText.Subtitle(e), EntryText.KindIcon(e.Kind), i)))
        {
            Title = Loc.T("Transfer.PackageTitle"),
            Message = Loc.T("Transfer.PackageMessage"),
            IconKey = "Icon.Package",
        });
        if (picked is not List<int> indexes)
        {
            return;
        }

        using var password = await app.AskPasswordAsync(Loc.T("Transfer.PackagePassword"), Loc.T("Transfer.PackagePasswordMessage"), confirm: true, strength: true);
        if (password is null)
        {
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog { FileName = $"OnlyYours {DateTime.Now:yyyy-MM-dd}{AppPaths.PackageExtension}", Filter = $"{Loc.T("File.PackageFilter")}|*{AppPaths.PackageExtension}" };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var entries = indexes.Select(i => all[i]).ToList();
            var kdf = KdfParameters.FromStrength(AppViewModel.StrengthOf(app.Settings.KdfStrength));
            var bytes = await Task.Run(() => SealedPackage.CreateWithPassword(_shell.Session, entries, password, kdf, _shell.Session.Document.Name, null));
            await File.WriteAllBytesAsync(dialog.FileName, bytes);
            _shell.Session.Document.AddAudit(AuditKind.Exported, "package");
            _shell.Session.Commit();
            app.Toast(Loc.F("Transfer.Exported", entries.Count), dialog.FileName, ToastKind.Success);
        }
        catch (Exception error)
        {
            app.Toast(Loc.T("Transfer.Failed"), Errors.Describe(error), ToastKind.Error);
        }
    }

    private async Task ExportCsvAsync()
    {
        var app = _shell.App;
        if (!await app.ConfirmAsync(Loc.T("Transfer.CsvExportTitle"), Loc.T("Transfer.CsvExportWarning"), Loc.T("Transfer.CsvExportContinue"), danger: true))
        {
            return;
        }

        using var password = await app.AskPasswordAsync(Loc.T("Transfer.CsvExportTitle"), Loc.T("Transfer.ConfirmIdentity"));
        if (password is null)
        {
            return;
        }

        if (!await _shell.Session.VerifyPasswordAsync(password))
        {
            app.Toast(Loc.T("ChangePassword.Wrong"), null, ToastKind.Error);
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog { FileName = $"onlyyours-export-{DateTime.Now:yyyy-MM-dd}.csv", Filter = "CSV|*.csv" };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            CsvExporter.Export(_shell.Session, _shell.Session.Document.Entries, dialog.FileName);
            _shell.Session.Document.AddAudit(AuditKind.Exported, "csv");
            _shell.Session.Commit();
            app.Toast(Loc.T("Transfer.CsvExported"), Loc.T("Transfer.CsvExportedDetail"), ToastKind.Warning, TimeSpan.FromSeconds(8));
        }
        catch (Exception error)
        {
            app.Toast(Loc.T("Transfer.Failed"), Errors.Describe(error), ToastKind.Error);
        }
    }

    private void BackupCopy()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { FileName = $"{_shell.Session.Document.Name} {DateTime.Now:yyyy-MM-dd}{AppPaths.VaultExtension}", Filter = $"{Loc.T("File.VaultFilter")}|*{AppPaths.VaultExtension}" };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            await _shell.Session.FlushAsync(TimeSpan.FromSeconds(5));
            File.Copy(_shell.Session.Path, dialog.FileName, overwrite: true);
        }).ContinueWith(
            t => _shell.App.Toast(t.IsFaulted ? Loc.T("Transfer.Failed") : Loc.T("Transfer.BackupCopied"), dialog.FileName, t.IsFaulted ? ToastKind.Error : ToastKind.Success),
            TaskScheduler.FromCurrentSynchronizationContext());
    }

    private static void SecureDelete(string path)
    {
        try
        {
            var length = new FileInfo(path).Length;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None))
            {
                var zeros = new byte[64 * 1024];
                for (long written = 0; written < length; written += zeros.Length)
                {
                    stream.Write(zeros, 0, (int)Math.Min(zeros.Length, length - written));
                }

                stream.Flush(flushToDisk: true);
            }

            File.Delete(path);
        }
        catch
        {
            // Best effort.
        }
    }
}
