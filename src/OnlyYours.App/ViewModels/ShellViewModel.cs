using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using OnlyYours.App.Infrastructure;
using OnlyYours.App.Services;
using OnlyYours.Core.Analysis;
using OnlyYours.Core.Search;
using OnlyYours.Core.Security.Memory;
using OnlyYours.Core.Vault;
using OnlyYours.Core.Vault.Model;

namespace OnlyYours.App.ViewModels;

public enum ShellPage
{
    Vault,
    Generator,
    Security,
    Sharing,
    Trash,
    Transfer,
    Settings,
}

public enum FilterKind
{
    All,
    Favorites,
    Recent,
    Kind,
    Tag,
    Health,
}

public sealed class RailItemViewModel : ObservableObject
{
    private int _count;
    private bool _isSelected;

    public required string Key { get; init; }

    public required string Title { get; init; }

    public required string IconKey { get; init; }

    public FilterKind Filter { get; init; }

    public EntryKind? Kind { get; init; }

    public string? Tag { get; init; }

    public string? HealthCategory { get; init; }

    public ShellPage Page { get; init; } = ShellPage.Vault;

    public int Count
    {
        get => _count;
        set
        {
            if (Set(ref _count, value))
            {
                OnPropertyChanged(nameof(HasCount));
            }
        }
    }

    public bool HasCount => _count > 0;

    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value);
    }

    public string? Accent { get; init; }
}

/// <summary>The unlocked vault: rail, list, detail/editor, pages, status bar.</summary>
public sealed class ShellViewModel : ObservableObject, IDisposable
{
    private readonly DispatcherTimer _refresh = new(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(50) };
    private readonly DispatcherTimer _healthTimer = new(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(1200) };
    private readonly DispatcherTimer _status = new(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
    private CancellationTokenSource? _healthCancel;
    private ShellPage _page = ShellPage.Vault;
    private object? _pageViewModel;
    private RailItemViewModel? _selectedFilter;
    private string _query = string.Empty;
    private string _sort;
    private EntryItemViewModel? _selected;
    private EntryDetailViewModel? _detail;
    private EntryEditorViewModel? _editor;
    private HealthReport _health = HealthReport.Empty;
    private bool _railCollapsed;

    public ShellViewModel(AppViewModel app, VaultSession session)
    {
        App = app;
        Session = session;
        _sort = app.Settings.EntrySort;
        _railCollapsed = app.Settings.RailCollapsed;
        session.Changed += OnSessionChanged;
        _refresh.Tick += (_, _) =>
        {
            _refresh.Stop();
            RefreshAll();
        };
        _healthTimer.Tick += (_, _) =>
        {
            _healthTimer.Stop();
            _ = AnalyzeHealthAsync();
        };
        _status.Tick += (_, _) => OnStatusTick();
        app.Clipboard.PropertyChanged += (_, _) => OnPropertyChanged(nameof(ClipboardText));

        NavigateCommand = new RelayCommand(p =>
        {
            if (p is ShellPage page)
            {
                Navigate(page);
            }
            else if (p is string name && Enum.TryParse<ShellPage>(name, out var parsed))
            {
                Navigate(parsed);
            }
        });
        SelectFilterCommand = new RelayCommand(p =>
        {
            if (p is RailItemViewModel item)
            {
                SelectFilter(item);
            }
        });
        NewEntryCommand = new RelayCommand(p => NewEntry(p is EntryKind kind ? kind : p is string s && Enum.TryParse<EntryKind>(s, out var k) ? k : EntryKind.Login));
        EditCommand = new RelayCommand(BeginEdit, () => _selected is not null && _editor is null);
        LockCommand = new AsyncCommand(() => app.LockAsync(null));
        PaletteCommand = new RelayCommand(app.OpenPalette);
        CopyPasswordCommand = new RelayCommand(() => WithSelected(e => CopyField(e, e.PrimaryPassword)));
        CopyUsernameCommand = new RelayCommand(() => WithSelected(CopyUsername));
        CopyTotpCommand = new RelayCommand(() => WithSelected(CopyTotp));
        OpenUrlCommand = new RelayCommand(() => WithSelected(e => OpenUrl(e.Url)));
        FavoriteCommand = new RelayCommand(() => WithSelected(ToggleFavorite));
        TrashCommand = new AsyncCommand(async () =>
        {
            if (_selected is { } item && _editor is null)
            {
                await TrashAsync(item.Entry);
            }
        });
        AutoTypeCommand = new AsyncCommand(async () =>
        {
            if (_selected is { } item)
            {
                await AutoTypeAsync(item.Entry, null);
            }
        });
        DuplicateCommand = new RelayCommand(() => WithSelected(Duplicate));
        ClearFiltersCommand = new RelayCommand(() =>
        {
            Query = string.Empty;
            SelectFilter(Rail.First());
        });
        ToggleRailCommand = new RelayCommand(() =>
        {
            RailCollapsed = !RailCollapsed;
        });
        SaveEditCommand = new AsyncCommand(async () =>
        {
            if (_editor?.SaveCommand is AsyncCommand save)
            {
                await save.ExecuteAsync();
            }
        });
        RetrySaveCommand = new RelayCommand(() => session.RequestSave());

        BuildRail();
        SelectFilter(Rail[0]);
        _status.Start();
        _healthTimer.Start();
        OnStatusTick();
    }

    public AppViewModel App { get; }

    public VaultSession Session { get; }

    public string VaultName => Session.Document.Name;

    public ObservableCollection<RailItemViewModel> Rail { get; } = [];

    public ObservableCollection<RailItemViewModel> RailTags { get; } = [];

    public ObservableCollection<RailItemViewModel> RailPages { get; } = [];

    public BulkObservableCollection<EntryItemViewModel> Items { get; } = [];

    public IReadOnlyList<string> AllTags => Session.Document.Entries.SelectMany(e => e.Tags).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.CurrentCultureIgnoreCase).ToList();

    public ShellPage Page
    {
        get => _page;
        private set
        {
            if (Set(ref _page, value))
            {
                OnPropertyChanged(nameof(IsVaultPage));
            }
        }
    }

    public bool IsVaultPage => _page == ShellPage.Vault;

    public object? PageViewModel
    {
        get => _pageViewModel;
        private set
        {
            var previous = _pageViewModel;
            if (Set(ref _pageViewModel, value) && previous is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    public RailItemViewModel? SelectedFilter
    {
        get => _selectedFilter;
        private set
        {
            if (Set(ref _selectedFilter, value))
            {
                OnPropertyChanged(nameof(ListTitle));
            }
        }
    }

    public string ListTitle => _selectedFilter?.Title ?? Loc.T("Rail.All");

    public string Query
    {
        get => _query;
        set
        {
            if (Set(ref _query, value))
            {
                RefreshList();
                OnPropertyChanged(nameof(HasQuery));
            }
        }
    }

    public bool HasQuery => !string.IsNullOrWhiteSpace(_query);

    public string Sort
    {
        get => _sort;
        set
        {
            if (Set(ref _sort, value))
            {
                App.Settings.EntrySort = value;
                App.SaveSettings();
                RefreshList();
            }
        }
    }

    public IReadOnlyList<ChoiceOption> SortOptions { get; } =
    [
        new(Loc.T("Sort.Title"), string.Empty, "Icon.Sort", "title"),
        new(Loc.T("Sort.Recent"), string.Empty, "Icon.Clock", "recent"),
        new(Loc.T("Sort.Modified"), string.Empty, "Icon.Edit", "modified"),
        new(Loc.T("Sort.Created"), string.Empty, "Icon.Calendar", "created"),
        new(Loc.T("Sort.Strength"), string.Empty, "Icon.Shield", "strength"),
    ];

    public EntryItemViewModel? Selected
    {
        get => _selected;
        set
        {
            if (ReferenceEquals(_selected, value))
            {
                return;
            }

            if (_editor is not null)
            {
                // Selecting another entry while editing asks the editor first.
                _ = ConfirmLeaveEditorAsync(() => SelectCore(value));
                OnPropertyChanged();
                return;
            }

            SelectCore(value);
        }
    }

    public EntryDetailViewModel? Detail
    {
        get => _detail;
        private set
        {
            var previous = _detail;
            if (Set(ref _detail, value))
            {
                previous?.Dispose();
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(ShowEmptyState));
            }
        }
    }

    public EntryEditorViewModel? Editor
    {
        get => _editor;
        private set
        {
            if (Set(ref _editor, value))
            {
                OnPropertyChanged(nameof(IsEditing));
                OnPropertyChanged(nameof(ShowEmptyState));
            }
        }
    }

    public bool IsEditing => _editor is not null;

    public bool HasSelection => _detail is not null;

    public bool ShowEmptyState => _detail is null && _editor is null;

    public bool IsEmptyVault => Session.Document.Entries.Count == 0;

    public bool IsEmptyList => Items.Count == 0;

    /// <summary>Filters or a search hide everything (as opposed to an empty vault).</summary>
    public bool ShowNoMatches => Items.Count == 0 && Session.Document.Entries.Count > 0;

    public bool RailCollapsed
    {
        get => _railCollapsed;
        set
        {
            if (Set(ref _railCollapsed, value))
            {
                App.Settings.RailCollapsed = value;
                App.SaveSettings();
            }
        }
    }

    public HealthReport Health
    {
        get => _health;
        private set
        {
            if (Set(ref _health, value))
            {
                OnPropertyChanged(nameof(HealthBadge));
                OnPropertyChanged(nameof(HasHealthBadge));
            }
        }
    }

    public int HealthBadge => _health.IssueCount;

    public bool HasHealthBadge => _health.IssueCount > 0;

    // ------------------------------------------------------------ status bar

    public string SaveText => Session.SaveState switch
    {
        SaveState.Saving => Loc.T("Status.Saving"),
        SaveState.Failed => Loc.T("Status.SaveFailed"),
        _ when Session.LastSaved is { } saved => Loc.F("Status.Saved", saved.LocalDateTime.ToString("HH:mm", Loc.Culture)),
        _ => Loc.T("Status.Ready"),
    };

    public bool SaveFailed => Session.SaveState == SaveState.Failed;

    public string CountText => Loc.F("Status.Entries", Session.Document.Entries.Count);

    public string CryptoText => $"{Session.PasswordKdf?.Describe() ?? "Argon2id"} · ChaCha20 + AES-256";

    public string? AutoLockText => App.AutoLock.Remaining is { } left ? Loc.F("Status.AutoLock", $"{(int)left.TotalMinutes}:{left.Seconds:00}") : null;

    public bool HasAutoLock => App.AutoLock.Remaining is not null;

    public string? ClipboardText => App.Clipboard.IsPending ? Loc.F("Status.Clipboard", App.Clipboard.SecondsRemaining) : null;

    public bool HasClipboard => App.Clipboard.IsPending;

    public double ClipboardProgress => App.Clipboard.Progress;

    public string MemoryText
    {
        get
        {
            var status = SecureMemory.Status;
            return Loc.F("Status.Memory", status.Regions, Loc.Bytes(status.LockedBytes));
        }
    }

    // --------------------------------------------------------------- commands

    public ICommand NavigateCommand { get; }

    public ICommand SelectFilterCommand { get; }

    public ICommand NewEntryCommand { get; }

    public ICommand EditCommand { get; }

    public ICommand LockCommand { get; }

    public ICommand PaletteCommand { get; }

    public ICommand CopyPasswordCommand { get; }

    public ICommand CopyUsernameCommand { get; }

    public ICommand CopyTotpCommand { get; }

    public ICommand OpenUrlCommand { get; }

    public ICommand FavoriteCommand { get; }

    public ICommand TrashCommand { get; }

    public ICommand AutoTypeCommand { get; }

    public ICommand DuplicateCommand { get; }

    public ICommand ClearFiltersCommand { get; }

    public ICommand ToggleRailCommand { get; }

    public ICommand SaveEditCommand { get; }

    public ICommand RetrySaveCommand { get; }

    /// <summary>Raised when the list should take keyboard focus on its search box.</summary>
    public event Action? FocusSearchRequested;

    public void FocusSearch() => FocusSearchRequested?.Invoke();

    // ============================================================ navigation

    public void Navigate(ShellPage page)
    {
        if (_editor is not null && page != ShellPage.Vault)
        {
            _ = ConfirmLeaveEditorAsync(() => NavigateCore(page));
            return;
        }

        NavigateCore(page);
    }

    private void NavigateCore(ShellPage page)
    {
        Page = page;
        PageViewModel = page switch
        {
            ShellPage.Generator => new GeneratorViewModel(this),
            ShellPage.Security => new SecurityViewModel(this),
            ShellPage.Sharing => new SharingViewModel(this),
            ShellPage.Trash => new TrashViewModel(this),
            ShellPage.Transfer => new TransferViewModel(this),
            ShellPage.Settings => new SettingsViewModel(App, this),
            _ => null,
        };

        foreach (var item in Rail.Concat(RailTags).Concat(RailPages))
        {
            item.IsSelected = page == ShellPage.Vault ? ReferenceEquals(item, _selectedFilter) : item.Page == page && item.Filter == FilterKind.All && RailPages.Contains(item);
        }
    }

    private void BuildRail()
    {
        Rail.Add(new RailItemViewModel { Key = "all", Title = Loc.T("Rail.All"), IconKey = "Icon.Grid", Filter = FilterKind.All });
        Rail.Add(new RailItemViewModel { Key = "fav", Title = Loc.T("Rail.Favorites"), IconKey = "Icon.Star", Filter = FilterKind.Favorites });
        Rail.Add(new RailItemViewModel { Key = "recent", Title = Loc.T("Rail.Recent"), IconKey = "Icon.Clock", Filter = FilterKind.Recent });
        foreach (var kind in Enum.GetValues<EntryKind>())
        {
            Rail.Add(new RailItemViewModel { Key = "kind:" + kind, Title = EntryText.KindName(kind), IconKey = EntryText.KindIcon(kind), Filter = FilterKind.Kind, Kind = kind });
        }

        RailPages.Add(new RailItemViewModel { Key = "page:generator", Title = Loc.T("Rail.Generator"), IconKey = "Icon.Wand", Page = ShellPage.Generator });
        RailPages.Add(new RailItemViewModel { Key = "page:security", Title = Loc.T("Rail.Security"), IconKey = "Icon.ShieldCheck", Page = ShellPage.Security });
        RailPages.Add(new RailItemViewModel { Key = "page:sharing", Title = Loc.T("Rail.Sharing"), IconKey = "Icon.Share", Page = ShellPage.Sharing });
        RailPages.Add(new RailItemViewModel { Key = "page:transfer", Title = Loc.T("Rail.Transfer"), IconKey = "Icon.Package", Page = ShellPage.Transfer });
        RailPages.Add(new RailItemViewModel { Key = "page:trash", Title = Loc.T("Rail.Trash"), IconKey = "Icon.Trash", Page = ShellPage.Trash });
        RailPages.Add(new RailItemViewModel { Key = "page:settings", Title = Loc.T("Rail.Settings"), IconKey = "Icon.Gear", Page = ShellPage.Settings });
        RefreshRail();
    }

    private void RefreshRail()
    {
        var entries = Session.Document.Entries;
        foreach (var item in Rail)
        {
            item.Count = item.Filter switch
            {
                FilterKind.All => entries.Count,
                FilterKind.Favorites => entries.Count(e => e.Favorite),
                FilterKind.Recent => entries.Count(e => e.LastUsed is not null),
                FilterKind.Kind => entries.Count(e => e.Kind == item.Kind),
                _ => item.Count,
            };
        }

        var tags = entries.SelectMany(e => e.Tags).GroupBy(t => t, StringComparer.OrdinalIgnoreCase).OrderBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase).ToList();
        var selectedKey = _selectedFilter?.Key;
        RailTags.Clear();
        foreach (var group in tags)
        {
            var item = new RailItemViewModel { Key = "tag:" + group.Key, Title = group.Key, IconKey = "Icon.Tag", Filter = FilterKind.Tag, Tag = group.Key, Count = group.Count() };
            RailTags.Add(item);
            if (item.Key == selectedKey)
            {
                item.IsSelected = true;
                _selectedFilter = item;
            }
        }

        if (RailPages.FirstOrDefault(p => p.Page == ShellPage.Trash) is { } trash)
        {
            trash.Count = Session.Document.Trash.Count;
        }

        if (RailPages.FirstOrDefault(p => p.Page == ShellPage.Security) is { } security)
        {
            security.Count = _health.IssueCount;
        }
    }

    public void SelectFilter(RailItemViewModel item)
    {
        if (item.Page != ShellPage.Vault && item.Filter == FilterKind.All && RailPages.Contains(item))
        {
            Navigate(item.Page);
            return;
        }

        SelectedFilter = item;
        if (_page != ShellPage.Vault)
        {
            NavigateCore(ShellPage.Vault);
        }

        foreach (var other in Rail.Concat(RailTags).Concat(RailPages))
        {
            other.IsSelected = ReferenceEquals(other, item);
        }

        RefreshList();
    }

    /// <summary>Shows the entries of one health category (from the security center).</summary>
    public void ShowHealthCategory(string category, string title)
    {
        var item = new RailItemViewModel { Key = "health:" + category, Title = title, IconKey = "Icon.ShieldAlert", Filter = FilterKind.Health, HealthCategory = category };
        SelectedFilter = item;
        NavigateCore(ShellPage.Vault);
        foreach (var other in Rail.Concat(RailTags).Concat(RailPages))
        {
            other.IsSelected = false;
        }

        RefreshList();
    }

    // ================================================================= list

    private void OnSessionChanged(object? sender, EventArgs e)
    {
        _refresh.Stop();
        _refresh.Start();
        _healthTimer.Stop();
        _healthTimer.Start();
    }

    private void RefreshAll()
    {
        RefreshRail();
        RefreshList();
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(IsEmptyVault));
        OnPropertyChanged(nameof(ShowNoMatches));
        OnPropertyChanged(nameof(VaultName));
        if (_detail is { } detail && _editor is null)
        {
            var entry = Session.Document.Entries.FirstOrDefault(e => e.Id == detail.Entry.Id);
            var expanded = detail.HistoryExpanded;
            Detail = entry is null ? null : new EntryDetailViewModel(this, entry) { HistoryExpanded = expanded };
        }

        (PageViewModel as IRefreshable)?.Refresh();
    }

    public void RefreshList()
    {
        var tokens = FuzzySearch.Tokenize(_query);
        IEnumerable<Entry> pool = Session.Document.Entries;
        var filter = _selectedFilter;
        pool = filter?.Filter switch
        {
            FilterKind.Favorites => pool.Where(e => e.Favorite),
            FilterKind.Recent => pool.Where(e => e.LastUsed is not null),
            FilterKind.Kind => pool.Where(e => e.Kind == filter.Kind),
            FilterKind.Tag => pool.Where(e => e.Tags.Contains(filter.Tag!, StringComparer.OrdinalIgnoreCase)),
            FilterKind.Health => pool.Where(e => MatchesHealth(e, filter.HealthCategory)),
            _ => pool,
        };

        List<(Entry Entry, int Score)> scored;
        if (tokens.Length > 0)
        {
            scored = pool.Select(e => (e, EntrySearch.Score(e, tokens))).Where(x => x.Item2 >= 0).OrderByDescending(x => x.Item2).ThenBy(x => x.e.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
        }
        else
        {
            var list = pool.ToList();
            IEnumerable<Entry> ordered = _sort switch
            {
                "recent" => list.OrderByDescending(e => e.LastUsed ?? DateTimeOffset.MinValue).ThenBy(e => e.Title, StringComparer.CurrentCultureIgnoreCase),
                "modified" => list.OrderByDescending(e => e.Modified),
                "created" => list.OrderByDescending(e => e.Created),
                "strength" => list.OrderBy(e => _health.For(e.Id)?.EntropyBits ?? double.MaxValue),
                _ => list.OrderBy(e => e.Title, StringComparer.CurrentCultureIgnoreCase),
            };
            scored = ordered.Select(e => (e, 0)).ToList();
        }

        if (filter?.Filter == FilterKind.Recent && tokens.Length == 0)
        {
            scored = scored.OrderByDescending(x => x.Entry.LastUsed).ToList();
        }

        var selectedId = _selected?.Id;
        var existing = Items.ToDictionary(i => i.Id);
        var items = new List<EntryItemViewModel>(scored.Count);
        foreach (var (entry, _) in scored)
        {
            var item = existing.TryGetValue(entry.Id, out var reuse) && ReferenceEquals(reuse.Entry, entry) ? reuse : new EntryItemViewModel(entry);
            item.Refresh();
            item.Health = _health.For(entry.Id);
            if (tokens.Length > 0)
            {
                var positions = new List<int>();
                FuzzySearch.Match(tokens, item.Title, positions);
                item.Matches = positions;
            }
            else
            {
                item.Matches = [];
            }

            items.Add(item);
        }

        Items.ReplaceAll(items);
        OnPropertyChanged(nameof(IsEmptyList));
        OnPropertyChanged(nameof(ShowNoMatches));
        var keep = selectedId is null ? null : items.FirstOrDefault(i => i.Id == selectedId);
        if (!ReferenceEquals(keep, _selected))
        {
            _selected = keep;
            OnPropertyChanged(nameof(Selected));
            if (keep is null && _editor is null)
            {
                Detail = null;
            }
        }
        else
        {
            OnPropertyChanged(nameof(Selected));
        }
    }

    private bool MatchesHealth(Entry entry, string? category)
    {
        var report = _health;
        return category switch
        {
            "weak" => report.Weak.Contains(entry.Id),
            "reused" => report.Reused.Contains(entry.Id),
            "old" => report.Old.Contains(entry.Id),
            "expired" => report.Expired.Contains(entry.Id) || report.ExpiringSoon.Contains(entry.Id),
            "totp" => report.MissingTotp.Contains(entry.Id),
            "empty" => report.EmptyPassword.Contains(entry.Id),
            _ => true,
        };
    }

    private void SelectCore(EntryItemViewModel? value)
    {
        _selected = value;
        OnPropertyChanged(nameof(Selected));
        Detail = value is null ? null : new EntryDetailViewModel(this, value.Entry);
        if (value is not null)
        {
            Session.RecordUse(value.Id);
        }
    }

    public void SelectEntry(Guid id)
    {
        if (_page != ShellPage.Vault)
        {
            NavigateCore(ShellPage.Vault);
        }

        var item = Items.FirstOrDefault(i => i.Id == id);
        if (item is null)
        {
            Query = string.Empty;
            SelectFilter(Rail[0]);
            item = Items.FirstOrDefault(i => i.Id == id);
        }

        Selected = item;
    }

    private void WithSelected(Action<Entry> action)
    {
        if (_selected is { } item)
        {
            action(item.Entry);
        }
    }

    // ================================================================ editing

    public void NewEntry(EntryKind kind)
    {
        if (_editor is not null)
        {
            return;
        }

        if (_page != ShellPage.Vault)
        {
            NavigateCore(ShellPage.Vault);
        }

        Editor = new EntryEditorViewModel(this, null, kind);
    }

    public void BeginEdit()
    {
        if (_selected is { } item && _editor is null)
        {
            Editor = new EntryEditorViewModel(this, item.Entry, item.Entry.Kind);
        }
    }

    public void CancelEdit()
    {
        var editor = _editor;
        Editor = null;
        editor?.Dispose();
    }

    public void EndEdit(Entry saved, bool created)
    {
        var editor = _editor;
        Editor = null;
        editor?.Dispose();
        RefreshAll();
        SelectEntry(saved.Id);
        App.Toast(created ? Loc.T("Entry.Created") : Loc.T("Entry.Saved"), saved.Title, ToastKind.Success);
    }

    private async Task ConfirmLeaveEditorAsync(Action then)
    {
        if (_editor is { IsDirty: true })
        {
            var discard = await App.ConfirmAsync(Loc.T("Editor.DiscardTitle"), Loc.T("Editor.DiscardMessage"), Loc.T("Editor.Discard"), danger: true);
            if (!discard)
            {
                OnPropertyChanged(nameof(Selected));
                return;
            }
        }

        CancelEdit();
        then();
    }

    // ================================================================ actions

    public void ToggleFavorite(Entry entry)
    {
        Session.SetFavorite(entry.Id, !entry.Favorite);
        App.Toast(entry.Favorite ? Loc.T("Entry.FavoriteOn") : Loc.T("Entry.FavoriteOff"), entry.Title, ToastKind.Info, TimeSpan.FromSeconds(2.5));
    }

    public async Task TrashAsync(Entry entry)
    {
        if (App.Settings.ConfirmDelete &&
            !await App.ConfirmAsync(Loc.T("Entry.TrashTitle"), Loc.F("Entry.TrashMessage", entry.Title, App.Settings.TrashRetentionDays), Loc.T("Entry.MoveToTrash"), danger: true))
        {
            return;
        }

        Session.MoveToTrash(entry.Id);
        if (_selected?.Id == entry.Id)
        {
            Detail = null;
            _selected = null;
            OnPropertyChanged(nameof(Selected));
        }

        App.Toast(Loc.T("Entry.Trashed"), entry.Title, ToastKind.Info, TimeSpan.FromSeconds(6), Loc.T("Common.Undo"), new RelayCommand(() =>
        {
            Session.RestoreFromTrash(entry.Id);
            SelectEntry(entry.Id);
        }));
    }

    public void Duplicate(Entry entry)
    {
        if (Session.Duplicate(entry.Id, Loc.T("Entry.CopySuffix")) is { } copy)
        {
            RefreshAll();
            SelectEntry(copy.Id);
            App.Toast(Loc.T("Entry.Duplicated"), copy.Title, ToastKind.Success);
        }
    }

    public void CopyField(Entry entry, Field? field)
    {
        if (field is null || !field.HasValue)
        {
            App.Toast(Loc.T("Toast.NothingToCopy"), null, ToastKind.Warning, TimeSpan.FromSeconds(2.5));
            return;
        }

        var label = EntryText.FieldName(field);
        if (field.IsSensitive)
        {
            if (!Session.TryReveal(entry, field, out var plain))
            {
                App.Toast(Loc.T("Toast.CopyFailed"), null, ToastKind.Error);
                return;
            }

            using (plain)
            {
                App.Clipboard.CopySecret(plain!.Span, label);
            }
        }
        else
        {
            App.Clipboard.CopyText(field.Text ?? string.Empty, label);
        }

        Session.RecordUse(entry.Id);
        var seconds = App.Settings.ClipboardClearSeconds;
        App.Toast(Loc.F("Toast.CopiedField", label), field.IsSensitive && seconds > 0 ? Loc.F("Toast.ClearsIn", seconds) : entry.Title, ToastKind.Success, TimeSpan.FromSeconds(2.8));
    }

    public void CopyUsername(Entry entry) => CopyField(entry, entry.FirstField(FieldKind.Username) ?? entry.FirstField(FieldKind.Email));

    public void CopyTotp(Entry entry)
    {
        if (entry.Totp is not { } field || Session.GenerateTotp(entry, field, DateTimeOffset.Now) is not { } code)
        {
            App.Toast(Loc.T("Totp.None"), entry.Title, ToastKind.Warning);
            return;
        }

        App.Clipboard.CopySecret(code.Value, Loc.T("Clipboard.Totp"));
        Session.RecordUse(entry.Id);
        App.Toast(Loc.T("Toast.TotpCopied"), Loc.F("Toast.TotpValid", code.SecondsRemaining), ToastKind.Success, TimeSpan.FromSeconds(2.8));
    }

    public static void OpenUrl(string? url) => Browser.Open(url);

    public async Task SetupTotpAsync(Entry entry)
    {
        var result = await App.ShowDialog(new TotpSetupDialogViewModel(entry.Title, entry.Username ?? string.Empty)
        {
            Title = Loc.T("Totp.SetupTitle"),
            Message = Loc.T("Totp.SetupMessage"),
            IconKey = "Icon.Timer",
        });
        if (result is Core.Otp.OtpSetup setup)
        {
            using (setup)
            {
                Session.SetTotp(entry.Id, setup);
            }

            App.Toast(Loc.T("Totp.Added"), entry.Title, ToastKind.Success);
        }
    }

    public Entry? FindForWindow(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var lower = title.ToLowerInvariant();
        var candidates = Session.Document.Entries
            .Where(e => e.Kind == EntryKind.Login)
            .Select(e => (Entry: e, Score: (e.Host is { } host && lower.Contains(host.ToLowerInvariant().Split('.')[0], StringComparison.Ordinal) ? 2 : 0)
                + (e.Title.Length > 2 && lower.Contains(e.Title.ToLowerInvariant(), StringComparison.Ordinal) ? 3 : 0)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Entry.LastUsed)
            .ToList();
        return candidates.Count == 0 ? null : candidates[0].Entry;
    }

    public async Task AutoTypeAsync(Entry entry, nint? target)
    {
        var window = target ?? App.AutoType.LastExternalWindow;
        if (window == 0)
        {
            App.Toast(Loc.T("AutoType.NoTarget"), Loc.T("AutoType.NoTargetDetail"), ToastKind.Warning);
            return;
        }

        var tokens = AutoTypeService.Parse(entry.AutoType);
        var ok = await App.AutoType.TypeAsync(window, tokens, kind => kind switch
        {
            AutoTypeTokenKind.Username => SecureCharBuffer.CopyOf(entry.Username ?? string.Empty),
            AutoTypeTokenKind.Password => entry.PrimaryPassword is { } p && Session.TryReveal(entry, p, out var plain) ? plain : null,
            AutoTypeTokenKind.Totp => entry.Totp is { } t && Session.GenerateTotp(entry, t, DateTimeOffset.Now) is { } code ? SecureCharBuffer.CopyOf(code.Value) : null,
            AutoTypeTokenKind.Url => SecureCharBuffer.CopyOf(entry.Url ?? string.Empty),
            AutoTypeTokenKind.Title => SecureCharBuffer.CopyOf(entry.Title),
            _ => null,
        }, App.Settings.AutoTypeDelayMs);

        Session.RecordUse(entry.Id);
        if (!ok)
        {
            App.Toast(Loc.T("AutoType.Aborted"), Loc.T("AutoType.AbortedDetail"), ToastKind.Warning);
        }
    }

    public async Task ShareAsync(IReadOnlyList<Entry> entries)
    {
        Navigate(ShellPage.Sharing);
        if (PageViewModel is SharingViewModel sharing)
        {
            await sharing.ShareEntriesAsync(entries);
        }
    }

    // ================================================================ health

    private async Task AnalyzeHealthAsync()
    {
        _healthCancel?.Cancel();
        var cancel = new CancellationTokenSource();
        _healthCancel = cancel;
        var snapshot = Session.Document.Entries.ToList();
        try
        {
            var report = await Task.Run(() => VaultHealth.Analyze(Session, snapshot, DateTimeOffset.Now, cancel.Token), cancel.Token);
            if (cancel.IsCancellationRequested || Session.IsDisposed)
            {
                return;
            }

            Health = report;
            foreach (var item in Items)
            {
                item.Health = report.For(item.Id);
            }

            RefreshRail();
            if (_detail is not null)
            {
                OnPropertyChanged(nameof(Detail));
            }

            (PageViewModel as IRefreshable)?.Refresh();
        }
        catch (OperationCanceledException)
        {
            // A newer analysis is on its way.
        }
        catch (Exception error)
        {
            AppErrors.Report(error);
        }
    }

    public void OnSaveStateChanged()
    {
        OnPropertyChanged(nameof(SaveText));
        OnPropertyChanged(nameof(SaveFailed));
    }

    private void OnStatusTick()
    {
        OnPropertyChanged(nameof(SaveText));
        OnPropertyChanged(nameof(SaveFailed));
        OnPropertyChanged(nameof(AutoLockText));
        OnPropertyChanged(nameof(HasAutoLock));
        OnPropertyChanged(nameof(ClipboardText));
        OnPropertyChanged(nameof(HasClipboard));
        OnPropertyChanged(nameof(ClipboardProgress));
        OnPropertyChanged(nameof(MemoryText));
    }

    public void Dispose()
    {
        _status.Stop();
        _refresh.Stop();
        _healthTimer.Stop();
        _healthCancel?.Cancel();
        Session.Changed -= OnSessionChanged;
        Detail?.Dispose();
        _editor?.Dispose();
        (PageViewModel as IDisposable)?.Dispose();
    }
}

/// <summary>Pages that rebuild themselves when the vault changes.</summary>
public interface IRefreshable
{
    void Refresh();
}
