using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using OnlyYours.App.Infrastructure;

namespace OnlyYours.App.Controls;

public enum StageTransition
{
    None,
    Fade,
    Rise,
    Slide,

    /// <summary>Unlock: the old screen splits in two halves that swing apart, the vault zooms in from behind.</summary>
    SealOpen,

    /// <summary>Lock: the vault blurs away and sinks, the lock screen settles in.</summary>
    SealClose,
}

/// <summary>Content host with cinematic transitions between screens.</summary>
public sealed class TransitionHost : ContentControl
{
    public static readonly DependencyProperty TransitionProperty = DependencyProperty.Register(
        nameof(Transition), typeof(StageTransition), typeof(TransitionHost), new PropertyMetadata(StageTransition.Fade));

    private readonly Grid _root = new() { ClipToBounds = true };
    private ContentPresenter? _current;

    public TransitionHost()
    {
        var template = new ControlTemplate(typeof(TransitionHost));
        template.VisualTree = new FrameworkElementFactory(typeof(ContentPresenter), "PART_Placeholder");
        Template = template;
        Focusable = false;
        IsTabStop = false;
    }

    public StageTransition Transition
    {
        get => (StageTransition)GetValue(TransitionProperty);
        set => SetValue(TransitionProperty, value);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (GetTemplateChild("PART_Placeholder") is ContentPresenter placeholder)
        {
            placeholder.Content = _root;
        }
    }

    protected override void OnContentChanged(object oldContent, object newContent)
    {
        // The root grid is our real visual; base handling would reparent content into the placeholder.
        var previous = _current;
        var next = new ContentPresenter { Content = newContent, Opacity = 1 };
        _current = next;
        _root.Children.Add(next);
        if (previous is null)
        {
            return;
        }

        var transition = Motion.IsOff ? StageTransition.None : Transition;
        switch (transition)
        {
            case StageTransition.None:
                _root.Children.Remove(previous);
                break;
            case StageTransition.SealOpen:
                SealOpen(previous, next);
                break;
            case StageTransition.SealClose:
                SealClose(previous, next);
                break;
            case StageTransition.Slide:
                CrossFade(previous, next, 24, 0, 260);
                break;
            case StageTransition.Rise:
                CrossFade(previous, next, 0, 22, 420);
                break;
            default:
                CrossFade(previous, next, 0, 0, 320);
                break;
        }
    }

    private void CrossFade(ContentPresenter previous, ContentPresenter next, double dx, double dy, double milliseconds)
    {
        previous.IsHitTestVisible = false;
        var (oldTranslate, _) = Motion.Transforms(previous);
        Motion.Animate(previous, OpacityProperty, 0, milliseconds * 0.6, Motion.EaseIn, completed: () => _root.Children.Remove(previous));
        Motion.Animate(oldTranslate, TranslateTransform.XProperty, -dx, milliseconds * 0.6, Motion.EaseIn);
        Motion.Animate(oldTranslate, TranslateTransform.YProperty, -dy * 0.4, milliseconds * 0.6, Motion.EaseIn);

        var (translate, scale) = Motion.Transforms(next);
        Motion.Animate(next, OpacityProperty, 1, milliseconds, Motion.QuintOut, 0, milliseconds * 0.25);
        Motion.Animate(translate, TranslateTransform.XProperty, 0, milliseconds + 120, Motion.QuintOut, dx, milliseconds * 0.25);
        Motion.Animate(translate, TranslateTransform.YProperty, 0, milliseconds + 120, Motion.QuintOut, dy, milliseconds * 0.25);
        if (dx == 0 && dy == 0)
        {
            Motion.Animate(scale, ScaleTransform.ScaleXProperty, 1, milliseconds + 120, Motion.QuintOut, 0.985, milliseconds * 0.25);
            Motion.Animate(scale, ScaleTransform.ScaleYProperty, 1, milliseconds + 120, Motion.QuintOut, 0.985, milliseconds * 0.25);
        }
    }

    private void SealOpen(ContentPresenter previous, ContentPresenter next)
    {
        var width = _root.ActualWidth;
        var height = _root.ActualHeight;
        if (width < 10 || height < 10)
        {
            _root.Children.Remove(previous);
            return;
        }

        var snapshot = Capture(previous, width, height);
        _root.Children.Remove(previous);

        // New content: zooms in from behind, unblurring.
        var (_, scale) = Motion.Transforms(next);
        next.Opacity = 0;
        var blur = new BlurEffect { Radius = 18, RenderingBias = RenderingBias.Performance };
        next.Effect = blur;
        Motion.Animate(next, OpacityProperty, 1, 520, Motion.EaseOut, 0, 140);
        Motion.Animate(scale, ScaleTransform.ScaleXProperty, 1, 1100, Motion.ExpoOut, 1.08, 120);
        Motion.Animate(scale, ScaleTransform.ScaleYProperty, 1, 1100, Motion.ExpoOut, 1.08, 120);
        Motion.Animate(blur, BlurEffect.RadiusProperty, 0, 900, Motion.EaseOut, 18, 160, () => next.Effect = null);

        if (snapshot is null)
        {
            return;
        }

        // Old content: two halves swing open like a vault door.
        var halves = new Grid { IsHitTestVisible = false };
        for (var side = 0; side < 2; side++)
        {
            var image = new Image
            {
                Source = snapshot,
                Stretch = Stretch.Fill,
                Width = width,
                Height = height,
                Clip = new RectangleGeometry(new Rect(side == 0 ? 0 : width / 2, 0, width / 2, height)),
                RenderTransformOrigin = new Point(side == 0 ? 0 : 1, 0.5),
            };
            var translate = new TranslateTransform();
            var rotate = new RotateTransform();
            var skew = new SkewTransform();
            image.RenderTransform = new TransformGroup { Children = { skew, rotate, translate } };
            halves.Children.Add(image);

            var direction = side == 0 ? -1 : 1;
            Motion.Animate(translate, TranslateTransform.XProperty, direction * width * 0.62, 950, Motion.ExpoOut, 0, 60);
            Motion.Animate(rotate, RotateTransform.AngleProperty, direction * 5, 950, Motion.EaseOut, 0, 60);
            Motion.Animate(skew, SkewTransform.AngleYProperty, direction * -6, 950, Motion.EaseOut, 0, 60);
            Motion.Animate(image, OpacityProperty, 0, 700, Motion.EaseIn, 1, 260);
        }

        var seam = new Rectangle
        {
            Width = 6,
            HorizontalAlignment = HorizontalAlignment.Center,
            Fill = new LinearGradientBrush(Color.FromArgb(0, 0xFF, 0xF3, 0xD2), Color.FromArgb(0xFF, 0xFF, 0xF3, 0xD2), 90) { GradientStops = { new GradientStop(Color.FromArgb(0, 0xFF, 0xF3, 0xD2), 0), new GradientStop(Color.FromArgb(0xE0, 0xFF, 0xF3, 0xD2), 0.5), new GradientStop(Color.FromArgb(0, 0xFF, 0xF3, 0xD2), 1) } },
            Opacity = 0,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new ScaleTransform(1, 1),
        };
        halves.Children.Add(seam);
        var seamFlash = new DoubleAnimationUsingKeyFrames();
        seamFlash.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(90))));
        seamFlash.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(650)), Motion.EaseOut));
        seam.BeginAnimation(OpacityProperty, seamFlash);
        Motion.Animate((ScaleTransform)seam.RenderTransform, ScaleTransform.ScaleXProperty, 60, 650, Motion.ExpoOut, 1);

        _root.Children.Add(halves);
        var cleanup = new System.Windows.Threading.DispatcherTimer { Interval = Motion.Time(1150) + TimeSpan.FromMilliseconds(50) };
        cleanup.Tick += (_, _) =>
        {
            cleanup.Stop();
            _root.Children.Remove(halves);
        };
        cleanup.Start();
    }

    private void SealClose(ContentPresenter previous, ContentPresenter next)
    {
        previous.IsHitTestVisible = false;
        var blur = new BlurEffect { Radius = 0, RenderingBias = RenderingBias.Performance };
        previous.Effect = blur;
        var (_, oldScale) = Motion.Transforms(previous);
        Motion.Animate(blur, BlurEffect.RadiusProperty, 16, 520, Motion.EaseIn);
        Motion.Animate(oldScale, ScaleTransform.ScaleXProperty, 0.94, 600, Motion.EaseIn);
        Motion.Animate(oldScale, ScaleTransform.ScaleYProperty, 0.94, 600, Motion.EaseIn);
        Motion.Animate(previous, OpacityProperty, 0, 560, Motion.EaseIn, completed: () => _root.Children.Remove(previous));

        var (_, scale) = Motion.Transforms(next);
        next.Opacity = 0;
        Motion.Animate(next, OpacityProperty, 1, 520, Motion.EaseOut, 0, 260);
        Motion.Animate(scale, ScaleTransform.ScaleXProperty, 1, 900, Motion.ExpoOut, 1.04, 260);
        Motion.Animate(scale, ScaleTransform.ScaleYProperty, 1, 900, Motion.ExpoOut, 1.04, 260);
    }

    private static BitmapSource? Capture(FrameworkElement element, double width, double height)
    {
        try
        {
            var dpi = VisualTreeHelper.GetDpi(element);
            var bitmap = new RenderTargetBitmap(
                Math.Max(1, (int)(width * dpi.DpiScaleX)),
                Math.Max(1, (int)(height * dpi.DpiScaleY)),
                dpi.PixelsPerInchX,
                dpi.PixelsPerInchY,
                PixelFormats.Pbgra32);
            bitmap.Render(element);
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>Attached entrance animations for XAML: <c>c:Anim.Enter="Rise" c:Anim.Delay="120"</c>.</summary>
public static class Anim
{
    public static readonly DependencyProperty EnterProperty = DependencyProperty.RegisterAttached(
        "Enter", typeof(string), typeof(Anim), new PropertyMetadata(null, OnEnterChanged));

    public static readonly DependencyProperty DelayProperty = DependencyProperty.RegisterAttached(
        "Delay", typeof(double), typeof(Anim), new PropertyMetadata(0.0));

    public static readonly DependencyProperty StaggerProperty = DependencyProperty.RegisterAttached(
        "Stagger", typeof(double), typeof(Anim), new PropertyMetadata(0.0, OnStaggerChanged));

    public static string? GetEnter(DependencyObject d) => (string?)d.GetValue(EnterProperty);

    public static void SetEnter(DependencyObject d, string? value) => d.SetValue(EnterProperty, value);

    public static double GetDelay(DependencyObject d) => (double)d.GetValue(DelayProperty);

    public static void SetDelay(DependencyObject d, double value) => d.SetValue(DelayProperty, value);

    public static double GetStagger(DependencyObject d) => (double)d.GetValue(StaggerProperty);

    public static void SetStagger(DependencyObject d, double value) => d.SetValue(StaggerProperty, value);

    private static void OnEnterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element || e.NewValue is not string kind)
        {
            return;
        }

        if (!Motion.IsOff)
        {
            element.Opacity = 0;
        }

        element.Loaded += (_, _) => Play(element, kind, GetDelay(element));
    }

    public static void Play(FrameworkElement element, string kind, double delay)
    {
        if (Motion.IsOff)
        {
            element.Opacity = 1;
            return;
        }

        var (translate, scale) = Motion.Transforms(element);
        switch (kind)
        {
            case "Fade":
                Motion.Animate(element, UIElement.OpacityProperty, 1, 420, Motion.EaseOut, 0, delay);
                break;
            case "Pop":
                Motion.Animate(element, UIElement.OpacityProperty, 1, 260, Motion.EaseOut, 0, delay);
                Motion.Animate(scale, ScaleTransform.ScaleXProperty, 1, 620, Motion.Spring, 0.85, delay);
                Motion.Animate(scale, ScaleTransform.ScaleYProperty, 1, 620, Motion.Spring, 0.85, delay);
                break;
            case "Left":
                Motion.Animate(element, UIElement.OpacityProperty, 1, 380, Motion.EaseOut, 0, delay);
                Motion.Animate(translate, TranslateTransform.XProperty, 0, 560, Motion.QuintOut, -24, delay);
                break;
            case "Right":
                Motion.Animate(element, UIElement.OpacityProperty, 1, 380, Motion.EaseOut, 0, delay);
                Motion.Animate(translate, TranslateTransform.XProperty, 0, 560, Motion.QuintOut, 24, delay);
                break;
            default:
                Motion.Rise(element, delay);
                break;
        }
    }

    private static void OnStaggerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Panel panel)
        {
            return;
        }

        panel.Loaded += (_, _) =>
        {
            var step = GetStagger(panel);
            var baseDelay = GetDelay(panel);
            var index = 0;
            foreach (UIElement child in panel.Children)
            {
                if (child is FrameworkElement element && element.Visibility == Visibility.Visible)
                {
                    Play(element, "Rise", baseDelay + (Math.Min(index, 14) * step));
                    index++;
                }
            }
        };
    }
}
