using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using OnlyYours.App.Controls;
using OnlyYours.App.Infrastructure;
using OnlyYours.App.Services;
using OnlyYours.Core.Analysis;
using OnlyYours.Core.Otp;
using OnlyYours.Core.Security.Memory;

namespace OnlyYours.App.ViewModels;

/// <summary>Service locator for the few things controls need without a view model (clipboard, settings).</summary>
public static class AppServices
{
    public static ClipboardService? Clipboard { get; set; }

    public static AppViewModel? App { get; set; }
}

public enum ToastKind
{
    Info,
    Success,
    Warning,
    Error,
}

public sealed class ToastViewModel : ObservableObject
{
    private readonly DispatcherTimer _timer;

    public ToastViewModel(string title, string? detail, ToastKind kind, TimeSpan duration, Action<ToastViewModel> dismiss)
    {
        Title = title;
        Detail = detail;
        Kind = kind;
        DismissCommand = new RelayCommand(() => dismiss(this));
        _timer = new DispatcherTimer { Interval = duration };
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            dismiss(this);
        };
        _timer.Start();
    }

    public string Title { get; }

    public string? Detail { get; }

    public ToastKind Kind { get; }

    public bool HasDetail => !string.IsNullOrEmpty(Detail);

    public string IconKey => Kind switch
    {
        ToastKind.Success => "Icon.Check",
        ToastKind.Warning => "Icon.Warning",
        ToastKind.Error => "Icon.ShieldAlert",
        _ => "Icon.Info",
    };

    public ICommand DismissCommand { get; }

    public string? ActionText { get; init; }

    public ICommand? ActionCommand { get; init; }

    public bool HasAction => ActionCommand is not null;

    public void Stop() => _timer.Stop();
}

/// <summary>Base for modal sheets shown in the overlay host.</summary>
public abstract class DialogViewModel : ObservableObject
{
    private readonly TaskCompletionSource<object?> _completion = new();
    private string? _error;

    protected DialogViewModel()
    {
        CancelCommand = new RelayCommand(() => Close(null));
    }

    public string Title { get; init; } = string.Empty;

    public string? Message { get; init; }

    public string IconKey { get; init; } = "Icon.Seal";

    public bool IsDanger { get; init; }

    public virtual double Width => 480;

    public string? Error
    {
        get => _error;
        set
        {
            if (Set(ref _error, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrEmpty(_error);

    public ICommand CancelCommand { get; }

    public Task<object?> Result => _completion.Task;

    public event Action<DialogViewModel>? Closed;

    public void Close(object? result)
    {
        if (_completion.TrySetResult(result))
        {
            OnClosed();
            Closed?.Invoke(this);
        }
    }

    protected virtual void OnClosed()
    {
    }
}

public sealed class ConfirmDialogViewModel : DialogViewModel
{
    public ConfirmDialogViewModel()
    {
        ConfirmCommand = new RelayCommand(() => Close(true));
    }

    public string ConfirmText { get; init; } = Loc.T("Common.Confirm");

    public string CancelText { get; init; } = Loc.T("Common.Cancel");

    public bool ShowCancel { get; init; } = true;

    public ICommand ConfirmCommand { get; }
}

/// <summary>Asks for a secret (master password, package password…). The caller owns the returned draft.</summary>
public sealed class PasswordDialogViewModel : DialogViewModel
{
    private SecretDraft _password = new();
    private SecretDraft? _confirm;
    private bool _handedOver;

    public PasswordDialogViewModel(bool confirm = false, bool showStrength = false)
    {
        if (confirm)
        {
            _confirm = new SecretDraft();
        }

        ShowStrength = showStrength;
        _password.Changed += (_, _) => UpdateStrength();
        SubmitCommand = new RelayCommand(Submit);
    }

    public SecretDraft Password => _password;

    public SecretDraft? Confirm => _confirm;

    public bool HasConfirm => _confirm is not null;

    public bool ShowStrength { get; }

    public string ConfirmText { get; init; } = Loc.T("Common.Continue");

    public int StrengthLevel { get; private set; }

    public string StrengthText { get; private set; } = string.Empty;

    public ICommand SubmitCommand { get; }

    private void UpdateStrength()
    {
        if (!ShowStrength)
        {
            return;
        }

        using var lease = _password.Read();
        var report = PasswordStrength.Evaluate(lease.Chars);
        StrengthLevel = (int)report.Level;
        StrengthText = report.Level == Core.Analysis.StrengthLevel.Empty ? string.Empty : Loc.T("Strength." + report.Level);
        OnPropertyChanged(nameof(StrengthLevel));
        OnPropertyChanged(nameof(StrengthText));
    }

    private void Submit()
    {
        if (_password.IsEmpty)
        {
            Error = Loc.T("Error.PasswordEmpty");
            return;
        }

        if (_confirm is not null && !_password.ContentEquals(_confirm))
        {
            Error = Loc.T("Error.PasswordMismatch");
            return;
        }

        _handedOver = true;
        Close(_password);
    }

    protected override void OnClosed()
    {
        _confirm?.Dispose();
        if (!_handedOver)
        {
            _password.Dispose();
        }
    }
}

public sealed class TextInputDialogViewModel : DialogViewModel
{
    private string _text = string.Empty;

    public TextInputDialogViewModel()
    {
        SubmitCommand = new RelayCommand(() =>
        {
            if (!AllowEmpty && string.IsNullOrWhiteSpace(Text))
            {
                Error = Loc.T("Error.Required");
                return;
            }

            Close(Text.Trim());
        });
    }

    public string Label { get; init; } = string.Empty;

    public string Placeholder { get; init; } = string.Empty;

    public bool Multiline { get; init; }

    public bool AllowEmpty { get; init; }

    public string ConfirmText { get; init; } = Loc.T("Common.Save");

    public string Text
    {
        get => _text;
        set => Set(ref _text, value);
    }

    public ICommand SubmitCommand { get; }

    public override double Width => Multiline ? 620 : 480;
}

/// <summary>Shows a recovery key once, with copy/save and an explicit acknowledgement.</summary>
public sealed class RecoveryKeyDialogViewModel : DialogViewModel, ISecretSource
{
    private readonly SecureCharBuffer _code;
    private bool _acknowledged;
    private bool _revealed = true;

    public RecoveryKeyDialogViewModel(SecureCharBuffer code)
    {
        _code = code;
        CopyCommand = new RelayCommand(() =>
        {
            AppServices.Clipboard?.CopySecret(_code.Span, Loc.T("Clipboard.RecoveryKey"));
        });
        SaveCommand = new RelayCommand(Save);
        DoneCommand = new RelayCommand(() => Close(true), () => Acknowledged);
    }

    public bool Acknowledged
    {
        get => _acknowledged;
        set => Set(ref _acknowledged, value);
    }

    public bool IsRevealed
    {
        get => _revealed;
        set => Set(ref _revealed, value);
    }

    public ICommand CopyCommand { get; }

    public ICommand SaveCommand { get; }

    public ICommand DoneCommand { get; }

    public override double Width => 600;

    public SecureCharBuffer? OpenSecret() => _code.IsDisposed ? null : SecureCharBuffer.CopyOf(_code.Span);

    private void Save()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            FileName = Loc.T("Recovery.FileName"),
            DefaultExt = ".txt",
            Filter = "Text|*.txt",
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        RecoveryKit.Write(dialog.FileName, _code, AppServices.App?.VaultDisplayName ?? "OnlyYours");
        AppServices.App?.Toast(Loc.T("Recovery.Saved"), dialog.FileName, ToastKind.Success);
    }

    protected override void OnClosed() => _code.Dispose();
}

/// <summary>Writes the printable recovery kit (a plain text sheet with instructions).</summary>
public static class RecoveryKit
{
    public static void Write(string path, SecureCharBuffer code, string vaultName)
    {
        using var output = new SecureCharBuffer(2048);
        output.Append(Loc.T("Recovery.KitHeader"));
        output.Append("\r\n\r\n");
        output.Append(Loc.F("Recovery.KitVault", vaultName));
        output.Append("\r\n");
        output.Append(Loc.F("Recovery.KitDate", DateTime.Now.ToString("d MMMM yyyy", Loc.Culture)));
        output.Append("\r\n\r\n    ");
        output.Append(code.Span);
        output.Append("\r\n\r\n");
        output.Append(Loc.T("Recovery.KitInstructions"));
        output.Append("\r\n");
        using var utf8 = output.ToUtf8();
        using var stream = new System.IO.FileStream(path, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.None);
        stream.Write(utf8.Span);
        stream.Flush(flushToDisk: true);
    }
}

/// <summary>Set up a two-factor token from an otpauth:// link or a base32 secret, with a live preview code.</summary>
public sealed class TotpSetupDialogViewModel : DialogViewModel
{
    private readonly SecretDraft _input = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private OtpSetup? _setup;
    private string _preview = string.Empty;
    private string _details = string.Empty;
    private double _progress;
    private int _seconds;

    public TotpSetupDialogViewModel(string issuer, string account)
    {
        Issuer = issuer;
        Account = account;
        _input.Changed += (_, _) => Parse();
        _timer.Tick += (_, _) => UpdateCode();
        SubmitCommand = new RelayCommand(() =>
        {
            if (_setup is null)
            {
                Error = Loc.T("Totp.Invalid");
                return;
            }

            var result = _setup;
            _setup = null;
            Close(result);
        });
    }

    public string Issuer { get; }

    public string Account { get; }

    public SecretDraft Input => _input;

    public bool IsValid => _setup is not null;

    public string Preview
    {
        get => _preview;
        private set => Set(ref _preview, value);
    }

    public string Details
    {
        get => _details;
        private set => Set(ref _details, value);
    }

    public double Progress
    {
        get => _progress;
        private set => Set(ref _progress, value);
    }

    public int Seconds
    {
        get => _seconds;
        private set => Set(ref _seconds, value);
    }

    public ICommand SubmitCommand { get; }

    public override double Width => 560;

    private void Parse()
    {
        _setup?.Dispose();
        _setup = null;
        Error = null;
        using (var lease = _input.Read())
        {
            if (lease.Chars.IsWhiteSpace())
            {
                Preview = string.Empty;
                Details = string.Empty;
                OnPropertyChanged(nameof(IsValid));
                _timer.Stop();
                return;
            }

            var defaults = new TotpConfig { Issuer = Issuer, Account = Account };
            if (!OtpAuthUri.TryParse(lease.Chars, defaults, out _setup, out var error))
            {
                Error = Loc.T("Totp.Error." + (error ?? "malformed"));
            }
        }

        OnPropertyChanged(nameof(IsValid));
        if (_setup is not null)
        {
            var config = _setup.Config;
            Details = Loc.F("Totp.Details", TotpConfig.AlgorithmLabel(config.Algorithm), config.EffectiveDigits, config.Period)
                + (string.IsNullOrEmpty(config.Issuer) ? string.Empty : $" · {config.Issuer}")
                + (string.IsNullOrEmpty(config.Account) ? string.Empty : $" · {config.Account}");
            UpdateCode();
            _timer.Start();
        }
        else
        {
            Preview = string.Empty;
            Details = string.Empty;
            _timer.Stop();
        }
    }

    private void UpdateCode()
    {
        if (_setup is null)
        {
            return;
        }

        var code = Totp.Generate(_setup.Secret.Span, _setup.Config, DateTimeOffset.Now);
        Preview = code.Grouped;
        Progress = code.Progress;
        Seconds = code.SecondsRemaining;
    }

    protected override void OnClosed()
    {
        _timer.Stop();
        _setup?.Dispose();
        _input.Dispose();
    }
}

/// <summary>A selectable line in multi-select dialogs (package preview, export selection).</summary>
public sealed class SelectableItem(string title, string subtitle, string iconKey, int index) : ObservableObject
{
    private bool _isSelected = true;

    public string Title { get; } = title;

    public string Subtitle { get; } = subtitle;

    public string IconKey { get; } = iconKey;

    public int Index { get; } = index;

    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value);
    }
}

public sealed class SelectionDialogViewModel : DialogViewModel
{
    public SelectionDialogViewModel(IEnumerable<SelectableItem> items)
    {
        Items = new ObservableCollection<SelectableItem>(items);
        SubmitCommand = new RelayCommand(() =>
        {
            var chosen = Items.Where(i => i.IsSelected).Select(i => i.Index).ToList();
            if (chosen.Count == 0)
            {
                Error = Loc.T("Error.NothingSelected");
                return;
            }

            Close(chosen);
        });
        SelectAllCommand = new RelayCommand(() =>
        {
            var all = Items.Any(i => !i.IsSelected);
            foreach (var item in Items)
            {
                item.IsSelected = all;
            }
        });
    }

    public ObservableCollection<SelectableItem> Items { get; }

    public string ConfirmText { get; init; } = Loc.T("Common.Continue");

    public string? Note { get; init; }

    public bool HasNote => !string.IsNullOrEmpty(Note);

    public ICommand SubmitCommand { get; }

    public ICommand SelectAllCommand { get; }

    public override double Width => 620;
}

/// <summary>Picks one of a list of options (used for recipients, KDF strength…).</summary>
public sealed class ChoiceDialogViewModel : DialogViewModel
{
    private ChoiceOption? _selected;

    public ChoiceDialogViewModel(IEnumerable<ChoiceOption> options)
    {
        Options = new ObservableCollection<ChoiceOption>(options);
        _selected = Options.FirstOrDefault(o => o.IsDefault) ?? Options.FirstOrDefault();
        SubmitCommand = new RelayCommand(() =>
        {
            if (Selected is null)
            {
                Error = Loc.T("Error.NothingSelected");
                return;
            }

            Close(Selected.Value);
        });
    }

    public ObservableCollection<ChoiceOption> Options { get; }

    public ChoiceOption? Selected
    {
        get => _selected;
        set => Set(ref _selected, value);
    }

    public string ConfirmText { get; init; } = Loc.T("Common.Continue");

    public ICommand SubmitCommand { get; }
}

public sealed record ChoiceOption(string Title, string Description, string IconKey, object Value, bool IsDefault = false);
