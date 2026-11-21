using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using OnlyYours.App.Infrastructure;

namespace OnlyYours.App.Controls;

/// <summary>
/// The living OnlyYours emblem — a minted seal built from individually animatable parts
/// (rings, a milled bezel of 72 ticks, a Latin inscription, a sunburst, the medallion
/// and its obsidian keyhole). It idles with slow counter-rotating rings and a breathing
/// glow, spins up while the key is being derived, "unlocks" like a combination dial
/// when the password is right and flares ember red when it is not. The cinematic intro
/// assembles it piece by piece from these same parts.
/// </summary>
public sealed class SealEmblem : FrameworkElement
{
    public const double Design = 400;
    public const double Center = Design / 2;

    public static readonly DependencyProperty IsIdleAnimatedProperty = DependencyProperty.Register(
        nameof(IsIdleAnimated), typeof(bool), typeof(SealEmblem), new PropertyMetadata(true, (d, _) => ((SealEmblem)d).UpdateIdle()));

    public static readonly DependencyProperty InscriptionProperty = DependencyProperty.Register(
        nameof(Inscription), typeof(string), typeof(SealEmblem), new PropertyMetadata("ONLYYOURS ✦ KEEPER OF SECRETS ✦ MMXXVI ✦ ", (d, _) => ((SealEmblem)d).BuildInscription()));

    private readonly Canvas _stage = new() { Width = Design, Height = Design };
    private readonly ScaleTransform _fit = new();
    private readonly RotateTransform _tickRotation = new(0, Center, Center);
    private readonly RotateTransform _inscriptionRotation = new(0, Center, Center);
    private readonly RotateTransform _keyholeRotation = new(0, Center, Center);
    private readonly RotateTransform _rayRotation = new(0, Center, Center);
    private readonly TranslateTransform _sheenTranslate = new(-320, 0);
    private bool _idleRunning;
    private bool _busy;

    public SealEmblem()
    {
        _stage.RenderTransform = _fit;
        Build();
        AddVisualChild(_stage);
        AddLogicalChild(_stage);
        Loaded += (_, _) =>
        {
            ThemeManager.ThemeChanged += OnThemeChanged;
            Recolor();
            UpdateIdle();
        };
        Unloaded += (_, _) =>
        {
            ThemeManager.ThemeChanged -= OnThemeChanged;
            StopIdle();
        };
        IsVisibleChanged += (_, _) => UpdateIdle();
    }

    public bool IsIdleAnimated
    {
        get => (bool)GetValue(IsIdleAnimatedProperty);
        set => SetValue(IsIdleAnimatedProperty, value);
    }

    public string Inscription
    {
        get => (string)GetValue(InscriptionProperty);
        set => SetValue(InscriptionProperty, value);
    }

    public Canvas Stage => _stage;

    public Ellipse Glow { get; } = new();

    public Ellipse OuterRing { get; } = new();

    public Ellipse MidRing { get; } = new();

    public Ellipse InnerRingA { get; } = new();

    public Ellipse InnerRingB { get; } = new();

    public Canvas TickRing { get; } = new() { Width = Design, Height = Design };

    public Canvas InscriptionRing { get; } = new() { Width = Design, Height = Design };

    public Canvas RayRing { get; } = new() { Width = Design, Height = Design };

    public List<Line> Ticks { get; } = [];

    public List<TextBlock> Letters { get; } = [];

    public List<Path> Rays { get; } = [];

    public Ellipse Medallion { get; } = new();

    public Ellipse MedallionRim { get; } = new();

    public Path Keyhole { get; } = new();

    public Canvas SheenHost { get; } = new() { Width = Design, Height = Design };

    public Rectangle Sheen { get; } = new();

    public Ellipse Flash { get; } = new();

    public Ellipse Shock { get; } = new();

    public Ellipse EmberTint { get; } = new();

    public RotateTransform TickRotation => _tickRotation;

    public RotateTransform InscriptionRotation => _inscriptionRotation;

    public RotateTransform KeyholeRotation => _keyholeRotation;

    protected override int VisualChildrenCount => 1;

    protected override Visual GetVisualChild(int index) => _stage;

    protected override Size MeasureOverride(Size availableSize)
    {
        _stage.Measure(new Size(Design, Design));
        var side = Math.Min(
            double.IsInfinity(availableSize.Width) ? Design : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? Design : availableSize.Height);
        return new Size(side, side);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var side = Math.Min(finalSize.Width, finalSize.Height);
        _fit.ScaleX = _fit.ScaleY = side / Design;
        _stage.Arrange(new Rect((finalSize.Width - side) / 2, (finalSize.Height - side) / 2, Design, Design));
        return finalSize;
    }

    private static void Place(FrameworkElement element, double radius)
    {
        element.Width = element.Height = radius * 2;
        Canvas.SetLeft(element, Center - radius);
        Canvas.SetTop(element, Center - radius);
    }

    private void Build()
    {
        Place(Glow, 200);
        Glow.IsHitTestVisible = false;
        _stage.Children.Add(Glow);

        Place(OuterRing, 176);
        OuterRing.StrokeThickness = 2.4;
        _stage.Children.Add(OuterRing);

        TickRing.RenderTransform = _tickRotation;
        for (var i = 0; i < 72; i++)
        {
            var angle = i * Math.PI * 2 / 72;
            var major = i % 6 == 0;
            var inner = major ? 152 : 158;
            var line = new Line
            {
                X1 = Center + (Math.Cos(angle) * inner),
                Y1 = Center + (Math.Sin(angle) * inner),
                X2 = Center + (Math.Cos(angle) * 169),
                Y2 = Center + (Math.Sin(angle) * 169),
                StrokeThickness = major ? 2.4 : 1.3,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
            };
            Ticks.Add(line);
            TickRing.Children.Add(line);
        }

        _stage.Children.Add(TickRing);

        Place(MidRing, 146);
        MidRing.StrokeThickness = 1.2;
        _stage.Children.Add(MidRing);

        InscriptionRing.RenderTransform = _inscriptionRotation;
        _stage.Children.Add(InscriptionRing);
        BuildInscription();

        Place(InnerRingA, 120);
        InnerRingA.StrokeThickness = 1.4;
        _stage.Children.Add(InnerRingA);
        Place(InnerRingB, 115);
        InnerRingB.StrokeThickness = 0.8;
        _stage.Children.Add(InnerRingB);

        RayRing.RenderTransform = _rayRotation;
        for (var i = 0; i < 24; i++)
        {
            var angle = i * Math.PI * 2 / 24;
            var half = Math.PI / 24 * 0.42;
            var outer = i % 2 == 0 ? 108 : 94;
            const double inner = 80;
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(new Point(Center + (Math.Cos(angle - half) * inner), Center + (Math.Sin(angle - half) * inner)), true, true);
                context.LineTo(new Point(Center + (Math.Cos(angle) * outer), Center + (Math.Sin(angle) * outer)), true, false);
                context.LineTo(new Point(Center + (Math.Cos(angle + half) * inner), Center + (Math.Sin(angle + half) * inner)), true, false);
            }

            geometry.Freeze();
            var ray = new Path { Data = geometry };
            Rays.Add(ray);
            RayRing.Children.Add(ray);
        }

        _stage.Children.Add(RayRing);

        Place(Medallion, 74);
        _stage.Children.Add(Medallion);
        Place(MedallionRim, 74);
        MedallionRim.StrokeThickness = 1.6;
        _stage.Children.Add(MedallionRim);

        Keyhole.Data = SealArt.Keyhole(Center, Center, 100);
        Keyhole.RenderTransform = _keyholeRotation;
        var hole = new LinearGradientBrush(Color.FromRgb(0x04, 0x03, 0x02), Color.FromRgb(0x26, 0x1D, 0x12), 90);
        hole.Freeze();
        Keyhole.Fill = hole;
        _stage.Children.Add(Keyhole);

        Sheen.Width = 110;
        Sheen.Height = 520;
        Canvas.SetLeft(Sheen, Center - 55);
        Canvas.SetTop(Sheen, Center - 260);
        var sheenBrush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0.5),
            EndPoint = new Point(1, 0.5),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(0, 0xFF, 0xFA, 0xEC), 0),
                new GradientStop(Color.FromArgb(0x6E, 0xFF, 0xFA, 0xEC), 0.5),
                new GradientStop(Color.FromArgb(0, 0xFF, 0xFA, 0xEC), 1),
            },
        };
        sheenBrush.Freeze();
        Sheen.Fill = sheenBrush;
        Sheen.RenderTransform = new TransformGroup { Children = { new RotateTransform(24, 55, 260), _sheenTranslate } };
        SheenHost.Children.Add(Sheen);
        SheenHost.Clip = new EllipseGeometry(new Point(Center, Center), 178, 178);
        SheenHost.IsHitTestVisible = false;
        _stage.Children.Add(SheenHost);

        Place(Flash, 76);
        Flash.Opacity = 0;
        var flashBrush = new RadialGradientBrush(Color.FromArgb(0xE0, 0xFF, 0xF8, 0xE4), Color.FromArgb(0, 0xFF, 0xF8, 0xE4));
        flashBrush.Freeze();
        Flash.Fill = flashBrush;
        _stage.Children.Add(Flash);

        Place(EmberTint, 178);
        EmberTint.Opacity = 0;
        _stage.Children.Add(EmberTint);

        Place(Shock, 70);
        Shock.Opacity = 0;
        Shock.StrokeThickness = 3;
        Shock.RenderTransformOrigin = new Point(0.5, 0.5);
        Shock.RenderTransform = new ScaleTransform(1, 1);
        _stage.Children.Add(Shock);
    }

    private void BuildInscription()
    {
        InscriptionRing.Children.Clear();
        Letters.Clear();
        var text = Inscription ?? string.Empty;
        if (text.Length == 0)
        {
            return;
        }

        const double radius = 133;
        var typeface = new Typeface(new FontFamily("Palatino Linotype, Sitka Banner, Georgia"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
        var widths = new double[text.Length];
        var total = 0.0;
        for (var i = 0; i < text.Length; i++)
        {
            var formatted = new FormattedText(text[i].ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 15, Brushes.White, 1);
            widths[i] = formatted.WidthIncludingTrailingWhitespace;
            total += widths[i];
        }

        var circumference = 2 * Math.PI * radius;
        var spacing = Math.Max(0, (circumference - total) / text.Length);
        var angle = -90.0;
        for (var i = 0; i < text.Length; i++)
        {
            var arc = widths[i] + spacing;
            var middle = angle + (arc / 2 / circumference * 360);
            var radians = middle * Math.PI / 180;
            var letter = new TextBlock
            {
                Text = text[i].ToString(),
                FontFamily = typeface.FontFamily,
                FontWeight = FontWeights.SemiBold,
                FontSize = 15,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new RotateTransform(middle + 90),
                Width = 20,
                Height = 20,
                TextAlignment = TextAlignment.Center,
            };
            Canvas.SetLeft(letter, Center + (Math.Cos(radians) * radius) - 10);
            Canvas.SetTop(letter, Center + (Math.Sin(radians) * radius) - 10);
            Letters.Add(letter);
            InscriptionRing.Children.Add(letter);
            angle += arc / circumference * 360;
        }

        Recolor();
    }

    private void OnThemeChanged(object? sender, EventArgs e) => Recolor();

    /// <summary>Applies the current accent metal to every part.</summary>
    public void Recolor()
    {
        var accent = TryFindResource("Color.Accent") is Color a ? a : Color.FromRgb(0xE3, 0xB2, 0x61);
        var bright = TryFindResource("Color.AccentBright") is Color b ? b : Color.FromRgb(0xF8, 0xD9, 0x8F);
        var deep = TryFindResource("Color.AccentDeep") is Color d ? d : Color.FromRgb(0xB0, 0x80, 0x36);
        var danger = TryFindResource("Color.Danger") is Color e ? e : Color.FromRgb(0xE8, 0x6A, 0x50);

        var metal = new LinearGradientBrush
        {
            StartPoint = new Point(0.1, 0),
            EndPoint = new Point(0.9, 1),
            GradientStops =
            {
                new GradientStop(ThemeManager.Lighten(bright, 0.35), 0),
                new GradientStop(bright, 0.3),
                new GradientStop(accent, 0.62),
                new GradientStop(deep, 1),
            },
        };
        metal.Freeze();
        var faint = new SolidColorBrush(Color.FromArgb(0xB4, accent.R, accent.G, accent.B));
        faint.Freeze();

        OuterRing.Stroke = metal;
        MidRing.Stroke = faint;
        InnerRingA.Stroke = metal;
        InnerRingB.Stroke = faint;
        MedallionRim.Stroke = new SolidColorBrush(deep);
        foreach (var tick in Ticks)
        {
            tick.Stroke = metal;
        }

        foreach (var letter in Letters)
        {
            letter.Foreground = metal;
        }

        var ray = new SolidColorBrush(Color.FromArgb(0x42, accent.R, accent.G, accent.B));
        ray.Freeze();
        foreach (var path in Rays)
        {
            path.Fill = ray;
        }

        var medallion = new RadialGradientBrush
        {
            GradientOrigin = new Point(0.34, 0.28),
            Center = new Point(0.45, 0.4),
            RadiusX = 0.7,
            RadiusY = 0.7,
            GradientStops =
            {
                new GradientStop(ThemeManager.Lighten(bright, 0.45), 0),
                new GradientStop(bright, 0.3),
                new GradientStop(accent, 0.68),
                new GradientStop(deep, 1),
            },
        };
        medallion.Freeze();
        Medallion.Fill = medallion;

        var glow = new RadialGradientBrush
        {
            GradientStops =
            {
                new GradientStop(Color.FromArgb(0x55, accent.R, accent.G, accent.B), 0.35),
                new GradientStop(Color.FromArgb(0x18, accent.R, accent.G, accent.B), 0.7),
                new GradientStop(Color.FromArgb(0, accent.R, accent.G, accent.B), 1),
            },
        };
        glow.Freeze();
        Glow.Fill = glow;
        Shock.Stroke = new SolidColorBrush(bright);

        var ember = new RadialGradientBrush
        {
            GradientStops =
            {
                new GradientStop(Color.FromArgb(0x00, danger.R, danger.G, danger.B), 0.2),
                new GradientStop(Color.FromArgb(0x70, danger.R, danger.G, danger.B), 0.85),
                new GradientStop(Color.FromArgb(0x00, danger.R, danger.G, danger.B), 1),
            },
        };
        ember.Freeze();
        EmberTint.Fill = ember;
    }

    // ------------------------------------------------------------- idle motion

    private void UpdateIdle()
    {
        if (IsIdleAnimated && IsVisible && IsLoaded && !Motion.IsOff)
        {
            StartIdle();
        }
        else
        {
            StopIdle();
        }
    }

    public void StartIdle()
    {
        if (_idleRunning || _busy)
        {
            return;
        }

        _idleRunning = true;
        Spin(_tickRotation, 360, 140);
        Spin(_inscriptionRotation, -360, 220);
        Spin(_rayRotation, 360, 300);
        var breathe = new DoubleAnimation(0.55, 1, TimeSpan.FromSeconds(3.2)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = Motion.EaseInOut };
        Timeline.SetDesiredFrameRate(breathe, 30);
        Glow.BeginAnimation(OpacityProperty, breathe);
        ScheduleSheen();
    }

    public void StopIdle()
    {
        _idleRunning = false;
        _tickRotation.BeginAnimation(RotateTransform.AngleProperty, null);
        _inscriptionRotation.BeginAnimation(RotateTransform.AngleProperty, null);
        _rayRotation.BeginAnimation(RotateTransform.AngleProperty, null);
        Glow.BeginAnimation(OpacityProperty, null);
    }

    private static void Spin(RotateTransform transform, double degrees, double seconds)
    {
        var from = transform.Angle % 360;
        var animation = new DoubleAnimation(from, from + degrees, TimeSpan.FromSeconds(seconds)) { RepeatBehavior = RepeatBehavior.Forever };
        Timeline.SetDesiredFrameRate(animation, 30);
        transform.BeginAnimation(RotateTransform.AngleProperty, animation);
    }

    private void ScheduleSheen()
    {
        if (!_idleRunning)
        {
            return;
        }

        PlaySheen(delaySeconds: 2.5, then: () =>
        {
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(7.5) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                ScheduleSheen();
            };
            timer.Start();
        });
    }

    /// <summary>A metallic light sweep across the seal.</summary>
    public void PlaySheen(double delaySeconds = 0, Action? then = null, double seconds = 1.4)
    {
        if (Motion.IsOff)
        {
            then?.Invoke();
            return;
        }

        var sweep = new DoubleAnimation(-330, 330, TimeSpan.FromSeconds(seconds))
        {
            BeginTime = TimeSpan.FromSeconds(delaySeconds),
            EasingFunction = Motion.EaseInOut,
        };
        if (then is not null)
        {
            sweep.Completed += (_, _) => then();
        }

        _sheenTranslate.BeginAnimation(TranslateTransform.XProperty, sweep);
    }

    // ------------------------------------------------------------ state moves

    /// <summary>While the key is being derived the bezel spins up like a vault dial.</summary>
    public void SetBusy(bool busy)
    {
        if (_busy == busy)
        {
            return;
        }

        _busy = busy;
        if (busy)
        {
            StopIdle();
            if (Motion.IsOff)
            {
                return;
            }

            var spin = new DoubleAnimation(_tickRotation.Angle % 360, (_tickRotation.Angle % 360) + 360, TimeSpan.FromSeconds(0.9)) { RepeatBehavior = RepeatBehavior.Forever };
            _tickRotation.BeginAnimation(RotateTransform.AngleProperty, spin);
            var counter = new DoubleAnimation(_inscriptionRotation.Angle % 360, (_inscriptionRotation.Angle % 360) - 360, TimeSpan.FromSeconds(1.6)) { RepeatBehavior = RepeatBehavior.Forever };
            _inscriptionRotation.BeginAnimation(RotateTransform.AngleProperty, counter);
            var pulse = new DoubleAnimation(0.6, 1, TimeSpan.FromSeconds(0.45)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever };
            Glow.BeginAnimation(OpacityProperty, pulse);
        }
        else
        {
            StopIdle();
            UpdateIdle();
        }
    }

    /// <summary>Rings snap into alignment, the key turns, the seal flares.</summary>
    public void PlayUnlock()
    {
        _busy = false;
        StopIdle();
        if (Motion.IsOff)
        {
            return;
        }

        var tick = _tickRotation.Angle;
        _tickRotation.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(tick, Math.Ceiling(tick / 60) * 60 + 60, TimeSpan.FromMilliseconds(620)) { EasingFunction = Motion.Spring });
        var inscription = _inscriptionRotation.Angle;
        _inscriptionRotation.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(inscription, Math.Floor(inscription / 90) * 90 - 90, TimeSpan.FromMilliseconds(700)) { EasingFunction = Motion.Spring });
        _keyholeRotation.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, 90, TimeSpan.FromMilliseconds(520)) { BeginTime = TimeSpan.FromMilliseconds(160), EasingFunction = Motion.BackOut });
        var flash = new DoubleAnimationUsingKeyFrames { BeginTime = TimeSpan.FromMilliseconds(380) };
        flash.KeyFrames.Add(new EasingDoubleKeyFrame(0.95, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(120))));
        flash.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(700)), Motion.EaseOut));
        Flash.BeginAnimation(OpacityProperty, flash);
        PlayShock(380);
        Glow.BeginAnimation(OpacityProperty, new DoubleAnimation(1.4, 1, TimeSpan.FromMilliseconds(900)) { BeginTime = TimeSpan.FromMilliseconds(380) });
    }

    /// <summary>Returns the keyhole to rest (used when the seal is shown again).</summary>
    public void ResetKey()
    {
        _keyholeRotation.BeginAnimation(RotateTransform.AngleProperty, null);
        _keyholeRotation.Angle = 0;
        Flash.BeginAnimation(OpacityProperty, null);
        Flash.Opacity = 0;
    }

    public void PlayError()
    {
        SetBusy(false);
        if (Motion.IsOff)
        {
            return;
        }

        var tint = new DoubleAnimationUsingKeyFrames();
        tint.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(90))));
        tint.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(900)), Motion.EaseOut));
        EmberTint.BeginAnimation(OpacityProperty, tint);
        var jitter = new DoubleAnimationUsingKeyFrames();
        var start = _tickRotation.Angle;
        double[] offsets = [0, -9, 7, -5, 3, 0];
        for (var i = 0; i < offsets.Length; i++)
        {
            jitter.KeyFrames.Add(new EasingDoubleKeyFrame(start + offsets[i], KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(i * 70))));
        }

        jitter.Completed += (_, _) => UpdateIdle();
        _tickRotation.BeginAnimation(RotateTransform.AngleProperty, jitter);
    }

    public void PlayShock(double delayMs = 0, double toScale = 3.2)
    {
        if (Motion.IsOff)
        {
            return;
        }

        var scale = (ScaleTransform)Shock.RenderTransform;
        var grow = new DoubleAnimation(0.9, toScale, TimeSpan.FromMilliseconds(900)) { BeginTime = TimeSpan.FromMilliseconds(delayMs), EasingFunction = Motion.ExpoOut };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        var fade = new DoubleAnimationUsingKeyFrames { BeginTime = TimeSpan.FromMilliseconds(delayMs) };
        fade.KeyFrames.Add(new EasingDoubleKeyFrame(0.85, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(40))));
        fade.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(900)), Motion.EaseOut));
        Shock.BeginAnimation(OpacityProperty, fade);
    }

    /// <summary>A "stamp" impact: the seal presses in and springs back (lock, success).</summary>
    public void PlayStamp()
    {
        if (Motion.IsOff)
        {
            return;
        }

        RenderTransformOrigin = new Point(0.5, 0.5);
        var scale = RenderTransform as ScaleTransform;
        if (scale is null || scale.IsFrozen)
        {
            scale = new ScaleTransform(1, 1);
            RenderTransform = scale;
        }

        var stamp = new DoubleAnimationUsingKeyFrames();
        stamp.KeyFrames.Add(new EasingDoubleKeyFrame(1.18, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        stamp.KeyFrames.Add(new EasingDoubleKeyFrame(0.94, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(230)), Motion.EaseIn));
        stamp.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(760)), Motion.Spring));
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, stamp);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, stamp);
        PlayShock(200, 2.6);
    }

    /// <summary>Hides every part (the intro then reveals them one by one).</summary>
    public void PrepareForAssembly()
    {
        StopIdle();
        foreach (var element in new UIElement[] { Glow, MidRing, InnerRingA, InnerRingB, Medallion, MedallionRim, Keyhole })
        {
            element.Opacity = 0;
        }

        foreach (var tick in Ticks)
        {
            tick.Opacity = 0;
        }

        foreach (var letter in Letters)
        {
            letter.Opacity = 0;
        }

        foreach (var ray in Rays)
        {
            ray.Opacity = 0;
        }

        var circumference = 2 * Math.PI * 176 / OuterRing.StrokeThickness;
        OuterRing.StrokeDashArray = [circumference, circumference];
        OuterRing.StrokeDashOffset = circumference;
        RayRing.RenderTransformOrigin = new Point(0.5, 0.5);
    }

    /// <summary>Restores every part to its fully assembled state.</summary>
    public void CompleteAssembly()
    {
        foreach (var element in new UIElement[] { Glow, MidRing, InnerRingA, InnerRingB, Medallion, MedallionRim, Keyhole })
        {
            element.BeginAnimation(OpacityProperty, null);
            element.Opacity = 1;
        }

        foreach (var tick in Ticks)
        {
            tick.BeginAnimation(OpacityProperty, null);
            tick.Opacity = 1;
        }

        foreach (var letter in Letters)
        {
            letter.BeginAnimation(OpacityProperty, null);
            letter.Opacity = 1;
        }

        foreach (var ray in Rays)
        {
            ray.BeginAnimation(OpacityProperty, null);
            ray.Opacity = 1;
        }

        OuterRing.BeginAnimation(Shape.StrokeDashOffsetProperty, null);
        OuterRing.StrokeDashArray = null;
        OuterRing.StrokeDashOffset = 0;
        UpdateIdle();
    }

    /// <summary>A crisp bitmap of the emblem (used by the split-open transition).</summary>
    public ImageSource Snapshot(double size)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(
            (int)(size * dpi.DpiScaleX), (int)(size * dpi.DpiScaleY), dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new VisualBrush(_stage) { Stretch = Stretch.Uniform }, null, new Rect(0, 0, size, size));
        }

        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }
}
