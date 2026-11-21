using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Threading;
using OnlyYours.App.Controls;
using OnlyYours.App.Infrastructure;
using OnlyYours.Core.Analysis;
using OnlyYours.Core.Otp;
using OnlyYours.Core.Security.Memory;
using OnlyYours.Core.Vault;
using OnlyYours.Core.Vault.Model;

namespace OnlyYours.App.ViewModels;

/// <summary>Display helpers shared by list, detail and editor.</summary>
public static class EntryText
{
    public static string KindName(EntryKind kind) => Loc.T("Kind." + kind);

    public static string KindIcon(EntryKind kind) => kind switch
    {
        EntryKind.SecureNote => "Icon.Note",
        EntryKind.PaymentCard => "Icon.Card",
        EntryKind.Identity => "Icon.Identity",
        EntryKind.WiFi => "Icon.Wifi",
        EntryKind.SshKey => "Icon.Terminal",
        EntryKind.ApiKey => "Icon.Code",
        EntryKind.BankAccount => "Icon.Bank",
        _ => "Icon.Key",
    };

    public static string FieldName(Field field) => !string.IsNullOrWhiteSpace(field.Label) ? field.Label! : FieldName(field.Kind);

    public static string FieldName(FieldKind kind) => Loc.T("FieldKind." + kind);

    public static string FieldIcon(FieldKind kind) => kind switch
    {
        FieldKind.Username or FieldKind.CardHolder or FieldKind.FullName or FieldKind.AccountHolder => "Icon.User",
        FieldKind.Email => "Icon.Mail",
        FieldKind.Password or FieldKind.PrivateKey or FieldKind.PublicKey => "Icon.Key",
        FieldKind.Url or FieldKind.CustomUrl => "Icon.Globe",
        FieldKind.Totp => "Icon.Timer",
        FieldKind.Notes => "Icon.Note",
        FieldKind.CardNumber or FieldKind.Iban => "Icon.Card",
        FieldKind.CardExpiry or FieldKind.BirthDate or FieldKind.CustomDate => "Icon.Calendar",
        FieldKind.CardCvv or FieldKind.Passphrase or FieldKind.ApiSecret or FieldKind.CustomSecret => "Icon.Lock",
        FieldKind.CardPin or FieldKind.ApiKeyId or FieldKind.Swift or FieldKind.AccountNumber or FieldKind.CustomNumber => "Icon.Hash",
        FieldKind.Phone => "Icon.Phone",
        FieldKind.Address => "Icon.MapPin",
        FieldKind.Company or FieldKind.BankName => "Icon.Bank",
        FieldKind.DocumentNumber => "Icon.Identity",
        FieldKind.Ssid => "Icon.Wifi",
        FieldKind.WifiSecurity => "Icon.Shield",
        FieldKind.Host => "Icon.Server",
        _ => "Icon.Text",
    };

    public static string Subtitle(Entry entry)
    {
        if (FieldKindInfo.SubtitleField(entry.Kind) is { } kind && entry.TextOf(kind) is { Length: > 0 } text)
        {
            return text;
        }

        if (entry.Username is { Length: > 0 } user)
        {
            return user;
        }

        if (entry.Host is { } host)
        {
            return host;
        }

        if (entry.Tags.Count > 0)
        {
            return string.Join(" · ", entry.Tags);
        }

        return KindName(entry.Kind);
    }
}

/// <summary>A row of the entry list.</summary>
public sealed class EntryItemViewModel : ObservableObject
{
    private IReadOnlyList<int> _matches = [];
    private EntryHealth? _health;

    public EntryItemViewModel(Entry entry)
    {
        Entry = entry;
    }

    public Entry Entry { get; }

    public Guid Id => Entry.Id;

    public string Title => string.IsNullOrWhiteSpace(Entry.Title) ? Loc.T("Entry.Untitled") : Entry.Title;

    public string Subtitle => EntryText.Subtitle(Entry);

    public string KindIcon => EntryText.KindIcon(Entry.Kind);

    public string KindName => EntryText.KindName(Entry.Kind);

    public bool IsFavorite => Entry.Favorite;

    public bool HasTotp => Entry.HasTotp;

    public bool HasUrl => Entry.Url is not null;

    public string? Host => Entry.Host;

    public IReadOnlyList<int> Matches
    {
        get => _matches;
        set => Set(ref _matches, value);
    }

    public EntryHealth? Health
    {
        get => _health;
        set
        {
            if (Set(ref _health, value))
            {
                OnPropertyChanged(nameof(Badge));
                OnPropertyChanged(nameof(HasBadge));
                OnPropertyChanged(nameof(BadgeText));
            }
        }
    }

    /// <summary>"danger", "warning" or null.</summary>
    public string? Badge => _health switch
    {
        { Expired: true } => "danger",
        { IsReused: true } => "danger",
        { IsWeak: true } => "warning",
        { ExpiringSoon: true } => "warning",
        _ => null,
    };

    public bool HasBadge => Badge is not null;

    public string BadgeText => _health switch
    {
        { Expired: true } => Loc.T("Health.Expired"),
        { IsReused: true } => Loc.T("Health.Reused"),
        { IsWeak: true } => Loc.T("Health.Weak"),
        { ExpiringSoon: true } => Loc.T("Health.Expiring"),
        _ => string.Empty,
    };

    public void Refresh() => OnPropertyChanged(string.Empty);
}

/// <summary>A field shown in the detail view.</summary>
public sealed class FieldViewModel : ObservableObject, ISecretSource
{
    private readonly ShellViewModel _shell;
    private bool _revealed;

    public FieldViewModel(ShellViewModel shell, Entry entry, Field field)
    {
        _shell = shell;
        Entry = entry;
        Field = field;
        CopyCommand = new RelayCommand(Copy);
        RevealCommand = new RelayCommand(() => IsRevealed = !IsRevealed);
        OpenCommand = new RelayCommand(Open, () => IsUrl);
    }

    public Entry Entry { get; }

    public Field Field { get; }

    public string Label => EntryText.FieldName(Field);

    public string IconKey => EntryText.FieldIcon(Field.Kind);

    public bool IsSensitive => Field.IsSensitive;

    public bool IsPlain => !Field.IsSensitive;

    public bool IsMultiline => Field.IsMultiline;

    public bool IsMonospace => Field.Kind.IsMonospace();

    public bool IsUrl => Field.Kind.IsUrl() && !string.IsNullOrWhiteSpace(Field.Text);

    public string? Text => Field.Text;

    public bool HasValue => Field.HasValue;

    public int MaskLength => Field.Kind is FieldKind.CardPin or FieldKind.CardCvv ? 4 : 12;

    public bool IsRevealed
    {
        get => _revealed;
        set
        {
            if (Set(ref _revealed, value) && value)
            {
                _shell.Session.RecordUse(Entry.Id);
            }
        }
    }

    public ICommand CopyCommand { get; }

    public ICommand RevealCommand { get; }

    public ICommand OpenCommand { get; }

    public SecureCharBuffer? OpenSecret() => _shell.Session.TryReveal(Entry, Field, out var plain) ? plain : null;

    private void Copy() => _shell.CopyField(Entry, Field);

    private void Open() => ShellViewModel.OpenUrl(Field.Text);
}

/// <summary>A superseded password in the history list.</summary>
public sealed class HistoryItemViewModel : ObservableObject, ISecretSource
{
    private readonly ShellViewModel _shell;
    private bool _revealed;

    public HistoryItemViewModel(ShellViewModel shell, Entry entry, HistoryItem item)
    {
        _shell = shell;
        Entry = entry;
        Item = item;
        CopyCommand = new RelayCommand(() =>
        {
            using var value = shell.Session.RevealHistory(entry, item);
            shell.App.Clipboard.CopySecret(value.Span, Loc.T("Clipboard.OldPassword"));
            shell.App.Toast(Loc.T("Toast.Copied"), Loc.T("Clipboard.OldPassword"), ToastKind.Success);
        });
        RestoreCommand = new AsyncCommand(async () =>
        {
            if (await shell.App.ConfirmAsync(Loc.T("History.RestoreTitle"), Loc.T("History.RestoreMessage"), Loc.T("History.Restore")))
            {
                shell.Session.RestoreHistory(entry.Id, item.Id);
                shell.App.Toast(Loc.T("History.Restored"), entry.Title, ToastKind.Success);
            }
        });
        ForgetCommand = new RelayCommand(() => shell.Session.ForgetHistory(entry.Id, item.Id));
        RevealCommand = new RelayCommand(() => IsRevealed = !IsRevealed);
    }

    public Entry Entry { get; }

    public HistoryItem Item { get; }

    public string When => Loc.Date(Item.ReplacedAt);

    public string Ago => Loc.Ago(Item.ReplacedAt);

    public bool IsRevealed
    {
        get => _revealed;
        set => Set(ref _revealed, value);
    }

    public ICommand CopyCommand { get; }

    public ICommand RestoreCommand { get; }

    public ICommand ForgetCommand { get; }

    public ICommand RevealCommand { get; }

    public SecureCharBuffer? OpenSecret()
    {
        try
        {
            return _shell.Session.RevealHistory(Entry, Item);
        }
        catch (VaultException)
        {
            return null;
        }
    }
}

/// <summary>Live one-time code with a countdown, refreshed by one shared ticker.</summary>
public sealed class TotpViewModel : ObservableObject
{
    private static readonly DispatcherTimer Ticker = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(100) };
    private static readonly List<WeakReference<TotpViewModel>> Live = [];
    private readonly ShellViewModel _shell;
    private long _counter = -1;
    private string _code = "––– –––";
    private double _progress;
    private int _seconds;
    private bool _active;

    static TotpViewModel()
    {
        Ticker.Tick += (_, _) =>
        {
            lock (Live)
            {
                Live.RemoveAll(w => !w.TryGetTarget(out var vm) || !vm._active);
                foreach (var weak in Live.ToList())
                {
                    if (weak.TryGetTarget(out var vm))
                    {
                        vm.Update();
                    }
                }

                if (Live.Count == 0)
                {
                    Ticker.Stop();
                }
            }
        };
    }

    public TotpViewModel(ShellViewModel shell, Entry entry, Field field)
    {
        _shell = shell;
        Entry = entry;
        Field = field;
        var config = shell.Session.TotpConfigOf(entry, field);
        Description = config is null
            ? Loc.T("Totp.Unreadable")
            : Loc.F("Totp.Details", TotpConfig.AlgorithmLabel(config.Algorithm), config.EffectiveDigits, config.Period)
                + (string.IsNullOrEmpty(config.Issuer) ? string.Empty : " · " + config.Issuer);
        CopyCommand = new RelayCommand(() => shell.CopyTotp(entry));
        Update();
    }

    public Entry Entry { get; }

    public Field Field { get; }

    public string Description { get; }

    public string Code
    {
        get => _code;
        private set => Set(ref _code, value);
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

    public bool IsExpiring => _seconds <= 5;

    public ICommand CopyCommand { get; }

    /// <summary>Raised when the code rolls over (the view plays a flip animation).</summary>
    public event Action? Rolled;

    public void Activate()
    {
        _active = true;
        lock (Live)
        {
            Live.Add(new WeakReference<TotpViewModel>(this));
            Ticker.Start();
        }
    }

    public void Deactivate() => _active = false;

    private void Update()
    {
        if (_shell.Session.IsDisposed)
        {
            _active = false;
            return;
        }

        var now = DateTimeOffset.Now;
        if (_shell.Session.GenerateTotp(Entry, Field, now) is not { } code)
        {
            Code = Loc.T("Totp.Unavailable");
            return;
        }

        if (code.Counter != _counter)
        {
            var rolled = _counter >= 0;
            _counter = code.Counter;
            Code = code.Grouped;
            if (rolled)
            {
                Rolled?.Invoke();
            }
        }

        Progress = code.Progress;
        if (Seconds != code.SecondsRemaining)
        {
            Seconds = code.SecondsRemaining;
            OnPropertyChanged(nameof(IsExpiring));
        }
    }
}

/// <summary>The read-only detail pane of the selected entry.</summary>
public sealed class EntryDetailViewModel : ObservableObject, IDisposable
{
    private readonly ShellViewModel _shell;
    private bool _historyExpanded;

    public EntryDetailViewModel(ShellViewModel shell, Entry entry)
    {
        _shell = shell;
        Entry = entry;
        foreach (var field in entry.Fields)
        {
            if (field.Kind == FieldKind.Totp)
            {
                if (field.Sealed is not null)
                {
                    Totp = new TotpViewModel(shell, entry, field);
                    Totp.Activate();
                }

                continue;
            }

            if (field.HasValue)
            {
                Fields.Add(new FieldViewModel(shell, entry, field));
            }
        }

        foreach (var item in entry.History)
        {
            History.Add(new HistoryItemViewModel(shell, entry, item));
        }

        if (entry.PrimaryPassword is { Sealed: not null } password && shell.Session.TryReveal(entry, password, out var plain))
        {
            using (plain)
            {
                Strength = PasswordStrength.Evaluate(plain!.Span);
            }
        }

        EditCommand = new RelayCommand(shell.BeginEdit);
        FavoriteCommand = new RelayCommand(() => shell.ToggleFavorite(entry));
        TrashCommand = new AsyncCommand(() => shell.TrashAsync(entry));
        DuplicateCommand = new RelayCommand(() => shell.Duplicate(entry));
        AutoTypeCommand = new AsyncCommand(() => shell.AutoTypeAsync(entry, null));
        ShareCommand = new AsyncCommand(() => shell.ShareAsync([entry]));
        SetupTotpCommand = new AsyncCommand(() => shell.SetupTotpAsync(entry));
        ToggleHistoryCommand = new RelayCommand(() => HistoryExpanded = !HistoryExpanded);
        ClearHistoryCommand = new AsyncCommand(async () =>
        {
            if (await shell.App.ConfirmAsync(Loc.T("History.ClearTitle"), Loc.T("History.ClearMessage"), Loc.T("History.Clear"), danger: true))
            {
                shell.Session.ClearHistory(entry.Id);
            }
        });
        OpenUrlCommand = new RelayCommand(() => ShellViewModel.OpenUrl(entry.Url), () => entry.Url is not null);
    }

    public Entry Entry { get; }

    public string Title => string.IsNullOrWhiteSpace(Entry.Title) ? Loc.T("Entry.Untitled") : Entry.Title;

    public string Subtitle => EntryText.Subtitle(Entry);

    public string KindName => EntryText.KindName(Entry.Kind);

    public string KindIcon => EntryText.KindIcon(Entry.Kind);

    public bool IsFavorite => Entry.Favorite;

    public IReadOnlyList<string> Tags => Entry.Tags;

    public bool HasTags => Entry.Tags.Count > 0;

    public ObservableCollection<FieldViewModel> Fields { get; } = [];

    public ObservableCollection<HistoryItemViewModel> History { get; } = [];

    public bool HasHistory => History.Count > 0;

    public string HistoryHeader => Loc.F("History.Header", History.Count);

    public bool HistoryExpanded
    {
        get => _historyExpanded;
        set => Set(ref _historyExpanded, value);
    }

    public TotpViewModel? Totp { get; }

    public bool HasTotp => Totp is not null;

    public bool CanHaveTotp => Entry.Kind == EntryKind.Login && Totp is null;

    public StrengthReport? Strength { get; }

    public bool HasStrength => Strength is not null;

    public int StrengthLevel => (int)(Strength?.Level ?? Core.Analysis.StrengthLevel.Empty);

    public string StrengthText => Strength is null ? string.Empty : Loc.T("Strength." + Strength.Level) + " · " + Loc.F("Strength.Bits", Math.Round(Strength.EntropyBits));

    public EntryHealth? Health => _shell.Health.For(Entry.Id);

    public string? HealthWarning => Health switch
    {
        { IsReused: true } h => Loc.F("Health.ReusedDetail", h.ReuseCount - 1),
        { IsWeak: true } => Loc.T("Health.WeakDetail"),
        { Expired: true } => Loc.T("Health.ExpiredDetail"),
        { IsOld: true } h => Loc.F("Health.OldDetail", (int)(h.Age.TotalDays / 30)),
        _ => null,
    };

    public bool HasHealthWarning => HealthWarning is not null;

    public string Created => Loc.Date(Entry.Created);

    public string Modified => Loc.Date(Entry.Modified);

    public string LastUsed => Entry.LastUsed is null ? Loc.T("Time.Never") : Loc.Ago(Entry.LastUsed);

    public string UseCount => Entry.UseCount.ToString(Loc.Culture);

    public string? Expires => Entry.Expires is { } expires ? Loc.ShortDate(expires) : null;

    public bool HasExpiry => Entry.Expires is not null;

    public bool CanAutoType => Entry.Kind == EntryKind.Login;

    public ICommand EditCommand { get; }

    public ICommand FavoriteCommand { get; }

    public ICommand TrashCommand { get; }

    public ICommand DuplicateCommand { get; }

    public ICommand AutoTypeCommand { get; }

    public ICommand ShareCommand { get; }

    public ICommand SetupTotpCommand { get; }

    public ICommand ToggleHistoryCommand { get; }

    public ICommand ClearHistoryCommand { get; }

    public ICommand OpenUrlCommand { get; }

    public void Dispose()
    {
        Totp?.Deactivate();
        foreach (var field in Fields)
        {
            field.IsRevealed = false;
        }
    }
}

/// <summary>Opens URLs in the default browser (only http/https; never anything else).</summary>
internal static class Browser
{
    public static void Open(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        var raw = url.Trim();
        if (!raw.Contains("://", StringComparison.Ordinal))
        {
            raw = "https://" + raw;
        }

        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            return;
        }

        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }
}
