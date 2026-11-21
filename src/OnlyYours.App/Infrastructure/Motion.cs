using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace OnlyYours.App.Infrastructure;

public enum MotionLevel
{
    Cinematic,
    Reduced,
    Off,
}

/// <summary>
/// Global motion policy. Every animation in the app asks <see cref="Scale"/> for its
/// duration, so "reduced" shortens everything and "off" makes every transition
/// instantaneous (and respects the Windows "show animations" accessibility switch).
/// </summary>
public static class Motion
{
    public static MotionLevel Level { get; set; } = MotionLevel.Cinematic;

    public static bool IsOff => Level == MotionLevel.Off || !SystemParameters.ClientAreaAnimation;

    public static bool IsReduced => Level != MotionLevel.Cinematic || IsOff;

    public static MotionLevel Parse(string value) => value switch
    {
        "reduced" => MotionLevel.Reduced,
        "off" => MotionLevel.Off,
        _ => MotionLevel.Cinematic,
    };

    public static Duration Scale(double milliseconds) => IsOff
        ? new Duration(TimeSpan.Zero)
        : new Duration(TimeSpan.FromMilliseconds(Level == MotionLevel.Reduced ? milliseconds * 0.45 : milliseconds));

    public static TimeSpan Time(double milliseconds) => Scale(milliseconds).TimeSpan;

    public static readonly IEasingFunction EaseOut = Freeze(new CubicEase { EasingMode = EasingMode.EaseOut });
    public static readonly IEasingFunction EaseIn = Freeze(new CubicEase { EasingMode = EasingMode.EaseIn });
    public static readonly IEasingFunction EaseInOut = Freeze(new CubicEase { EasingMode = EasingMode.EaseInOut });
    public static readonly IEasingFunction ExpoOut = Freeze(new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 6 });
    public static readonly IEasingFunction QuintOut = Freeze(new QuinticEase { EasingMode = EasingMode.EaseOut });
    public static readonly IEasingFunction BackOut = Freeze(new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.35 });
    public static readonly IEasingFunction Spring = Freeze(new SpringEase { Damping = 0.62, Frequency = 2.2 });
    public static readonly IEasingFunction SoftSpring = Freeze(new SpringEase { Damping = 0.8, Frequency = 1.6 });

    private static IEasingFunction Freeze(EasingFunctionBase easing)
    {
        easing.Freeze();
        return easing;
    }

    /// <summary>Animates a dependency property, honouring the motion policy.</summary>
    public static AnimationClock? Animate(
        IAnimatable target,
        DependencyProperty property,
        double to,
        double milliseconds,
        IEasingFunction? easing = null,
        double? from = null,
        double delayMs = 0,
        Action? completed = null)
    {
        var animation = new DoubleAnimation
        {
            To = to,
            Duration = Scale(milliseconds),
            EasingFunction = easing ?? EaseOut,
            BeginTime = IsOff ? TimeSpan.Zero : TimeSpan.FromMilliseconds(Level == MotionLevel.Reduced ? delayMs * 0.45 : delayMs),
            FillBehavior = FillBehavior.HoldEnd,
        };
        if (from is { } start)
        {
            animation.From = start;
        }

        if (completed is not null)
        {
            animation.Completed += (_, _) => completed();
        }

        var clock = animation.CreateClock();
        target.ApplyAnimationClock(property, clock);
        return clock;
    }

    public static void Stop(IAnimatable target, DependencyProperty property) => target.ApplyAnimationClock(property, null);

    /// <summary>Ensures <paramref name="element"/> has a TranslateTransform + ScaleTransform pair to animate.</summary>
    public static (TranslateTransform Translate, ScaleTransform Scale) Transforms(UIElement element)
    {
        if (element.RenderTransform is TransformGroup group && group.Children.Count == 2 && group.Children[0] is ScaleTransform s && group.Children[1] is TranslateTransform t && !group.IsFrozen)
        {
            return (t, s);
        }

        var scale = new ScaleTransform(1, 1);
        var translate = new TranslateTransform();
        element.RenderTransform = new TransformGroup { Children = { scale, translate } };
        element.RenderTransformOrigin = new Point(0.5, 0.5);
        return (translate, scale);
    }

    /// <summary>Fades and lifts an element into view.</summary>
    public static void Rise(UIElement element, double delayMs = 0, double distance = 14, double durationMs = 420)
    {
        if (IsOff)
        {
            element.Opacity = 1;
            return;
        }

        var (translate, _) = Transforms(element);
        element.Opacity = 0;
        Animate(element, UIElement.OpacityProperty, 1, durationMs, QuintOut, 0, delayMs);
        Animate(translate, TranslateTransform.YProperty, 0, durationMs + 120, QuintOut, distance, delayMs);
    }

    /// <summary>Damped horizontal shake (wrong password).</summary>
    public static void Shake(UIElement element)
    {
        if (IsOff)
        {
            return;
        }

        var (translate, _) = Transforms(element);
        var animation = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(520) };
        double[] offsets = [0, -14, 12, -9, 7, -4, 2, 0];
        for (var i = 0; i < offsets.Length; i++)
        {
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(offsets[i], KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(i * 520.0 / (offsets.Length - 1))), EaseOut));
        }

        translate.BeginAnimation(TranslateTransform.XProperty, animation);
    }

    /// <summary>A brief scale "pop" (copy confirmations, toggles).</summary>
    public static void Pop(UIElement element, double peak = 1.12)
    {
        if (IsOff)
        {
            return;
        }

        var (_, scale) = Transforms(element);
        var animation = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(380) };
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(peak, KeyTime.FromPercent(0.35), EaseOut));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromPercent(1), Spring));
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
    }
}

/// <summary>Damped harmonic oscillator easing: overshoots and settles like a physical spring.</summary>
public sealed class SpringEase : EasingFunctionBase
{
    public double Damping { get; set; } = 0.6;

    public double Frequency { get; set; } = 2;

    protected override double EaseInCore(double normalizedTime)
    {
        // Expressed as an ease-in; EasingMode defaults to EaseOut, which mirrors it into the spring curve.
        var t = 1 - normalizedTime;
        var omega = Frequency * 2 * Math.PI;
        var k = Damping * 6;
        double Spring(double x) => 1 - (Math.Exp(-k * x) * Math.Cos(omega * x));

        // Blend the tiny residual oscillation away so the curve lands exactly on 1.
        var value = Spring(t) + ((1 - Spring(1)) * Math.Pow(t, 4));
        return 1 - value;
    }

    protected override Freezable CreateInstanceCore() => new SpringEase { Damping = Damping, Frequency = Frequency };
}
