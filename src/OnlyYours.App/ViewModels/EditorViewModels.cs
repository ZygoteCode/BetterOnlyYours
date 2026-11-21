using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using OnlyYours.App.Infrastructure;
using OnlyYours.Core.Analysis;
using OnlyYours.Core.Generators;
using OnlyYours.Core.Otp;
using OnlyYours.Core.Security.Memory;
using OnlyYours.Core.Vault;
using OnlyYours.Core.Vault.Model;

namespace OnlyYours.App.ViewModels;

/// <summary>One editable field: plain text, or a secret held in a <see cref="SecretDraft"/>.</summary>
public sealed class EditableFieldViewModel : ObservableObject, IDisposable
{
    private readonly EntryEditorViewModel _editor;
    private string? _text;
    private string _label;
    private bool _revealed;
    private int _originalVersion;
    private StrengthReport _strength = StrengthReport.Empty;

    public EditableFieldViewModel(EntryEditorViewModel editor, FieldKind kind, Guid id, string? label, string? text, SecretDraft? secret, FieldFlags flags)
    {
        _editor = editor;
        Kind = kind;
        Id = id;
        _label = label ?? string.Empty;
        _text = text;
        Flags = flags;
        Secret = secret;
        if (Secret is not null)
        {
            _originalVersion = Secret.Version;
            Secret.Changed += (_, _) => UpdateStrength();
            UpdateStrength();
        }

        RemoveCommand = new RelayCommand(() => editor.RemoveField(this));
        MoveUpCommand = new RelayCommand(() => editor.MoveField(this, -1));
        MoveDownCommand = new RelayCommand(() => editor.MoveField(this, +1));
        GenerateCommand = new RelayCommand(Generate, () => Secret is not null);
    }

    public Guid Id { get; }

    public FieldKind Kind { get; }

    public FieldFlags Flags { get; }

    public SecretDraft? Secret { get; }

    public bool IsSensitive => Secret is not null;

    public bool IsPlain => Secret is null;

    public bool IsCustom => Kind.IsCustom();

    public bool IsMultiline => Kind.IsMultiline() || (Flags & FieldFlags.Multiline) != 0;

    public bool IsSingleLine => !IsMultiline;

    public bool IsSecretSingleLine => IsSensitive && !IsMultiline;

    public bool IsSecretMultiline => IsSensitive && IsMultiline;

    public bool IsPlainSingleLine => IsPlain && !IsMultiline;

    public bool IsPlainMultiline => IsPlain && IsMultiline;

    public bool IsMonospace => Kind.IsMonospace();

    public bool CanGenerate => IsSensitive && Kind is FieldKind.Password or FieldKind.Passphrase or FieldKind.ApiSecret or FieldKind.CustomSecret or FieldKind.CardPin;

    public bool ShowStrength => Kind is FieldKind.Password or FieldKind.Passphrase;

    public string IconKey => EntryText.FieldIcon(Kind);

    public string DefaultLabel => EntryText.FieldName(Kind);

    public string Label
    {
        get => _label;
        set => Set(ref _label, value);
    }

    public string DisplayLabel => string.IsNullOrWhiteSpace(_label) ? DefaultLabel : _label;

    public string? Text
    {
        get => _text;
        set => Set(ref _text, value);
    }

    public bool IsRevealed
    {
        get => _revealed;
        set => Set(ref _revealed, value);
    }

    public bool IsChanged => Secret is not null && Secret.Version != _originalVersion;

    public int StrengthLevel => (int)_strength.Level;

    public string StrengthText => _strength.Level == Core.Analysis.StrengthLevel.Empty ? string.Empty : Loc.T("Strength." + _strength.Level);

    public ICommand RemoveCommand { get; }

    public ICommand MoveUpCommand { get; }

    public ICommand MoveDownCommand { get; }

    public ICommand GenerateCommand { get; }

    private void UpdateStrength()
    {
        if (!ShowStrength || Secret is null)
        {
            return;
        }

        using (var lease = Secret.Read())
        {
            _strength = PasswordStrength.Evaluate(lease.Chars);
        }

        OnPropertyChanged(nameof(StrengthLevel));
        OnPropertyChanged(nameof(StrengthText));
    }

    private void Generate()
    {
        if (Secret is null)
        {
            return;
        }

        var options = _editor.App.Settings.Generator.Clone();
        if (Kind == FieldKind.CardPin)
        {
            options.Mode = Core.Settings.GeneratorMode.Pin;
            options.PinLength = 4;
        }

        using var generated = PasswordGenerator.Generate(options);
        Secret.Set(generated.Value.Span);
        IsRevealed = true;
        GeneratedPulse?.Invoke();
    }

    public event Action? GeneratedPulse;

    public FieldEdit ToEdit() => new()
    {
        Id = Id,
        Kind = Kind,
        Label = IsCustom || !string.IsNullOrWhiteSpace(_label) ? _label : null,
        Flags = Flags,
        Text = Secret is null ? _text : null,
        Secret = Secret,
        SecretChanged = IsChanged,
    };

    public void Dispose() => Secret?.Dispose();
}

/// <summary>The inline editor for a new or existing entry.</summary>
public sealed class EntryEditorViewModel : ObservableObject, IDisposable
{
    private readonly ShellViewModel _shell;
    private readonly Entry? _original;
    private string _title;
    private bool _favorite;
    private EntryKind _kind;
    private string _newTag = string.Empty;
    private string? _expiresText;
    private string? _autoType;
    private string? _error;
    private OtpSetup? _pendingTotp;
    private bool _removeTotp;
    private bool _hasTotp;

    public EntryEditorViewModel(ShellViewModel shell, Entry? entry, EntryKind kind)
    {
        _shell = shell;
        _original = entry;
        _kind = entry?.Kind ?? kind;
        _title = entry?.Title ?? string.Empty;
        _favorite = entry?.Favorite ?? false;
        _autoType = entry?.AutoType;
        _expiresText = entry?.Expires?.LocalDateTime.ToString("d", Loc.Culture);
        _hasTotp = entry?.HasTotp ?? false;
        foreach (var tag in entry?.Tags ?? [])
        {
            Tags.Add(tag);
        }

        var source = entry ?? VaultSession.NewEntry(_kind, string.Empty);
        foreach (var field in source.Fields)
        {
            if (field.Kind == FieldKind.Totp)
            {
                continue;
            }

            Fields.Add(Create(field, entry));
        }

        SaveCommand = new AsyncCommand(SaveAsync);
        CancelCommand = new RelayCommand(shell.CancelEdit);
        AddTagCommand = new RelayCommand(AddTag);
        RemoveTagCommand = new RelayCommand(p =>
        {
            if (p is string tag)
            {
                Tags.Remove(tag);
            }
        });
        AddFieldCommand = new RelayCommand(p =>
        {
            if (p is FieldKind fieldKind)
            {
                AddField(fieldKind);
            }
            else if (p is string name && Enum.TryParse<FieldKind>(name, out var parsed))
            {
                AddField(parsed);
            }
        });
        SetupTotpCommand = new AsyncCommand(SetupTotpAsync);
        RemoveTotpCommand = new RelayCommand(() =>
        {
            _pendingTotp?.Dispose();
            _pendingTotp = null;
            _removeTotp = true;
            HasTotp = false;
        });
        SetExpiryCommand = new RelayCommand(p =>
        {
            if (p is string days && int.TryParse(days, out var count))
            {
                ExpiresText = count <= 0 ? null : DateTime.Today.AddDays(count).ToString("d", Loc.Culture);
            }
        });
    }

    public AppViewModel App => _shell.App;

    public bool IsNew => _original is null;

    public string Heading => IsNew ? Loc.F("Editor.NewTitle", EntryText.KindName(_kind)) : Loc.T("Editor.EditTitle");

    public string Title
    {
        get => _title;
        set => Set(ref _title, value);
    }

    public bool Favorite
    {
        get => _favorite;
        set => Set(ref _favorite, value);
    }

    public EntryKind Kind
    {
        get => _kind;
        set => Set(ref _kind, value);
    }

    public string KindIcon => EntryText.KindIcon(_kind);

    public ObservableCollection<string> Tags { get; } = [];

    public IReadOnlyList<string> KnownTags => _shell.AllTags;

    public string NewTag
    {
        get => _newTag;
        set => Set(ref _newTag, value);
    }

    public string? ExpiresText
    {
        get => _expiresText;
        set => Set(ref _expiresText, value);
    }

    public string? AutoType
    {
        get => _autoType;
        set => Set(ref _autoType, value);
    }

    public bool CanAutoType => _kind == EntryKind.Login;

    public bool CanHaveTotp => _kind == EntryKind.Login;

    public bool HasTotp
    {
        get => _hasTotp;
        private set => Set(ref _hasTotp, value);
    }

    public string TotpStatus => _pendingTotp is not null
        ? Loc.F("Totp.Pending", _pendingTotp.Config.Issuer)
        : HasTotp ? Loc.T("Totp.Configured") : Loc.T("Totp.None");

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

    public ObservableCollection<EditableFieldViewModel> Fields { get; } = [];

    public IReadOnlyList<FieldKind> AddableKinds { get; } =
    [
        FieldKind.Username, FieldKind.Email, FieldKind.Password, FieldKind.Url, FieldKind.Phone, FieldKind.Notes,
        FieldKind.CustomText, FieldKind.CustomSecret, FieldKind.CustomUrl, FieldKind.CustomNumber, FieldKind.CustomDate,
    ];

    public ICommand SaveCommand { get; }

    public ICommand CancelCommand { get; }

    public ICommand AddTagCommand { get; }

    public ICommand RemoveTagCommand { get; }

    public ICommand AddFieldCommand { get; }

    public ICommand SetupTotpCommand { get; }

    public ICommand RemoveTotpCommand { get; }

    public ICommand SetExpiryCommand { get; }

    /// <summary>True when something differs from the stored entry (used for the unsaved-changes guard).</summary>
    public bool IsDirty =>
        IsNew
            ? !string.IsNullOrWhiteSpace(_title) || Fields.Any(f => f.IsChanged || !string.IsNullOrEmpty(f.Text))
            : _title != _original!.Title || _favorite != _original.Favorite || Fields.Any(f => f.IsChanged) || _pendingTotp is not null || _removeTotp
              || !Tags.SequenceEqual(_original.Tags)
              || Fields.Count != _original.Fields.Count(f => f.Kind != FieldKind.Totp)
              || Fields.Any(f => f.IsPlain && _original.Fields.FirstOrDefault(o => o.Id == f.Id)?.Text != f.Text);

    private EditableFieldViewModel Create(Field field, Entry? owner)
    {
        SecretDraft? secret = null;
        var sensitive = field.IsSensitive || field.Kind.IsSensitive();
        if (sensitive)
        {
            secret = new SecretDraft();
            if (owner is not null && field.Sealed is not null && _shell.Session.TryReveal(owner, field, out var plain))
            {
                using (plain)
                {
                    secret.Set(plain!.Span);
                }
            }
        }

        return new EditableFieldViewModel(this, field.Kind, field.Id, field.Label, sensitive ? null : field.Text, secret, field.Flags);
    }

    public void AddField(FieldKind kind)
    {
        var field = new Field
        {
            Kind = kind,
            Flags = (kind.IsSensitive() ? FieldFlags.Sensitive : FieldFlags.None) | (kind.IsMultiline() ? FieldFlags.Multiline : FieldFlags.None),
            Label = kind.IsCustom() ? Loc.T("Editor.CustomLabel") : null,
        };
        var created = Create(field, null);
        var notesIndex = Fields.ToList().FindIndex(f => f.Kind == FieldKind.Notes);
        Fields.Insert(notesIndex >= 0 && kind != FieldKind.Notes ? notesIndex : Fields.Count, created);
    }

    public void RemoveField(EditableFieldViewModel field)
    {
        Fields.Remove(field);
        field.Dispose();
    }

    public void MoveField(EditableFieldViewModel field, int delta)
    {
        var index = Fields.IndexOf(field);
        var target = index + delta;
        if (index < 0 || target < 0 || target >= Fields.Count)
        {
            return;
        }

        Fields.Move(index, target);
    }

    private void AddTag()
    {
        foreach (var raw in NewTag.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Tags.Contains(raw, StringComparer.OrdinalIgnoreCase))
            {
                Tags.Add(raw);
            }
        }

        NewTag = string.Empty;
    }

    private async Task SetupTotpAsync()
    {
        var issuer = string.IsNullOrWhiteSpace(_title) ? string.Empty : _title.Trim();
        var account = Fields.FirstOrDefault(f => f.Kind is FieldKind.Username or FieldKind.Email)?.Text ?? string.Empty;
        var result = await App.ShowDialog(new TotpSetupDialogViewModel(issuer, account)
        {
            Title = Loc.T("Totp.SetupTitle"),
            Message = Loc.T("Totp.SetupMessage"),
            IconKey = "Icon.Timer",
        });
        if (result is OtpSetup setup)
        {
            _pendingTotp?.Dispose();
            _pendingTotp = setup;
            _removeTotp = false;
            HasTotp = true;
            OnPropertyChanged(nameof(TotpStatus));
        }
    }

    private async Task SaveAsync()
    {
        Error = null;
        if (NewTag.Trim().Length > 0)
        {
            AddTag();
        }

        if (string.IsNullOrWhiteSpace(_title))
        {
            Error = Loc.T("Editor.TitleRequired");
            return;
        }

        DateTimeOffset? expires = null;
        if (!string.IsNullOrWhiteSpace(_expiresText))
        {
            if (!DateTime.TryParse(_expiresText, Loc.Culture, DateTimeStyles.AssumeLocal, out var parsed))
            {
                Error = Loc.T("Editor.BadDate");
                return;
            }

            expires = new DateTimeOffset(parsed.Date);
        }

        var edit = new EntryEdit
        {
            EntryId = _original?.Id,
            Kind = _kind,
            Title = _title,
            Favorite = _favorite,
            Tags = [.. Tags],
            Expires = expires,
            AutoType = _autoType,
            Fields = [.. Fields.Select(f => f.ToEdit())],
        };

        // Keep the token field of an existing entry unless it is being replaced or removed.
        if (_original?.Totp is { } existing && !_removeTotp)
        {
            edit.Fields.Insert(Math.Max(0, edit.Fields.FindIndex(f => f.Kind == FieldKind.Notes) is var i && i >= 0 ? i : edit.Fields.Count), new FieldEdit
            {
                Id = existing.Id,
                Kind = FieldKind.Totp,
                Flags = FieldFlags.Sensitive,
            });
        }

        try
        {
            var saved = _shell.Session.Apply(edit);
            if (_pendingTotp is not null)
            {
                _shell.Session.SetTotp(saved.Id, _pendingTotp);
                _pendingTotp.Dispose();
                _pendingTotp = null;
            }

            _shell.EndEdit(saved, IsNew);
        }
        catch (Exception error)
        {
            Error = Errors.Describe(error);
        }

        await Task.CompletedTask;
    }

    public void Dispose()
    {
        foreach (var field in Fields)
        {
            field.Dispose();
        }

        _pendingTotp?.Dispose();
    }
}
