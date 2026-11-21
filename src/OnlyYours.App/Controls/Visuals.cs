using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using OnlyYours.App.Infrastructure;

namespace OnlyYours.App.Controls;

/// <summary>A line icon from the OnlyYours set, stroked with the inherited foreground at a constant pixel width.</summary>
public sealed class Glyph : FrameworkElement
{
    public static readonly DependencyProperty DataProperty = DependencyProperty.Register(
        nameof(Data), typeof(Geometry), typeof(Glyph), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnGeometryChanged));

    public static readonly DependencyProperty ForegroundProperty = TextElement.ForegroundProperty.AddOwner(
        typeof(Glyph), new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender, OnPenChanged));

    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill), typeof(Brush), typeof(Glyph), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
        nameof(StrokeThickness), typeof(double), typeof(Glyph), new FrameworkPropertyMetadata(1.6, FrameworkPropertyMetadataOptions.AffectsRender, OnPenChanged));

    private Pen? _pen;
    private Geometry? _scaled;
    private Size _scaledFor;

    static Glyph()
    {
        WidthProperty.OverrideMetadata(typeof(Glyph), new FrameworkPropertyMetadata(18.0));
        HeightProperty.OverrideMetadata(typeof(Glyph), new FrameworkPropertyMetadata(18.0));
        SnapsToDevicePixelsProperty.OverrideMetadata(typeof(Glyph), new FrameworkPropertyMetadata(false));
        IsHitTestVisibleProperty.OverrideMetadata(typeof(Glyph), new UIPropertyMetadata(false));
    }

    public Geometry? Data
    {
        get => (Geometry?)GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public Brush Foreground
    {
        get => (Brush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public Brush? Fill
    {
        get => (Brush?)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    private static void OnGeometryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((Glyph)d)._scaled = null;

    private static void OnPenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((Glyph)d)._pen = null;

    protected override void OnRender(DrawingContext dc)
    {
        if (Data is null || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        var size = new Size(ActualWidth, ActualHeight);
        if (_scaled is null || _scaledFor != size)
        {
            var scale = Math.Min(size.Width, size.Height) / 24.0;
            var clone = Data.CloneCurrentValue();
            clone.Transform = new MatrixTransform(scale, 0, 0, scale, (size.Width - (24 * scale)) / 2, (size.Height - (24 * scale)) / 2);
            clone.Freeze();
            _scaled = clone;
            _scaledFor = size;
        }

        _pen ??= CreatePen();
        dc.DrawGeometry(Fill, _pen, _scaled);
    }

    private Pen CreatePen()
    {
        var pen = new Pen(Foreground, StrokeThickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round,
        };
        if (Foreground.IsFrozen)
        {
            pen.Freeze();
        }

        return pen;
    }
}

/// <summary>Entry avatar: a squircle with a deterministic metallic gradient and the entry's initial.</summary>
public sealed class Monogram : FrameworkElement
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(Monogram), new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsFavoriteProperty = DependencyProperty.Register(
        nameof(IsFavorite), typeof(bool), typeof(Monogram), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly (Color Top, Color Bottom)[] Palette =
    [
        (Color.FromRgb(0xE9, 0xBE, 0x72), Color.FromRgb(0x96, 0x64, 0x21)),
        (Color.FromRgb(0xDE, 0x8F, 0x62), Color.FromRgb(0x8A, 0x3E, 0x1E)),
        (Color.FromRgb(0xC7, 0xA4, 0x82), Color.FromRgb(0x6A, 0x4C, 0x31)),
        (Color.FromRgb(0x8D, 0xBE, 0xAB), Color.FromRgb(0x2F, 0x67, 0x55)),
        (Color.FromRgb(0x83, 0xA9, 0xD1), Color.FromRgb(0x2D, 0x50, 0x7C)),
        (Color.FromRgb(0xB2, 0x92, 0xCC), Color.FromRgb(0x58, 0x3A, 0x77)),
        (Color.FromRgb(0xCC, 0x86, 0x95), Color.FromRgb(0x77, 0x2F, 0x40)),
        (Color.FromRgb(0xA9, 0xBE, 0x75), Color.FromRgb(0x51, 0x66, 0x26)),
        (Color.FromRgb(0xE2, 0xC6, 0x6E), Color.FromRgb(0x8A, 0x6C, 0x1D)),
        (Color.FromRgb(0x73, 0xBD, 0xBC), Color.FromRgb(0x26, 0x66, 0x66)),
        (Color.FromRgb(0xA4, 0xA6, 0xC2), Color.FromRgb(0x46, 0x47, 0x69)),
        (Color.FromRgb(0xD3, 0xB0, 0x95), Color.FromRgb(0x7D, 0x55, 0x3B)),
    ];

    private static readonly Dictionary<int, Brush> BrushCache = [];
    private static readonly Typeface Face = new(new FontFamily("Segoe UI Variable Display, Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

    static Monogram()
    {
        WidthProperty.OverrideMetadata(typeof(Monogram), new FrameworkPropertyMetadata(40.0));
        HeightProperty.OverrideMetadata(typeof(Monogram), new FrameworkPropertyMetadata(40.0));
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public bool IsFavorite
    {
        get => (bool)GetValue(IsFavoriteProperty);
        set => SetValue(IsFavoriteProperty, value);
    }

    public static int PaletteIndex(string text)
    {
        unchecked
        {
            var hash = 17;
            foreach (var c in text.Trim().ToLowerInvariant())
            {
                hash = (hash * 31) + c;
            }

            return Math.Abs(hash % Palette.Length);
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0)
        {
            return;
        }

        var text = (Text ?? string.Empty).Trim();
        var index = PaletteIndex(text);
        if (!BrushCache.TryGetValue(index, out var brush))
        {
            var (top, bottom) = Palette[index];
            var gradient = new LinearGradientBrush(top, bottom, new Point(0.1, 0), new Point(0.9, 1));
            gradient.Freeze();
            BrushCache[index] = brush = gradient;
        }

        var radius = Math.Min(w, h) * 0.3;
        dc.DrawRoundedRectangle(brush, null, new Rect(0, 0, w, h), radius, radius);

        var sheen = new LinearGradientBrush(Color.FromArgb(0x4A, 0xFF, 0xFF, 0xFF), Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF), 90);
        sheen.Freeze();
        dc.DrawRoundedRectangle(sheen, null, new Rect(0, 0, w, h * 0.55), radius, radius);

        var initial = text.Length == 0 ? "?" : char.ToUpper(text[0], CultureInfo.CurrentCulture).ToString();
        var formatted = new FormattedText(initial, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, Face, h * 0.44, Brushes.White, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        var shadow = new FormattedText(initial, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, Face, h * 0.44, new SolidColorBrush(Color.FromArgb(0x50, 0, 0, 0)), VisualTreeHelper.GetDpi(this).PixelsPerDip);
        var origin = new Point((w - formatted.Width) / 2, (h - formatted.Height) / 2);
        dc.DrawText(shadow, new Point(origin.X, origin.Y + 1));
        dc.DrawText(formatted, origin);

        if (IsFavorite)
        {
            var ring = TryFindResource("Brush.Accent") as Brush ?? Brushes.Goldenrod;
            dc.DrawRoundedRectangle(null, new Pen(ring, 1.6), new Rect(-2.5, -2.5, w + 5, h + 5), radius + 2.5, radius + 2.5);
        }
    }
}

/// <summary>Circular countdown for one-time codes (smoothly animated, turns amber then ember before expiry).</summary>
public sealed class TotpRing : FrameworkElement
{
    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
        nameof(Progress), typeof(double), typeof(TotpRing), new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SecondsProperty = DependencyProperty.Register(
        nameof(Seconds), typeof(int), typeof(TotpRing), new FrameworkPropertyMetadata(30, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ThicknessProperty = DependencyProperty.Register(
        nameof(Thickness), typeof(double), typeof(TotpRing), new FrameworkPropertyMetadata(3.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShowSecondsProperty = DependencyProperty.Register(
        nameof(ShowSeconds), typeof(bool), typeof(TotpRing), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    static TotpRing()
    {
        WidthProperty.OverrideMetadata(typeof(TotpRing), new FrameworkPropertyMetadata(34.0));
        HeightProperty.OverrideMetadata(typeof(TotpRing), new FrameworkPropertyMetadata(34.0));
    }

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public int Seconds
    {
        get => (int)GetValue(SecondsProperty);
        set => SetValue(SecondsProperty, value);
    }

    public double Thickness
    {
        get => (double)GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    public bool ShowSeconds
    {
        get => (bool)GetValue(ShowSecondsProperty);
        set => SetValue(ShowSecondsProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 0)
        {
            return;
        }

        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        var radius = (size - Thickness) / 2;
        var track = TryFindResource("Brush.Line") as Brush ?? Brushes.DimGray;
        dc.DrawEllipse(null, new Pen(track, Thickness), center, radius, radius);

        var key = Seconds <= 5 ? "Brush.Danger" : Seconds <= 10 ? "Brush.Warning" : "Brush.Accent";
        var brush = TryFindResource(key) as Brush ?? Brushes.Goldenrod;
        var pen = new Pen(brush, Thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        var progress = Math.Clamp(Progress, 0, 1);
        if (progress >= 0.999)
        {
            dc.DrawEllipse(null, pen, center, radius, radius);
        }
        else if (progress > 0.001)
        {
            dc.DrawGeometry(null, pen, Arc(center, radius, -90, -90 + (360 * progress)));
        }

        if (ShowSeconds)
        {
            var text = new FormattedText(
                Seconds.ToString(CultureInfo.InvariantCulture),
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI Variable Text, Segoe UI"),
                size * 0.34,
                TryFindResource("Brush.TextMuted") as Brush ?? Brushes.Gray,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(text, new Point(center.X - (text.Width / 2), center.Y - (text.Height / 2)));
        }
    }

    internal static Geometry Arc(Point center, double radius, double startDegrees, double endDegrees)
    {
        var start = PointOn(center, radius, startDegrees);
        var end = PointOn(center, radius, endDegrees);
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(start, false, false);
            context.ArcTo(end, new Size(radius, radius), 0, endDegrees - startDegrees > 180, SweepDirection.Clockwise, true, false);
        }

        geometry.Freeze();
        return geometry;
    }

    private static Point PointOn(Point center, double radius, double degrees)
    {
        var radians = degrees * Math.PI / 180;
        return new Point(center.X + (radius * Math.Cos(radians)), center.Y + (radius * Math.Sin(radians)));
    }
}

/// <summary>Password strength bar: five segments that fill fluidly and change metal from ember to jade.</summary>
public sealed class StrengthMeter : FrameworkElement
{
    public static readonly DependencyProperty LevelProperty = DependencyProperty.Register(
        nameof(Level), typeof(int), typeof(StrengthMeter), new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender, OnLevelChanged));

    public static readonly DependencyProperty FractionProperty = DependencyProperty.Register(
        nameof(Fraction), typeof(double), typeof(StrengthMeter), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly DependencyProperty AnimatedFractionProperty = DependencyProperty.Register(
        "AnimatedFraction", typeof(double), typeof(StrengthMeter), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    static StrengthMeter()
    {
        HeightProperty.OverrideMetadata(typeof(StrengthMeter), new FrameworkPropertyMetadata(6.0));
    }

    /// <summary>0 = empty … 5 = excellent (mirrors <c>StrengthLevel</c>).</summary>
    public int Level
    {
        get => (int)GetValue(LevelProperty);
        set => SetValue(LevelProperty, value);
    }

    public double Fraction
    {
        get => (double)GetValue(FractionProperty);
        set => SetValue(FractionProperty, value);
    }

    private static void OnLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var meter = (StrengthMeter)d;
        var target = Math.Clamp((int)e.NewValue, 0, 5) / 5.0;
        Motion.Animate(meter, AnimatedFractionProperty, target, 480, Motion.QuintOut);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0)
        {
            return;
        }

        const int segments = 5;
        const double gap = 4;
        var segmentWidth = (w - (gap * (segments - 1))) / segments;
        var fill = (double)GetValue(AnimatedFractionProperty) * segments;
        var key = Level switch
        {
            <= 1 => "Brush.Danger",
            2 => "Brush.Warning",
            3 => "Brush.Accent",
            _ => "Brush.Success",
        };
        var active = TryFindResource(key) as Brush ?? Brushes.Goldenrod;
        var track = TryFindResource("Brush.Line") as Brush ?? Brushes.DimGray;
        for (var i = 0; i < segments; i++)
        {
            var rect = new Rect(i * (segmentWidth + gap), 0, segmentWidth, h);
            dc.DrawRoundedRectangle(track, null, rect, h / 2, h / 2);
            var amount = Math.Clamp(fill - i, 0, 1);
            if (amount > 0)
            {
                dc.DrawRoundedRectangle(active, null, new Rect(rect.X, 0, rect.Width * amount, h), h / 2, h / 2);
            }
        }
    }
}

/// <summary>Arc gauge for the vault health score, with an animated sweep and count-up.</summary>
public sealed class HealthGauge : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(HealthGauge), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender, OnValueChanged));

    public static readonly DependencyProperty CaptionProperty = DependencyProperty.Register(
        nameof(Caption), typeof(string), typeof(HealthGauge), new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly DependencyProperty DisplayValueProperty = DependencyProperty.Register(
        "DisplayValue", typeof(double), typeof(HealthGauge), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    static HealthGauge()
    {
        WidthProperty.OverrideMetadata(typeof(HealthGauge), new FrameworkPropertyMetadata(180.0));
        HeightProperty.OverrideMetadata(typeof(HealthGauge), new FrameworkPropertyMetadata(180.0));
    }

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string Caption
    {
        get => (string)GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        Motion.Animate((HealthGauge)d, DisplayValueProperty, (double)e.NewValue, 1400, Motion.ExpoOut);

    protected override void OnRender(DrawingContext dc)
    {
        var size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 0)
        {
            return;
        }

        var value = Math.Clamp((double)GetValue(DisplayValueProperty), 0, 100);
        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        var thickness = size * 0.075;
        var radius = (size - thickness) / 2 - 2;
        const double start = 135, sweep = 270;
        var track = TryFindResource("Brush.Line") as Brush ?? Brushes.DimGray;
        dc.DrawGeometry(null, new Pen(track, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, TotpRing.Arc(center, radius, start, start + sweep));

        var key = value < 40 ? "Color.Danger" : value < 65 ? "Color.Warning" : value < 85 ? "Color.Accent" : "Color.Success";
        var color = TryFindResource(key) is Color c ? c : Colors.Goldenrod;
        if (value > 0.5)
        {
            var glow = new Pen(new SolidColorBrush(Color.FromArgb(0x40, color.R, color.G, color.B)), thickness * 2.2) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            dc.DrawGeometry(null, glow, TotpRing.Arc(center, radius, start, start + (sweep * value / 100)));
            var pen = new Pen(new SolidColorBrush(color), thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            dc.DrawGeometry(null, pen, TotpRing.Arc(center, radius, start, start + (sweep * value / 100)));
        }

        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var number = new FormattedText(
            Math.Round(value).ToString(CultureInfo.CurrentCulture),
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI Variable Display, Segoe UI"), FontStyles.Normal, FontWeights.Light, FontStretches.Normal),
            size * 0.3,
            TryFindResource("Brush.Text") as Brush ?? Brushes.White,
            dpi);
        dc.DrawText(number, new Point(center.X - (number.Width / 2), center.Y - (number.Height / 2) - (size * 0.04)));

        if (!string.IsNullOrEmpty(Caption))
        {
            var caption = new FormattedText(
                Caption,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI Variable Text, Segoe UI"),
                size * 0.075,
                TryFindResource("Brush.TextFaint") as Brush ?? Brushes.Gray,
                dpi);
            dc.DrawText(caption, new Point(center.X - (caption.Width / 2), center.Y + (size * 0.17)));
        }
    }
}
