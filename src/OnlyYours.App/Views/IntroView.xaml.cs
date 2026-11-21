using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using OnlyYours.App.Controls;
using OnlyYours.App.Infrastructure;

namespace OnlyYours.App.Views;

/// <summary>
/// The opening title sequence — a keyframed, multi-layer composition:
/// <code>
///  0.00  black, letterbox, film grain fades in
///  0.15  dust spirals in towards the centre (vortex)
///  0.40  a spark ignites, 0.65 an anamorphic flare streaks across
///  0.90  the outer ring of the seal is engraved by light
///  1.20  inner rings · 1.45 the milled bezel ticks in · 1.85 the inscription
///  2.10  the sunburst unfolds · 2.40 the medallion and its keyhole form
///  2.85  IMPACT — shockwave, flash, camera shake, particle burst
///  3.00  a metallic sheen sweeps the seal · 3.10 the camera dollies back
///  3.30  the wordmark is engraved letter by letter, then gilded
///  4.05  tagline · 4.55 the letterbox opens · 4.70 particles warp away
///  4.75  match cut: the seal glides onto the lock screen's emblem
/// </code>
/// Skippable at any time with a key or a click.
/// </summary>
public partial class IntroView : UserControl
{
    private const double SealSize = 300;

    /// <summary>Default easing marker for keyframes.</summary>
    private const IEasingFunction? D = null;
    private readonly List<(Path Outline, Path Fill, TranslateTransform Shift, double Length)> _letters = [];
    private Storyboard? _story;
    private readonly List<(double Time, Action Action)> _cues = [];
    private int _nextCue;
    private Func<FrameworkElement?>? _findTarget;
    private Action? _completed;
    private FrameworkElement? _target;
    private bool _finished;
    private bool _matchCutStarted;

    public IntroView()
    {
        InitializeComponent();
        Grain.Fill = AmbientBackdrop.Grain();
        MouseDown += (_, _) => Skip();
        KeyDown += (_, _) => Skip();
    }

    public void Play(string mode, Func<FrameworkElement?> findTarget, Action completed)
    {
        _findTarget = findTarget;
        _completed = completed;
        Focus();
        Keyboard.Focus(this);
        if (!IsLoaded || ActualWidth < 1)
        {
            Loaded += (_, _) => Dispatcher.BeginInvoke(() => Start(mode), System.Windows.Threading.DispatcherPriority.Loaded);
        }
        else
        {
            Start(mode);
        }
    }

    /// <summary>True once the storyboard has been built and started.</summary>
    internal bool IsPlaying => _story is not null && !_finished;

    /// <summary>True once the hand-over to the real screen has begun.</summary>
    internal bool MatchCutStarted => _matchCutStarted;

    /// <summary>Holds the sequence still at <paramref name="time"/> — the visual tests film it frame by frame.</summary>
    internal void FreezeAt(TimeSpan time)
    {
        if (_story is null)
        {
            return;
        }

        _story.Pause(this);
        _story.SeekAlignedToLastTick(this, time, TimeSeekOrigin.BeginTime);
    }

    private void Start(string mode)
    {
        Seal.PrepareForAssembly();
        BuildWordmark();
        Wordmark.Margin = new Thickness(0, 410, 0, 0);
        Tagline.Margin = new Thickness(0, 524, 0, 0);
        LetterTop.Height = LetterBottom.Height = ActualHeight * 0.11;

        _target = _findTarget?.Invoke();
        if (_target is not null)
        {
            _target.Opacity = 0;
        }

        _cues.Clear();
        _nextCue = 0;
        _story = mode == "short" ? BuildShort() : BuildCinematic();
        _cues.Sort((a, b) => a.Time.CompareTo(b.Time));
        _story.Completed += (_, _) => Finish();
        _story.Begin(this, true);
        CompositionTarget.Rendering += OnFrame;
        Services.StartupTrace.Mark("intro-started");
    }

    // ------------------------------------------------------------- builders

    private Storyboard BuildCinematic()
    {
        var s = new Storyboard();
        var w = ActualWidth;
        var center = new Point(w / 2, ActualHeight / 2);

        Fade(s, Grain, 0, 0.05, 0.1, 0.5);
        Key(s, Dust, ParticleField.IntensityProperty, (0.15, 0, D), (1.2, 0.95, D));
        Fade(s, SkipHint, 0, 0.55, 1.2, 0.6);

        // Spark ignition and pulse.
        Key(s, SparkScale, ScaleTransform.ScaleXProperty, (0.4, 0, D), (0.7, 1, Motion.ExpoOut), (0.82, 1.35, D), (1.0, 0.9, Motion.EaseInOut), (2.4, 0.6, D));
        Key(s, SparkScale, ScaleTransform.ScaleYProperty, (0.4, 0, D), (0.7, 1, Motion.ExpoOut), (0.82, 1.35, D), (1.0, 0.9, Motion.EaseInOut), (2.4, 0.6, D));
        Key(s, Spark, OpacityProperty, (0.4, 0, D), (0.6, 1, D), (2.3, 0.85, D), (2.9, 0, D));

        // Anamorphic flare.
        Key(s, FlareScale, ScaleTransform.ScaleXProperty, (0.65, 0, D), (1.05, 1, Motion.ExpoOut));
        Key(s, FlareScale, ScaleTransform.ScaleYProperty, (1.05, 1, D), (1.5, 0.25, Motion.EaseIn));
        Key(s, Flare, OpacityProperty, (0.65, 0, D), (0.8, 0.95, D), (1.5, 0, Motion.EaseIn));

        // Outer ring engraved by light.
        var circumference = 2 * Math.PI * 176 / Seal.OuterRing.StrokeThickness;
        Key(s, Seal.OuterRing, Shape.StrokeDashOffsetProperty, (0.9, circumference, D), (1.8, 0, Motion.EaseInOut));
        Fade(s, Seal.Glow, 0, 0.7, 0.9, 1.2);
        Fade(s, Seal.MidRing, 0, 1, 1.2, 0.5);
        Fade(s, Seal.InnerRingA, 0, 1, 1.3, 0.5);
        Fade(s, Seal.InnerRingB, 0, 1, 1.4, 0.5);

        for (var i = 0; i < Seal.Ticks.Count; i++)
        {
            Fade(s, Seal.Ticks[i], 0, 1, 1.45 + (i * 0.008), 0.14);
        }

        for (var i = 0; i < Seal.Letters.Count; i++)
        {
            Fade(s, Seal.Letters[i], 0, 1, 1.85 + (i * 0.012), 0.2);
        }

        var rayScale = new ScaleTransform(0.3, 0.3, SealEmblem.Center, SealEmblem.Center);
        Seal.RayRing.RenderTransform = new TransformGroup { Children = { rayScale, Seal.RayRing.RenderTransform } };
        Key(s, rayScale, ScaleTransform.ScaleXProperty, (2.1, 0.3, D), (2.7, 1, Motion.BackOut));
        Key(s, rayScale, ScaleTransform.ScaleYProperty, (2.1, 0.3, D), (2.7, 1, Motion.BackOut));
        for (var i = 0; i < Seal.Rays.Count; i++)
        {
            Fade(s, Seal.Rays[i], 0, 1, 2.1 + (i * 0.012), 0.25);
        }

        // Medallion and keyhole form, then the impact.
        Fade(s, Seal.Medallion, 0, 1, 2.4, 0.4);
        Fade(s, Seal.MedallionRim, 0, 1, 2.5, 0.35);
        Fade(s, Seal.Keyhole, 0, 1, 2.6, 0.25);
        Key(s, SealPunch, ScaleTransform.ScaleXProperty, (2.6, 1, D), (2.85, 1.07, Motion.EaseIn), (3.35, 1, Motion.Spring));
        Key(s, SealPunch, ScaleTransform.ScaleYProperty, (2.6, 1, D), (2.85, 1.07, Motion.EaseIn), (3.35, 1, Motion.Spring));
        Key(s, Flash, OpacityProperty, (2.85, 0, D), (2.9, 0.95, D), (3.6, 0, Motion.EaseOut));
        Key(s, Shake, TranslateTransform.XProperty, (2.85, 0, D), (2.9, 8, D), (2.97, -7, D), (3.04, 5, D), (3.12, -3, D), (3.2, 2, D), (3.3, 0, D));
        Key(s, Shake, TranslateTransform.YProperty, (2.85, 0, D), (2.92, -6, D), (3.0, 5, D), (3.08, -3, D), (3.18, 1, D), (3.3, 0, D));
        Cue(2.85, () =>
        {
            Seal.PlayShock(0, 3.6);
            Dust.Burst(1300);
        });
        Cue(3.0, () => Seal.PlaySheen(seconds: 1.0));

        // Camera dolly: scale about the centre while drifting up.
        const double dolly = 0.86;
        Key(s, CameraScale, ScaleTransform.ScaleXProperty, (3.1, 1, D), (4.2, dolly, Motion.ExpoOut));
        Key(s, CameraScale, ScaleTransform.ScaleYProperty, (3.1, 1, D), (4.2, dolly, Motion.ExpoOut));
        Key(s, CameraTranslate, TranslateTransform.XProperty, (3.1, 0, D), (4.2, (1 - dolly) * center.X, Motion.ExpoOut));
        Key(s, CameraTranslate, TranslateTransform.YProperty, (3.1, 0, D), (4.2, ((1 - dolly) * center.Y) - 70, Motion.ExpoOut));

        // Wordmark: engraved outline, converging letters, then gilded.
        for (var i = 0; i < _letters.Count; i++)
        {
            var (outline, fill, shift, length) = _letters[i];
            var start = 3.3 + (i * 0.07);
            Key(s, outline, Shape.StrokeDashOffsetProperty, (start, length, D), (start + 0.65, 0, Motion.EaseInOut));
            Fade(s, outline, 0, 1, start, 0.1);
            Fade(s, fill, 0, 1, start + 0.4, 0.45);
            Key(s, outline, OpacityProperty, (start + 0.75, 1, D), (start + 1.1, 0.35, D));
            Key(s, shift, TranslateTransform.XProperty, (start, (i - ((_letters.Count - 1) / 2.0)) * 22, D), (start + 0.9, 0, Motion.ExpoOut));
        }

        Fade(s, Tagline, 0, 1, 4.05, 0.5);
        Key(s, TaglineShift, TranslateTransform.YProperty, (4.05, 14, D), (4.6, 0, Motion.ExpoOut));

        // The letterbox opens, particles warp away, the match cut begins.
        Key(s, LetterTop, HeightProperty, (4.55, LetterTop.Height, D), (5.15, 0, Motion.EaseInOut));
        Key(s, LetterBottom, HeightProperty, (4.55, LetterBottom.Height, D), (5.15, 0, Motion.EaseInOut));
        Key(s, Dust, ParticleField.WarpProperty, (4.7, 0, D), (5.2, 1.3, Motion.EaseIn));
        Key(s, Dust, ParticleField.IntensityProperty, (4.7, 0.95, D), (5.35, 0, D));
        Fade(s, SkipHint, 0.55, 0, 4.6, 0.3);
        Cue(4.75, () => MatchCut(0.62));
        Key(s, Backdrop, OpacityProperty, (4.8, 1, D), (5.4, 0, Motion.EaseInOut));
        Key(s, Vignette, OpacityProperty, (4.8, 1, D), (5.4, 0, Motion.EaseInOut));
        Key(s, Grain, OpacityProperty, (4.8, 0.05, D), (5.4, 0, D));
        Key(s, Root, OpacityProperty, (5.45, 1, D), (5.5, 1, D));
        return s;
    }

    private Storyboard BuildShort()
    {
        var s = new Storyboard();
        Seal.CompleteAssembly();
        Seal.StopIdle();
        LetterTop.Height = LetterBottom.Height = 0;
        Key(s, Seal, OpacityProperty, (0, 0, D), (0.35, 1, Motion.EaseOut));
        Key(s, SealPunch, ScaleTransform.ScaleXProperty, (0, 1.25, D), (0.42, 1, Motion.ExpoOut));
        Key(s, SealPunch, ScaleTransform.ScaleYProperty, (0, 1.25, D), (0.42, 1, Motion.ExpoOut));
        Key(s, Flash, OpacityProperty, (0.3, 0, D), (0.36, 0.6, D), (0.8, 0, D));
        Cue(0.34, () => Seal.PlayShock(0, 3));
        Cue(0.45, () => Seal.PlaySheen(seconds: 0.7));
        Cue(0.7, () => MatchCut(0.5));
        Key(s, Backdrop, OpacityProperty, (0.7, 1, D), (1.2, 0, Motion.EaseInOut));
        Key(s, Vignette, OpacityProperty, (0.7, 1, D), (1.2, 0, Motion.EaseInOut));
        Key(s, Root, OpacityProperty, (1.25, 1, D), (1.3, 1, D));
        return s;
    }

    /// <summary>Glides the intro seal (by moving the whole camera) onto the lock screen's emblem.</summary>
    private void MatchCut(double seconds)
    {
        if (_matchCutStarted)
        {
            return;
        }

        _matchCutStarted = true;
        Motion.Animate(Wordmark, OpacityProperty, 0, seconds * 600, Motion.EaseIn);
        Motion.Animate(Tagline, OpacityProperty, 0, seconds * 600, Motion.EaseIn);

        _target ??= _findTarget?.Invoke();
        if (_target is null || !_target.IsVisible || _target.ActualWidth < 1)
        {
            Motion.Animate(Camera, OpacityProperty, 0, seconds * 1000, Motion.EaseIn);
            return;
        }

        var local = Seal.TransformToAncestor(Camera).TransformBounds(new Rect(Seal.RenderSize));
        var target = _target.TransformToVisual(Root).TransformBounds(new Rect(_target.RenderSize));
        var side = Math.Min(target.Width, target.Height);
        target = new Rect(target.X + ((target.Width - side) / 2), target.Y + ((target.Height - side) / 2), side, side);
        var scale = target.Width / local.Width;
        var tx = target.X - (scale * local.X);
        var ty = target.Y - (scale * local.Y);

        var ms = seconds * 1000;
        Motion.Animate(CameraScale, ScaleTransform.ScaleXProperty, scale, ms, Motion.EaseInOut);
        Motion.Animate(CameraScale, ScaleTransform.ScaleYProperty, scale, ms, Motion.EaseInOut);
        Motion.Animate(CameraTranslate, TranslateTransform.XProperty, tx, ms, Motion.EaseInOut);
        Motion.Animate(CameraTranslate, TranslateTransform.YProperty, ty, ms, Motion.EaseInOut);
    }

    private void BuildWordmark()
    {
        Wordmark.Children.Clear();
        _letters.Clear();
        const string word = "ONLYYOURS";
        const double size = 62;
        const double tracking = 20;
        var typeface = new Typeface(new FontFamily("Palatino Linotype, Sitka Banner, Georgia"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var glyphs = word.Select(ch => new FormattedText(ch.ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, size, Brushes.White, dpi)).ToList();
        var total = glyphs.Sum(g => g.WidthIncludingTrailingWhitespace) + (tracking * (word.Length - 1));
        var x = -total / 2;
        var gold = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1),
            GradientStops =
            {
                new GradientStop(Color.FromRgb(0xFF, 0xF1, 0xC9), 0),
                new GradientStop(Color.FromRgb(0xF0, 0xC8, 0x73), 0.45),
                new GradientStop(Color.FromRgb(0xB0, 0x80, 0x36), 1),
            },
        };
        gold.Freeze();
        var stroke = new SolidColorBrush(Color.FromRgb(0xF8, 0xD9, 0x8F));
        stroke.Freeze();

        foreach (var glyph in glyphs)
        {
            var geometry = glyph.BuildGeometry(new Point(x, -glyph.Baseline + (size * 0.36)));
            geometry.Freeze();
            var length = PathLength(geometry) / 1.1;
            var shift = new TranslateTransform();
            var outline = new Path { Data = geometry, Stroke = stroke, StrokeThickness = 1.1, StrokeDashArray = [length, length], StrokeDashOffset = length, Opacity = 0, RenderTransform = shift };
            var fill = new Path { Data = geometry, Fill = gold, Opacity = 0, RenderTransform = shift };
            Wordmark.Children.Add(fill);
            Wordmark.Children.Add(outline);
            _letters.Add((outline, fill, shift, length));
            x += glyph.WidthIncludingTrailingWhitespace + tracking;
        }
    }

    private static double PathLength(Geometry geometry)
    {
        var flat = geometry.GetFlattenedPathGeometry(0.25, ToleranceType.Absolute);
        var length = 0.0;
        foreach (var figure in flat.Figures)
        {
            var previous = figure.StartPoint;
            foreach (var segment in figure.Segments)
            {
                if (segment is PolyLineSegment poly)
                {
                    foreach (var point in poly.Points)
                    {
                        length += (point - previous).Length;
                        previous = point;
                    }
                }
                else if (segment is LineSegment line)
                {
                    length += (line.Point - previous).Length;
                    previous = line.Point;
                }
            }

            if (figure.IsClosed)
            {
                length += (figure.StartPoint - previous).Length;
            }
        }

        return Math.Max(length, 1);
    }

    // ------------------------------------------------------------ timeline

    private static void Key(Storyboard story, DependencyObject target, DependencyProperty property, params (double Time, double Value, IEasingFunction? Easing)[] frames)
    {
        var animation = new DoubleAnimationUsingKeyFrames();
        foreach (var (time, value, easing) in frames)
        {
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(value, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(time)), easing ?? Motion.EaseOut));
        }

        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, new PropertyPath(property));
        story.Children.Add(animation);
    }

    private static void Fade(Storyboard story, DependencyObject target, double from, double to, double start, double duration) =>
        Key(story, target, OpacityProperty, (start, from, (IEasingFunction?)null), (start + duration, to, Motion.EaseOut));

    /// <summary>
    /// Schedules <paramref name="action"/> at <paramref name="time"/> on the sequence's own clock.
    /// Cues are polled every rendered frame against the storyboard time: Completed events of
    /// child timelines are not raised reliably once WPF freezes the timeline tree.
    /// </summary>
    private void Cue(double time, Action action) => _cues.Add((time, action));

    private void OnFrame(object? sender, EventArgs e)
    {
        if (_story is null || _finished)
        {
            CompositionTarget.Rendering -= OnFrame;
            return;
        }

        var now = _story.GetCurrentTime(this)?.TotalSeconds ?? 0;
        while (_nextCue < _cues.Count && _cues[_nextCue].Time <= now)
        {
            _cues[_nextCue++].Action();
        }
    }

    // -------------------------------------------------------------- endings

    public void Skip()
    {
        if (_finished)
        {
            return;
        }

        CompositionTarget.Rendering -= OnFrame;
        _story?.Stop(this);
        _story = null;
        Seal.CompleteAssembly();
        Seal.StopIdle();
        Motion.Animate(this, OpacityProperty, 0, 280, Motion.EaseOut, completed: Finish);
    }

    private void Finish()
    {
        if (_finished)
        {
            return;
        }

        _finished = true;
        CompositionTarget.Rendering -= OnFrame;
        if (_target is not null)
        {
            _target.Opacity = 1;
        }

        Dust.IsRunning = false;
        _completed?.Invoke();
        Services.StartupTrace.Mark("intro-finished");
    }
}
