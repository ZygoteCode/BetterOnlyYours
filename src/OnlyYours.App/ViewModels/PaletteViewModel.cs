using System.Collections.ObjectModel;
using System.Windows.Input;
using OnlyYours.App.Infrastructure;
using OnlyYours.Core.Search;
using OnlyYours.Core.Vault.Model;

namespace OnlyYours.App.ViewModels;

public sealed class PaletteItem
{
    public required string Title { get; init; }

    public required string Subtitle { get; init; }

    public required string IconKey { get; init; }

    public string? Shortcut { get; init; }

    public Entry? Entry { get; init; }

    public Action? Run { get; init; }

    public IReadOnlyList<int> Matches { get; init; } = [];

    public bool IsEntry => Entry is not null;

    public bool HasShortcut => !string.IsNullOrEmpty(Shortcut);
}

/// <summary>
/// The command palette (Ctrl+K, and the global Ctrl+Alt+P quick search): fuzzy search
/// over entries and commands. Enter opens, Ctrl+Enter copies the password, Alt+Enter the
/// username, Ctrl+Shift+Enter the one-time code.
/// </summary>
public sealed class PaletteViewModel : ObservableObject
{
    private readonly AppViewModel _app;
    private readonly ShellViewModel _shell;
    private readonly List<PaletteItem> _commands;
    private string _query = string.Empty;
    private PaletteItem? _selected;

    public PaletteViewModel(AppViewModel app, ShellViewModel shell, bool quickMode = false)
    {
        _app = app;
        _shell = shell;
        QuickMode = quickMode;
        _commands =
        [
            Command("Palette.NewLogin", "Icon.Plus", "Ctrl+N", () => shell.NewEntry(EntryKind.Login)),
            Command("Palette.NewNote", "Icon.Note", null, () => shell.NewEntry(EntryKind.SecureNote)),
            Command("Palette.NewCard", "Icon.Card", null, () => shell.NewEntry(EntryKind.PaymentCard)),
            Command("Palette.Generator", "Icon.Wand", "Ctrl+G", () => shell.Navigate(ShellPage.Generator)),
            Command("Palette.Security", "Icon.ShieldCheck", null, () => shell.Navigate(ShellPage.Security)),
            Command("Palette.Sharing", "Icon.Share", null, () => shell.Navigate(ShellPage.Sharing)),
            Command("Palette.Transfer", "Icon.Package", null, () => shell.Navigate(ShellPage.Transfer)),
            Command("Palette.Trash", "Icon.Trash", null, () => shell.Navigate(ShellPage.Trash)),
            Command("Palette.Settings", "Icon.Gear", "Ctrl+,", () => shell.Navigate(ShellPage.Settings)),
            Command("Palette.Theme", "Icon.Moon", null, () =>
            {
                app.Settings.Theme = ThemeManager.IsDark ? "light" : "dark";
                App.ApplyAppearance(app.Settings);
                app.SaveSettings();
            }),
            Command("Palette.Lock", "Icon.Lock", "Ctrl+L", () => _ = app.LockAsync(null)),
        ];
        Results = [];
        Update();
    }

    public bool QuickMode { get; }

    public ObservableCollection<PaletteItem> Results { get; }

    public string Query
    {
        get => _query;
        set
        {
            if (Set(ref _query, value))
            {
                Update();
            }
        }
    }

    public PaletteItem? Selected
    {
        get => _selected;
        set => Set(ref _selected, value);
    }

    public bool IsEmpty => Results.Count == 0;

    /// <summary>Raised after an action ran, so the host can close.</summary>
    public event Action? Completed;

    private PaletteItem Command(string key, string icon, string? shortcut, Action run) => new()
    {
        Title = Loc.T(key),
        Subtitle = Loc.T("Palette.Command"),
        IconKey = icon,
        Shortcut = shortcut,
        Run = run,
    };

    private void Update()
    {
        var tokens = FuzzySearch.Tokenize(_query);
        var entries = _shell.Session.Document.Entries;
        IEnumerable<PaletteItem> entryItems;
        if (tokens.Length == 0)
        {
            entryItems = entries
                .OrderByDescending(e => e.LastUsed ?? DateTimeOffset.MinValue)
                .ThenBy(e => e.Title, StringComparer.CurrentCultureIgnoreCase)
                .Take(8)
                .Select(e => ForEntry(e, []));
        }
        else
        {
            entryItems = entries
                .Select(e => (Entry: e, Score: EntrySearch.Score(e, tokens)))
                .Where(x => x.Score >= 0)
                .OrderByDescending(x => x.Score)
                .Take(30)
                .Select(x =>
                {
                    var positions = new List<int>();
                    FuzzySearch.Match(tokens, x.Entry.Title, positions);
                    return ForEntry(x.Entry, positions);
                });
        }

        var commandItems = QuickMode ? [] : tokens.Length == 0
            ? _commands.Take(5)
            : _commands.Where(c => FuzzySearch.Match(tokens, c.Title) >= 0);

        Results.Clear();
        foreach (var item in entryItems.Concat(commandItems))
        {
            Results.Add(item);
        }

        Selected = Results.FirstOrDefault();
        OnPropertyChanged(nameof(IsEmpty));
    }

    private static PaletteItem ForEntry(Entry entry, IReadOnlyList<int> matches) => new()
    {
        Title = entry.Title,
        Subtitle = EntryText.Subtitle(entry),
        IconKey = EntryText.KindIcon(entry.Kind),
        Entry = entry,
        Matches = matches,
    };

    public void Move(int delta)
    {
        if (Results.Count == 0)
        {
            return;
        }

        var index = _selected is null ? -1 : Results.IndexOf(_selected);
        index = (index + delta + Results.Count) % Results.Count;
        Selected = Results[index];
    }

    public void Execute(PaletteItem? item, ModifierKeys modifiers)
    {
        item ??= _selected;
        if (item is null)
        {
            return;
        }

        if (item.Entry is { } entry)
        {
            if (modifiers.HasFlag(ModifierKeys.Control) && modifiers.HasFlag(ModifierKeys.Shift))
            {
                _shell.CopyTotp(entry);
            }
            else if (modifiers.HasFlag(ModifierKeys.Control))
            {
                _shell.CopyField(entry, entry.PrimaryPassword);
            }
            else if (modifiers.HasFlag(ModifierKeys.Alt))
            {
                _shell.CopyUsername(entry);
            }
            else
            {
                _app.RequestActivate();
                _shell.SelectEntry(entry.Id);
            }
        }
        else
        {
            item.Run?.Invoke();
        }

        Completed?.Invoke();
    }
}
