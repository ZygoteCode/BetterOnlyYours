using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using OnlyYours.App.Infrastructure;
using OnlyYours.Core.Security.Memory;

namespace OnlyYours.App.Controls;

/// <summary>Something that can hand a secret to the display layer, in secure memory, on demand.</summary>
public interface ISecretSource
{
    /// <summary>Opens the secret; the caller disposes (and thereby wipes) the buffer.</summary>
    SecureCharBuffer? OpenSecret();
}

/// <summary>
/// Read-only secret display. Hidden, it shows a fixed row of dots (the length is not
/// revealed). Revealed, it decodes the secret into glyph indices — the characters are
/// wiped immediately — and draws them colour-coded in a monospace face, wrapping when
/// asked to. <see cref="Scramble"/> plays a "decryption" effect where random glyphs
/// settle, left to right, into the real ones.
/// </summary>
public sealed class SecretText : FrameworkElement
{
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source), typeof(ISecretSource), typeof(SecretText), new PropertyMetadata(null, (d, _) => ((SecretText)d).Reload()));

    public static readonly DependencyProperty IsRevealedProperty = DependencyProperty.Register(
        nameof(IsRevealed), typeof(bool), typeof(SecretText), new PropertyMetadata(false, (d, _) => ((SecretText)d).Reload()));

    public static readonly DependencyProperty ColorizeProperty = DependencyProperty.Register(
        nameof(Colorize), typeof(bool), typeof(SecretText), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty WrapProperty = DependencyProperty.Register(
        nameof(Wrap), typeof(bool), typeof(SecretText), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty FontSizeProperty = TextElement.FontSizeProperty.AddOwner(
        typeof(SecretText), new FrameworkPropertyMetadata(15.0, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty MaskLengthProperty = DependencyProperty.Register(
        nameof(MaskLength), typeof(int), typeof(SecretText), new FrameworkPropertyMetadata(12, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty VersionProperty = DependencyProperty.Register(
        nameof(Version), typeof(int), typeof(SecretText), new PropertyMetadata(0, (d, _) => ((SecretText)d).Reload()));

    private const string ScrambleSet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789#$%&*+=?@";
    private readonly Random _random = new();
    private readonly Stopwatch _scrambleClock = new();
    private ushort[] _glyphs = [];
    private byte[] _classes = [];
    private int _count;
    private bool _scrambling;
    private double _scrambleDuration = 650;

    public ISecretSource? Source
    {
        get => (ISecretSource?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public bool IsRevealed
    {
        get => (bool)GetValue(IsRevealedProperty);
        set => SetValue(IsRevealedProperty, value);
    }

    public bool Colorize
    {
        get => (bool)GetValue(ColorizeProperty);
        set => SetValue(ColorizeProperty, value);
    }

    public bool Wrap
    {
        get => (bool)GetValue(WrapProperty);
        set => SetValue(WrapProperty, value);
    }

    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public int MaskLength
    {
        get => (int)GetValue(MaskLengthProperty);
        set => SetValue(MaskLengthProperty, value);
    }

    /// <summary>Bump to force a reload (e.g. after the value changed).</summary>
    public int Version
    {
        get => (int)GetValue(VersionProperty);
        set => SetValue(VersionProperty, value);
    }

    public SecretText()
    {
        Unloaded += (_, _) => Wipe();
        Loaded += (_, _) =>
        {
            if (IsRevealed && _count == 0)
            {
                Reload();
            }
        };
    }

    /// <summary>Colour coding is on when both this control and the user preference allow it.</summary>
    private bool UseColors => Colorize && (ViewModels.AppServices.App?.Settings.ColorizePasswords ?? true);

    /// <summary>Plays the decryption effect on the current value.</summary>
    public void Scramble(double milliseconds = 650)
    {
        if (Motion.IsOff || !IsRevealed || _count == 0)
        {
            return;
        }

        _scrambleDuration = Motion.IsReduced ? milliseconds * 0.4 : milliseconds;
        _scrambleClock.Restart();
        if (!_scrambling)
        {
            _scrambling = true;
            CompositionTarget.Rendering += OnScrambleFrame;
        }
    }

    private void OnScrambleFrame(object? sender, EventArgs e)
    {
        if (_scrambleClock.Elapsed.TotalMilliseconds >= _scrambleDuration)
        {
            _scrambling = false;
            CompositionTarget.Rendering -= OnScrambleFrame;
        }

        InvalidateVisual();
    }

    private void Reload()
    {
        Wipe();
        if (IsRevealed && Source?.OpenSecret() is { } secret)
        {
            using (secret)
            {
                var chars = secret.Span;
                _count = chars.Length;
                _glyphs = new ushort[_count];
                _classes = new byte[_count];
                var map = SecureTextLayer.Mono.CharacterToGlyphMap;
                for (var i = 0; i < _count; i++)
                {
                    var c = chars[i];
                    var display = c switch { ' ' => '·', '\n' => '↵', '\r' => ' ', '\t' => '→', _ => c };
                    _glyphs[i] = map.TryGetValue(display, out var index) ? index : (ushort)0;
                    _classes[i] = c switch
                    {
                        '\n' => 4,
                        ' ' or '\t' or '\r' => 3,
                        _ when char.IsDigit(c) => 1,
                        _ when char.IsLetter(c) => 0,
                        _ => 2,
                    };
                }
            }
        }

        InvalidateMeasure();
        InvalidateVisual();
    }

    private void Wipe()
    {
        Array.Clear(_glyphs);
        Array.Clear(_classes);
        _glyphs = [];
        _classes = [];
        _count = 0;
        if (_scrambling)
        {
            _scrambling = false;
            CompositionTarget.Rendering -= OnScrambleFrame;
        }
    }

    private double Cell => SecureTextLayer.Mono.AdvanceWidths[SecureTextLayer.Mono.CharacterToGlyphMap['M']] * FontSize;

    private double LineHeight => SecureTextLayer.Mono.Height * FontSize * 1.18;

    private int Columns(double width) => Wrap && width > 0 && !double.IsInfinity(width) ? Math.Max(1, (int)(width / Cell)) : int.MaxValue;

    protected override Size MeasureOverride(Size availableSize)
    {
        var count = IsRevealed && _count > 0 ? _count : MaskLength;
        var columns = Columns(availableSize.Width);
        var lines = 1;
        var widest = 0;
        if (IsRevealed && _count > 0)
        {
            var column = 0;
            for (var i = 0; i < _count; i++)
            {
                if (_classes[i] == 4 || column >= columns)
                {
                    widest = Math.Max(widest, column);
                    column = _classes[i] == 4 ? 0 : 1;
                    lines++;
                    continue;
                }

                column++;
            }

            widest = Math.Max(widest, column);
        }
        else
        {
            widest = count;
        }

        var width = Math.Min(widest * Cell, double.IsInfinity(availableSize.Width) ? double.MaxValue : availableSize.Width);
        return new Size(width, lines * LineHeight);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var size = FontSize;
        var cell = Cell;
        var line = LineHeight;
        var mono = SecureTextLayer.Mono;
        var text = TryFindResource("Brush.Text") as Brush ?? Brushes.White;
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        if (!IsRevealed || _count == 0)
        {
            var dot = TryFindResource("Brush.TextMuted") as Brush ?? text;
            var radius = Math.Max(2.2, size * 0.17);
            for (var i = 0; i < MaskLength; i++)
            {
                dc.DrawEllipse(dot, null, new Point((i * cell) + (cell / 2), line / 2), radius, radius);
            }

            return;
        }

        Brush[] palette =
        [
            text,
            UseColors ? TryFindResource("Brush.TextAccent") as Brush ?? text : text,
            UseColors ? TryFindResource("Brush.Info") as Brush ?? text : text,
            TryFindResource("Brush.TextFaint") as Brush ?? text,
            TryFindResource("Brush.TextFaint") as Brush ?? text,
        ];
        var scrambleBrush = TryFindResource("Brush.TextFaint") as Brush ?? text;
        var dpi = (float)VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var baselineOffset = ((line - (mono.Height * size)) / 2) + (mono.Baseline * size);
        var columns = Columns(ActualWidth);
        var progress = _scrambling ? Math.Clamp(_scrambleClock.Elapsed.TotalMilliseconds / _scrambleDuration, 0, 1) : 1;
        var settled = (int)(progress * _count);
        var row = 0;
        var column = 0;
        for (var i = 0; i < _count; i++)
        {
            if (_classes[i] == 4 || column >= columns)
            {
                if (_classes[i] == 4)
                {
                    DrawGlyph(dc, mono, _glyphs[i], palette[4], size, dpi, column * cell, (row * line) + baselineOffset, cell);
                }

                row++;
                column = 0;
                if (_classes[i] == 4)
                {
                    continue;
                }
            }

            var glyph = _glyphs[i];
            var brush = palette[_classes[i]];
            if (i >= settled)
            {
                glyph = mono.CharacterToGlyphMap.TryGetValue(ScrambleSet[_random.Next(ScrambleSet.Length)], out var scrambled) ? scrambled : glyph;
                brush = scrambleBrush;
            }

            DrawGlyph(dc, mono, glyph, brush, size, dpi, column * cell, (row * line) + baselineOffset, cell);
            column++;
        }
    }

    private static void DrawGlyph(DrawingContext dc, GlyphTypeface mono, ushort glyph, Brush brush, double size, float dpi, double x, double baseline, double cell)
    {
        var run = new GlyphRun(mono, 0, false, size, dpi, [glyph], new Point(x, baseline), [cell], null, null, null, null, null, null);
        dc.DrawGlyphRun(brush, run);
    }
}
