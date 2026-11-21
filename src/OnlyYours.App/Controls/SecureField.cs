using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using OnlyYours.App.Infrastructure;
using OnlyYours.App.Services;
using OnlyYours.App.ViewModels;
using OnlyYours.Core.Security.Memory;

namespace OnlyYours.App.Controls;

/// <summary>
/// The password field of OnlyYours — a from-scratch single-line editor whose text never
/// exists as a <see cref="string"/>. Keystrokes go straight into a <see cref="SecretDraft"/>
/// (kernel-encrypted, no-access pages between edits); the hidden state draws dots, the
/// revealed state draws glyphs with <see cref="GlyphRun"/> from the protected buffer,
/// colour-coding letters, digits and symbols. Paste and copy go through the secure
/// clipboard. Exposed to UI Automation as a password field (write-only value).
/// </summary>
[TemplatePart(Name = "PART_Layer", Type = typeof(SecureTextLayer))]
public sealed class SecureField : Control
{
    public static readonly DependencyProperty DraftProperty = DependencyProperty.Register(
        nameof(Draft), typeof(SecretDraft), typeof(SecureField), new PropertyMetadata(null, OnDraftChanged));

    public static readonly DependencyProperty IsRevealedProperty = DependencyProperty.Register(
        nameof(IsRevealed), typeof(bool), typeof(SecureField), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((SecureField)d).Refresh()));

    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(
        nameof(Placeholder), typeof(string), typeof(SecureField), new PropertyMetadata(string.Empty, (d, _) => ((SecureField)d).Refresh()));

    public static readonly DependencyProperty ColorizeProperty = DependencyProperty.Register(
        nameof(Colorize), typeof(bool), typeof(SecureField), new PropertyMetadata(true, (d, _) => ((SecureField)d).Refresh()));

    public static readonly DependencyProperty CanRevealProperty = DependencyProperty.Register(
        nameof(CanReveal), typeof(bool), typeof(SecureField), new PropertyMetadata(true));

    public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(
        nameof(IsReadOnly), typeof(bool), typeof(SecureField), new PropertyMetadata(false));

    public static readonly DependencyProperty MaxLengthProperty = DependencyProperty.Register(
        nameof(MaxLength), typeof(int), typeof(SecureField), new PropertyMetadata(1024));

    public static readonly DependencyProperty TrailingProperty = DependencyProperty.Register(
        nameof(Trailing), typeof(object), typeof(SecureField), new PropertyMetadata(null));

    public static readonly DependencyProperty IsEmptyProperty = DependencyProperty.Register(
        nameof(IsEmpty), typeof(bool), typeof(SecureField), new PropertyMetadata(true));

    public static readonly DependencyProperty CapsLockOnProperty = DependencyProperty.Register(
        nameof(CapsLockOn), typeof(bool), typeof(SecureField), new PropertyMetadata(false));

    public static readonly RoutedEvent SubmittedEvent = EventManager.RegisterRoutedEvent(
        nameof(Submitted), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(SecureField));

    private readonly DispatcherTimer _blink;
    private SecureTextLayer? _layer;
    private SecretDraft? _subscribed;
    private int _caret;
    private int _anchor;
    private bool _caretVisible = true;
    private bool _selecting;

    static SecureField()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(SecureField), new FrameworkPropertyMetadata(typeof(SecureField)));
        FocusableProperty.OverrideMetadata(typeof(SecureField), new FrameworkPropertyMetadata(true));
        InputMethod.IsInputMethodEnabledProperty.OverrideMetadata(typeof(SecureField), new FrameworkPropertyMetadata(false));
        CursorProperty.OverrideMetadata(typeof(SecureField), new FrameworkPropertyMetadata(Cursors.IBeam));
        KeyboardNavigation.IsTabStopProperty.OverrideMetadata(typeof(SecureField), new FrameworkPropertyMetadata(true));
    }

    public SecureField()
    {
        var interval = Interop.Native.GetCaretBlinkTime();
        _blink = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(interval is > 0 and < 5000 ? interval : 530),
        };
        _blink.Tick += (_, _) =>
        {
            _caretVisible = !_caretVisible;
            _layer?.InvalidateVisual();
        };
        Unloaded += (_, _) =>
        {
            _blink.Stop();
            Detach();
        };
        Loaded += (_, _) => Attach(Draft);
        ContextMenu = BuildContextMenu();
    }

    public event RoutedEventHandler Submitted
    {
        add => AddHandler(SubmittedEvent, value);
        remove => RemoveHandler(SubmittedEvent, value);
    }

    public SecretDraft? Draft
    {
        get => (SecretDraft?)GetValue(DraftProperty);
        set => SetValue(DraftProperty, value);
    }

    public bool IsRevealed
    {
        get => (bool)GetValue(IsRevealedProperty);
        set => SetValue(IsRevealedProperty, value);
    }

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public bool Colorize
    {
        get => (bool)GetValue(ColorizeProperty);
        set => SetValue(ColorizeProperty, value);
    }

    public bool CanReveal
    {
        get => (bool)GetValue(CanRevealProperty);
        set => SetValue(CanRevealProperty, value);
    }

    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    public int MaxLength
    {
        get => (int)GetValue(MaxLengthProperty);
        set => SetValue(MaxLengthProperty, value);
    }

    public object? Trailing
    {
        get => GetValue(TrailingProperty);
        set => SetValue(TrailingProperty, value);
    }

    public bool IsEmpty
    {
        get => (bool)GetValue(IsEmptyProperty);
        private set => SetValue(IsEmptyProperty, value);
    }

    public bool CapsLockOn
    {
        get => (bool)GetValue(CapsLockOnProperty);
        private set => SetValue(CapsLockOnProperty, value);
    }

    internal int CaretIndex => _caret;

    internal int SelectionStart => Math.Min(_caret, _anchor);

    internal int SelectionLength => Math.Abs(_caret - _anchor);

    internal bool CaretVisible => _caretVisible && IsKeyboardFocused && !IsReadOnly;

    internal int Length => Draft?.Length ?? 0;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _layer = GetTemplateChild("PART_Layer") as SecureTextLayer;
        if (_layer is not null)
        {
            _layer.Owner = this;
        }
    }

    public void SelectAll()
    {
        _anchor = 0;
        _caret = Length;
        Refresh();
    }

    public void ClearSecret()
    {
        Draft?.Clear();
        _caret = _anchor = 0;
        Refresh();
    }

    public void FocusField()
    {
        Focus();
        Keyboard.Focus(this);
    }

    private static void OnDraftChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var field = (SecureField)d;
        field.Attach(e.NewValue as SecretDraft);
        field._caret = field._anchor = field.Length;
        field.Refresh();
    }

    private void Attach(SecretDraft? draft)
    {
        Detach();
        if (draft is null || draft.IsDisposed)
        {
            IsEmpty = true;
            return;
        }

        _subscribed = draft;
        draft.Changed += OnDraftContentChanged;
        IsEmpty = draft.IsEmpty;
    }

    private void Detach()
    {
        if (_subscribed is not null)
        {
            _subscribed.Changed -= OnDraftContentChanged;
            _subscribed = null;
        }
    }

    private void OnDraftContentChanged(object? sender, EventArgs e)
    {
        _caret = Math.Min(_caret, Length);
        _anchor = Math.Min(_anchor, Length);
        IsEmpty = Length == 0;
        Refresh();
    }

    private void Refresh()
    {
        IsEmpty = Length == 0;
        _layer?.EnsureCaretVisible();
        _layer?.InvalidateVisual();
    }

    // ------------------------------------------------------------------ input

    protected override void OnTextInput(TextCompositionEventArgs e)
    {
        base.OnTextInput(e);
        if (IsReadOnly || Draft is not { IsDisposed: false } draft || string.IsNullOrEmpty(e.Text))
        {
            return;
        }

        Span<char> accepted = stackalloc char[Math.Min(e.Text.Length, 64)];
        var count = 0;
        foreach (var c in e.Text)
        {
            if (!char.IsControl(c) && count < accepted.Length)
            {
                accepted[count++] = c;
            }
        }

        if (count > 0)
        {
            Insert(draft, accepted[..count]);
        }

        accepted.Clear();
        e.Handled = true;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        UpdateCapsLock();
        if (Draft is not { IsDisposed: false } draft)
        {
            return;
        }

        var ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        var shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        switch (key)
        {
            case Key.Back when !IsReadOnly:
                if (SelectionLength > 0)
                {
                    DeleteSelection(draft);
                }
                else if (_caret > 0)
                {
                    var from = ctrl ? 0 : _caret - 1;
                    draft.Remove(from, _caret - from);
                    _caret = _anchor = from;
                }

                Touch();
                e.Handled = true;
                break;
            case Key.Delete when !IsReadOnly && !shift:
                if (SelectionLength > 0)
                {
                    DeleteSelection(draft);
                }
                else if (_caret < draft.Length)
                {
                    draft.Remove(_caret, ctrl ? draft.Length - _caret : 1);
                }

                Touch();
                e.Handled = true;
                break;
            case Key.Delete when shift:
                Cut(draft);
                e.Handled = true;
                break;
            case Key.Left:
                MoveCaret(ctrl ? 0 : (SelectionLength > 0 && !shift ? SelectionStart : _caret - 1), shift);
                e.Handled = true;
                break;
            case Key.Right:
                MoveCaret(ctrl ? draft.Length : (SelectionLength > 0 && !shift ? SelectionStart + SelectionLength : _caret + 1), shift);
                e.Handled = true;
                break;
            case Key.Home:
                MoveCaret(0, shift);
                e.Handled = true;
                break;
            case Key.End:
                MoveCaret(draft.Length, shift);
                e.Handled = true;
                break;
            case Key.A when ctrl:
                SelectAll();
                e.Handled = true;
                break;
            case Key.V when ctrl:
            case Key.Insert when shift:
                Paste(draft);
                e.Handled = true;
                break;
            case Key.C when ctrl:
            case Key.Insert when ctrl:
                Copy(draft);
                e.Handled = true;
                break;
            case Key.X when ctrl:
                Cut(draft);
                e.Handled = true;
                break;
            case Key.Enter:
                RaiseEvent(new RoutedEventArgs(SubmittedEvent, this));
                e.Handled = true;
                break;
            case Key.Z when ctrl:
            case Key.Y when ctrl:
                e.Handled = true;
                break;
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        UpdateCapsLock();
    }

    private void UpdateCapsLock() => CapsLockOn = Keyboard.IsKeyToggled(Key.CapsLock);

    private void Insert(SecretDraft draft, ReadOnlySpan<char> text)
    {
        if (SelectionLength > 0)
        {
            DeleteSelection(draft);
        }

        var room = Math.Max(0, MaxLength - draft.Length);
        if (room == 0)
        {
            return;
        }

        if (text.Length > room)
        {
            text = text[..room];
        }

        draft.Insert(_caret, text);
        _caret += text.Length;
        _anchor = _caret;
        Touch();
    }

    private void DeleteSelection(SecretDraft draft)
    {
        var start = SelectionStart;
        draft.Remove(start, SelectionLength);
        _caret = _anchor = start;
    }

    private void MoveCaret(int position, bool extend)
    {
        _caret = Math.Clamp(position, 0, Length);
        if (!extend)
        {
            _anchor = _caret;
        }

        Touch();
    }

    private void Touch()
    {
        _caretVisible = true;
        if (IsKeyboardFocused)
        {
            _blink.Stop();
            _blink.Start();
        }

        Refresh();
    }

    private void Paste(SecretDraft draft)
    {
        if (IsReadOnly || AppServices.Clipboard is not { } clipboard)
        {
            return;
        }

        using var buffer = new SecureCharBuffer(64);
        if (!clipboard.TryReadText(buffer))
        {
            return;
        }

        // A pasted password should not carry a trailing newline from the source.
        var span = buffer.Span;
        while (span.Length > 0 && span[^1] is '\r' or '\n')
        {
            span = span[..^1];
        }

        Span<char> filtered = span.Length <= 512 ? stackalloc char[span.Length] : new char[span.Length];
        var count = 0;
        foreach (var c in span)
        {
            if (!char.IsControl(c) || c == '\t')
            {
                filtered[count++] = c;
            }
        }

        Insert(draft, filtered[..count]);
        filtered.Clear();
    }

    private void Copy(SecretDraft draft)
    {
        if (AppServices.Clipboard is not { } clipboard || draft.Length == 0)
        {
            return;
        }

        using var lease = draft.Read();
        var span = SelectionLength > 0 ? lease.Chars.Slice(SelectionStart, SelectionLength) : lease.Chars;
        clipboard.CopySecret(span, Loc.T("Clipboard.Password"));
    }

    private void Cut(SecretDraft draft)
    {
        if (IsReadOnly)
        {
            return;
        }

        Copy(draft);
        if (SelectionLength > 0)
        {
            DeleteSelection(draft);
        }
        else
        {
            draft.Clear();
            _caret = _anchor = 0;
        }

        Touch();
    }

    // ------------------------------------------------------------------ mouse

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (!IsKeyboardFocusWithin)
        {
            FocusField();
        }

        if (_layer is null)
        {
            return;
        }

        if (e.ClickCount >= 2)
        {
            SelectAll();
            e.Handled = true;
            return;
        }

        var index = _layer.HitTest(e.GetPosition(_layer));
        MoveCaret(index, (Keyboard.Modifiers & ModifierKeys.Shift) != 0);
        _selecting = CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_selecting && _layer is not null)
        {
            MoveCaret(_layer.HitTest(e.GetPosition(_layer)), extend: true);
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_selecting)
        {
            _selecting = false;
            ReleaseMouseCapture();
        }
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        UpdateCapsLock();
        _caretVisible = true;
        _blink.Start();
        Refresh();
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        _blink.Stop();
        _anchor = _caret;
        Refresh();
    }

    private ContextMenu BuildContextMenu()
    {
        var menu = new ContextMenu();
        menu.Opened += (_, _) =>
        {
            menu.Items.Clear();
            var draft = Draft;
            menu.Items.Add(Item("Edit.Paste", "Ctrl+V", () => { if (draft is not null) { Paste(draft); } }, !IsReadOnly));
            menu.Items.Add(Item("Edit.Copy", "Ctrl+C", () => { if (draft is not null) { Copy(draft); } }, Length > 0));
            menu.Items.Add(Item("Edit.Cut", "Ctrl+X", () => { if (draft is not null) { Cut(draft); } }, Length > 0 && !IsReadOnly));
            menu.Items.Add(new Separator());
            menu.Items.Add(Item("Edit.SelectAll", "Ctrl+A", SelectAll, Length > 0));
            menu.Items.Add(Item(IsRevealed ? "Edit.Hide" : "Edit.Reveal", string.Empty, () => IsRevealed = !IsRevealed, CanReveal));
            menu.Items.Add(Item("Edit.Clear", string.Empty, ClearSecret, Length > 0 && !IsReadOnly));
        };
        return menu;
    }

    private static MenuItem Item(string key, string gesture, Action action, bool enabled)
    {
        var item = new MenuItem { Header = Loc.T(key), InputGestureText = gesture, IsEnabled = enabled };
        item.Click += (_, _) => action();
        return item;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new SecureFieldAutomationPeer(this);

    internal void SetFromAutomation(string value)
    {
        if (IsReadOnly || Draft is null)
        {
            throw new ElementNotEnabledException();
        }

        Draft.Set(value);
        _caret = _anchor = Draft.Length;
        Refresh();
    }
}

/// <summary>Draws the field's content: placeholder, masked dots or colour-coded glyphs, selection and caret.</summary>
public sealed class SecureTextLayer : FrameworkElement
{
    private static GlyphTypeface? _mono;
    private ushort[][] _glyphBuffers = [];
    private double _offset;

    public SecureTextLayer()
    {
        ClipToBounds = true;
    }

    internal SecureField? Owner { get; set; }

    internal static GlyphTypeface Mono
    {
        get
        {
            if (_mono is not null)
            {
                return _mono;
            }

            foreach (var family in new[] { "Cascadia Mono", "Consolas", "Courier New" })
            {
                if (new Typeface(new FontFamily(family), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal).TryGetGlyphTypeface(out var glyphs))
                {
                    return _mono = glyphs;
                }
            }

            throw new InvalidOperationException("No monospace font available.");
        }
    }

    private double FontSize => Owner?.FontSize ?? 14;

    private double Cell => Mono.AdvanceWidths[Glyph('M')] * FontSize;

    protected override Size MeasureOverride(Size availableSize)
    {
        var height = Mono.Height * FontSize * 1.1;
        return new Size(double.IsInfinity(availableSize.Width) ? 120 : availableSize.Width, height);
    }

    internal int HitTest(Point point)
    {
        var index = (int)Math.Round((point.X + _offset) / Cell);
        return Math.Clamp(index, 0, Owner?.Length ?? 0);
    }

    internal void EnsureCaretVisible()
    {
        if (Owner is null || ActualWidth <= 0)
        {
            return;
        }

        var caretX = Owner.CaretIndex * Cell;
        var padding = Cell * 2;
        if (caretX - _offset > ActualWidth - padding)
        {
            _offset = caretX - ActualWidth + padding;
        }
        else if (caretX - _offset < padding)
        {
            _offset = Math.Max(0, caretX - padding);
        }

        var contentWidth = (Owner.Length * Cell) + Cell;
        if (contentWidth <= ActualWidth)
        {
            _offset = 0;
        }
    }

    private static ushort Glyph(char c) => Mono.CharacterToGlyphMap.TryGetValue(c, out var index) ? index : (ushort)0;

    protected override void OnRender(DrawingContext dc)
    {
        // Glyph buffers of the previous frame are no longer referenced: wipe them.
        foreach (var buffer in _glyphBuffers)
        {
            Array.Clear(buffer);
        }

        _glyphBuffers = [];

        var owner = Owner;
        var width = ActualWidth;
        var height = ActualHeight;
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, width, height));
        if (owner is null || width <= 0)
        {
            return;
        }

        var size = FontSize;
        var cell = Cell;
        var dpi = (float)VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var baseline = ((height - (Mono.Height * size)) / 2) + (Mono.Baseline * size);
        var length = owner.Length;
        var text = owner.TryFindResource("Brush.Text") as Brush ?? owner.Foreground;

        if (length == 0)
        {
            if (!string.IsNullOrEmpty(owner.Placeholder))
            {
                var placeholder = new FormattedText(
                    owner.Placeholder,
                    CultureInfo.CurrentUICulture,
                    FlowDirection.LeftToRight,
                    new Typeface(owner.FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                    size,
                    owner.TryFindResource("Brush.TextFaint") as Brush ?? Brushes.Gray,
                    dpi);
                dc.DrawText(placeholder, new Point(0, (height - placeholder.Height) / 2));
            }
        }
        else
        {
            if (owner.SelectionLength > 0)
            {
                var selection = owner.TryFindResource("Brush.Selection") as Brush ?? Brushes.SteelBlue;
                var x = (owner.SelectionStart * cell) - _offset;
                dc.DrawRoundedRectangle(selection, null, new Rect(x, (height - (size * 1.35)) / 2, owner.SelectionLength * cell, size * 1.35), 3, 3);
            }

            if (owner.IsRevealed && owner.Draft is { IsDisposed: false } draft)
            {
                var letters = text;
                var digits = owner.Colorize ? owner.TryFindResource("Brush.TextAccent") as Brush ?? text : text;
                var symbols = owner.Colorize ? owner.TryFindResource("Brush.Info") as Brush ?? text : text;
                var faint = owner.TryFindResource("Brush.TextFaint") as Brush ?? text;
                using var lease = draft.Read();
                var chars = lease.Chars;
                var buffers = new List<ushort[]>(chars.Length);
                var first = Math.Max(0, (int)(_offset / cell) - 1);
                var last = Math.Min(chars.Length, (int)((_offset + width) / cell) + 2);
                for (var i = first; i < last; i++)
                {
                    var c = chars[i];
                    var display = c == ' ' ? '·' : c;
                    var glyphs = new ushort[] { Glyph(display) };
                    buffers.Add(glyphs);
                    var brush = c == ' ' ? faint : char.IsDigit(c) ? digits : char.IsLetter(c) ? letters : symbols;
                    var run = new GlyphRun(
                        Mono,
                        0,
                        false,
                        size,
                        dpi,
                        glyphs,
                        new Point((i * cell) - _offset, baseline),
                        [cell],
                        null,
                        null,
                        null,
                        null,
                        null,
                        null);
                    dc.DrawGlyphRun(brush, run);
                }

                _glyphBuffers = [.. buffers];
            }
            else
            {
                var dot = owner.TryFindResource("Brush.TextMuted") as Brush ?? text;
                var radius = Math.Max(2.2, size * 0.17);
                var y = height / 2;
                for (var i = 0; i < length; i++)
                {
                    var x = (i * cell) + (cell / 2) - _offset;
                    if (x < -cell || x > width + cell)
                    {
                        continue;
                    }

                    dc.DrawEllipse(dot, null, new Point(x, y), radius, radius);
                }
            }
        }

        if (owner.CaretVisible)
        {
            var caret = owner.TryFindResource("Brush.Accent") as Brush ?? text;
            var x = (owner.CaretIndex * cell) - _offset;
            dc.DrawRectangle(caret, null, new Rect(Math.Max(0, x - 0.75), (height - (size * 1.25)) / 2, 1.6, size * 1.25));
        }
    }
}

/// <summary>UI Automation: a password edit box whose value can be set but never read.</summary>
public sealed class SecureFieldAutomationPeer(SecureField owner) : FrameworkElementAutomationPeer(owner), IValueProvider
{
    private SecureField Field => (SecureField)Owner;

    public bool IsReadOnly => Field.IsReadOnly;

    public string Value => throw new InvalidOperationException("The value of a password field is not available.");

    public void SetValue(string value) => Field.SetFromAutomation(value);

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Edit;

    protected override string GetClassNameCore() => nameof(SecureField);

    protected override bool IsPasswordCore() => true;

    protected override string GetNameCore()
    {
        var name = base.GetNameCore();
        return string.IsNullOrEmpty(name) ? Field.Placeholder : name;
    }

    public override object? GetPattern(PatternInterface patternInterface) =>
        patternInterface == PatternInterface.Value ? this : base.GetPattern(patternInterface);
}
