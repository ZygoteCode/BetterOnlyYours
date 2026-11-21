using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using OnlyYours.App.Infrastructure;

namespace OnlyYours.App.Controls;

/// <summary>
/// The living background of every screen: obsidian ground, slow drifting metallic
/// glows, a whisper of film grain and a vignette. Everything is a static brush moved
/// by long composited animations, so it costs next to nothing per frame.
/// </summary>
public sealed class AmbientBackdrop : FrameworkElement
{
    private static ImageBrush? _grain;
    private readonly Grid _root = new();
    private readonly Rectangle _ground = new();
    private readonly Canvas _glowLayer = new();
    private readonly Ellipse[] _glows = [new(), new(), new()];
    private readonly Rectangle _noise = new();
    private readonly Rectangle _vignette = new();

    public AmbientBackdrop()
    {
        IsHitTestVisible = false;
        _ground.SetResourceReference(Shape.FillProperty, "Brush.Window");
        _root.ClipToBounds = true;
        _root.Children.Add(_ground);
        _root.Children.Add(_glowLayer);

        // Centre (fraction of the window), diameter (fraction of the longer side), drift period.
        var anchors = new (double Size, double X, double Y, double Seconds)[]
        {
            (1.1, 0.16, 0.10, 27),
            (0.95, 0.90, 0.95, 33),
            (0.7, 0.52, 0.58, 41),
        };

        for (var i = 0; i < _glows.Length; i++)
        {
            // A Canvas never clips children to a layout slot, so the huge soft glows keep
            // feathered edges wherever the drift takes them.
            var glow = _glows[i];
            glow.Tag = anchors[i];
            glow.RenderTransform = new TranslateTransform();
            _glowLayer.Children.Add(glow);
        }

        _noise.Fill = Grain();
        _noise.Opacity = 0.04;
        _root.Children.Add(_noise);

        var vignette = new RadialGradientBrush
        {
            RadiusX = 0.75,
            RadiusY = 0.75,
            GradientStops = { new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.55), new GradientStop(Color.FromArgb(0x70, 0, 0, 0), 1) },
        };
        vignette.Freeze();
        _vignette.Fill = vignette;
        _root.Children.Add(_vignette);

        AddVisualChild(_root);
        AddLogicalChild(_root);

        Loaded += (_, _) =>
        {
            ThemeManager.ThemeChanged += OnThemeChanged;
            Recolor();
            StartDrift();
        };
        Unloaded += (_, _) => ThemeManager.ThemeChanged -= OnThemeChanged;
        SizeChanged += (_, _) => Layout();
    }

    protected override int VisualChildrenCount => 1;

    protected override Visual GetVisualChild(int index) => _root;

    protected override Size MeasureOverride(Size availableSize)
    {
        _root.Measure(availableSize);
        return new Size(double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width, double.IsInfinity(availableSize.Height) ? 0 : availableSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _root.Arrange(new Rect(finalSize));
        return finalSize;
    }

    private void OnThemeChanged(object? sender, EventArgs e) => Recolor();

    private void Recolor()
    {
        var accent = TryFindResource("Color.Accent") is Color a ? a : Color.FromRgb(0xE3, 0xB2, 0x61);
        var dark = TryFindResource("Theme.IsDark") is not false;
        byte[] strengths = dark ? [0x2A, 0x1C, 0x12] : [0x2E, 0x22, 0x18];
        for (var i = 0; i < _glows.Length; i++)
        {
            var tint = i == 1 ? Shift(accent, -18) : accent;
            var brush = new RadialGradientBrush
            {
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(strengths[i], tint.R, tint.G, tint.B), 0),
                    new GradientStop(Color.FromArgb((byte)(strengths[i] / 3), tint.R, tint.G, tint.B), 0.45),
                    new GradientStop(Color.FromArgb(0, tint.R, tint.G, tint.B), 1),
                },
            };
            brush.Freeze();
            _glows[i].Fill = brush;
        }

        _vignette.Opacity = dark ? 1 : 0.18;
        _noise.Opacity = dark ? 0.045 : 0.03;
    }

    private void Layout()
    {
        var size = Math.Max(ActualWidth, ActualHeight);
        foreach (var glow in _glows)
        {
            var anchor = ((double Size, double X, double Y, double Seconds))glow.Tag;
            var diameter = size * anchor.Size;
            glow.Width = glow.Height = diameter;
            Canvas.SetLeft(glow, (ActualWidth * anchor.X) - (diameter / 2));
            Canvas.SetTop(glow, (ActualHeight * anchor.Y) - (diameter / 2));
        }
    }

    private void StartDrift()
    {
        if (Motion.IsOff)
        {
            return;
        }

        foreach (var glow in _glows)
        {
            var anchor = ((double Size, double X, double Y, double Seconds))glow.Tag;
            var transform = (TranslateTransform)glow.RenderTransform;
            var x = new DoubleAnimation(-60, 60, TimeSpan.FromSeconds(anchor.Seconds)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = Motion.EaseInOut };
            var y = new DoubleAnimation(40, -40, TimeSpan.FromSeconds(anchor.Seconds * 0.77)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = Motion.EaseInOut };
            Timeline.SetDesiredFrameRate(x, 20);
            Timeline.SetDesiredFrameRate(y, 20);
            transform.BeginAnimation(TranslateTransform.XProperty, x);
            transform.BeginAnimation(TranslateTransform.YProperty, y);
        }
    }

    private static Color Shift(Color c, int amount) => Color.FromRgb(
        (byte)Math.Clamp(c.R + amount, 0, 255),
        (byte)Math.Clamp(c.G + (amount / 2), 0, 255),
        (byte)Math.Clamp(c.B - amount, 0, 255));

    /// <summary>A tileable film-grain texture generated once at start-up.</summary>
    internal static ImageBrush Grain()
    {
        if (_grain is not null)
        {
            return _grain;
        }

        const int size = 160;
        var pixels = new byte[size * size * 4];
        var random = new Random(1337);
        for (var i = 0; i < size * size; i++)
        {
            var value = (byte)random.Next(0, 256);
            var alpha = (byte)random.Next(40, 140);
            var premultiplied = (byte)(value * alpha / 255);
            pixels[(i * 4) + 0] = premultiplied;
            pixels[(i * 4) + 1] = premultiplied;
            pixels[(i * 4) + 2] = premultiplied;
            pixels[(i * 4) + 3] = alpha;
        }

        var bitmap = BitmapSource.Create(size, size, 96, 96, PixelFormats.Pbgra32, null, pixels, size * 4);
        bitmap.Freeze();
        _grain = new ImageBrush(bitmap)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, size, size),
            ViewportUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.None,
        };
        _grain.Freeze();
        return _grain;
    }
}
