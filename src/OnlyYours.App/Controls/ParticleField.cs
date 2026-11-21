using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using OnlyYours.App.Infrastructure;

namespace OnlyYours.App.Controls;

public enum ParticleMode
{
    /// <summary>Slow drift with gentle parallax — ambient dust.</summary>
    Drift,

    /// <summary>Particles spiral in towards the centre (intro).</summary>
    Vortex,

    /// <summary>Embers rising from the bottom, flickering.</summary>
    Embers,
}

/// <summary>
/// Lightweight particle system rendered with retained drawing primitives and driven by
/// <see cref="CompositionTarget.Rendering"/> only while visible. Supports radial impulses
/// (<see cref="Burst"/>) and "warp" stretching for cinematic moments.
/// </summary>
public sealed class ParticleField : FrameworkElement
{
    public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(
        nameof(Mode), typeof(ParticleMode), typeof(ParticleField), new PropertyMetadata(ParticleMode.Drift, (d, _) => ((ParticleField)d).Seed()));

    public static readonly DependencyProperty CountProperty = DependencyProperty.Register(
        nameof(Count), typeof(int), typeof(ParticleField), new PropertyMetadata(140, (d, _) => ((ParticleField)d).Seed()));

    public static readonly DependencyProperty IsRunningProperty = DependencyProperty.Register(
        nameof(IsRunning), typeof(bool), typeof(ParticleField), new PropertyMetadata(true, (d, _) => ((ParticleField)d).UpdateSubscription()));

    public static readonly DependencyProperty ColorProperty = DependencyProperty.Register(
        nameof(Color), typeof(Color), typeof(ParticleField), new PropertyMetadata(Color.FromRgb(0xF2, 0xC8, 0x76), (d, _) => ((ParticleField)d)._sprites = null));

    public static readonly DependencyProperty IntensityProperty = DependencyProperty.Register(
        nameof(Intensity), typeof(double), typeof(ParticleField), new PropertyMetadata(1.0));

    public static readonly DependencyProperty WarpProperty = DependencyProperty.Register(
        nameof(Warp), typeof(double), typeof(ParticleField), new PropertyMetadata(0.0));

    private readonly Random _random = new(7);
    private readonly Stopwatch _clock = new();
    private Particle[] _particles = [];
    private Brush[]? _sprites;
    private TimeSpan _last;
    private bool _subscribed;

    private struct Particle
    {
        public double X;
        public double Y;
        public double Z;
        public double Vx;
        public double Vy;
        public double Size;
        public double Phase;
        public double Life;
        public double Angle;
        public double Radius;
    }

    public ParticleField()
    {
        IsHitTestVisible = false;
        Loaded += (_, _) => UpdateSubscription();
        Unloaded += (_, _) => Unsubscribe();
        IsVisibleChanged += (_, _) => UpdateSubscription();
    }

    public ParticleMode Mode
    {
        get => (ParticleMode)GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public int Count
    {
        get => (int)GetValue(CountProperty);
        set => SetValue(CountProperty, value);
    }

    public bool IsRunning
    {
        get => (bool)GetValue(IsRunningProperty);
        set => SetValue(IsRunningProperty, value);
    }

    public Color Color
    {
        get => (Color)GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    /// <summary>Global opacity multiplier (animatable).</summary>
    public double Intensity
    {
        get => (double)GetValue(IntensityProperty);
        set => SetValue(IntensityProperty, value);
    }

    /// <summary>0 = normal, 1 = particles stream outwards like a jump to light speed (animatable).</summary>
    public double Warp
    {
        get => (double)GetValue(WarpProperty);
        set => SetValue(WarpProperty, value);
    }

    /// <summary>Pushes every particle away from the centre.</summary>
    public void Burst(double strength = 900)
    {
        var cx = ActualWidth / 2;
        var cy = ActualHeight / 2;
        for (var i = 0; i < _particles.Length; i++)
        {
            ref var p = ref _particles[i];
            var dx = p.X - cx;
            var dy = p.Y - cy;
            var distance = Math.Max(20, Math.Sqrt((dx * dx) + (dy * dy)));
            var force = strength * (0.5 + p.Z) / Math.Sqrt(distance / 60);
            p.Vx += dx / distance * force;
            p.Vy += dy / distance * force;
        }
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        if (_particles.Length == 0 || sizeInfo.PreviousSize.Width < 1)
        {
            Seed();
        }
    }

    private void Seed()
    {
        var width = Math.Max(ActualWidth, 1);
        var height = Math.Max(ActualHeight, 1);
        var count = Motion.IsOff ? 0 : Motion.IsReduced ? Count / 3 : Count;
        _particles = new Particle[count];
        for (var i = 0; i < count; i++)
        {
            _particles[i] = Spawn(width, height, initial: true);
        }

        InvalidateVisual();
    }

    private Particle Spawn(double width, double height, bool initial)
    {
        var p = new Particle
        {
            Z = _random.NextDouble(),
            Phase = _random.NextDouble() * Math.PI * 2,
            Life = initial ? _random.NextDouble() : 0,
        };
        p.Size = 0.6 + (p.Z * 2.4);
        switch (Mode)
        {
            case ParticleMode.Vortex:
                p.Angle = _random.NextDouble() * Math.PI * 2;
                p.Radius = Math.Max(width, height) * (0.35 + (_random.NextDouble() * 0.6));
                p.X = (width / 2) + (Math.Cos(p.Angle) * p.Radius);
                p.Y = (height / 2) + (Math.Sin(p.Angle) * p.Radius);
                break;
            case ParticleMode.Embers:
                p.X = _random.NextDouble() * width;
                p.Y = initial ? _random.NextDouble() * height : height + 10;
                p.Vy = -(12 + (p.Z * 34));
                p.Vx = (_random.NextDouble() - 0.5) * 8;
                break;
            default:
                p.X = _random.NextDouble() * width;
                p.Y = _random.NextDouble() * height;
                p.Vx = (_random.NextDouble() - 0.5) * (4 + (p.Z * 10));
                p.Vy = (_random.NextDouble() - 0.5) * (4 + (p.Z * 10));
                break;
        }

        return p;
    }

    private void UpdateSubscription()
    {
        if (IsRunning && IsVisible && IsLoaded && !Motion.IsOff)
        {
            if (!_subscribed)
            {
                _subscribed = true;
                _clock.Restart();
                _last = TimeSpan.Zero;
                CompositionTarget.Rendering += OnFrame;
            }
        }
        else
        {
            Unsubscribe();
        }
    }

    private void Unsubscribe()
    {
        if (_subscribed)
        {
            _subscribed = false;
            CompositionTarget.Rendering -= OnFrame;
        }
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        var now = _clock.Elapsed;
        var dt = Math.Clamp((now - _last).TotalSeconds, 0, 0.05);
        _last = now;
        Step(dt, now.TotalSeconds);
        InvalidateVisual();
    }

    private void Step(double dt, double time)
    {
        var width = ActualWidth;
        var height = ActualHeight;
        if (width < 1 || height < 1)
        {
            return;
        }

        var cx = width / 2;
        var cy = height / 2;
        var warp = Warp;
        for (var i = 0; i < _particles.Length; i++)
        {
            ref var p = ref _particles[i];
            p.Life = Math.Min(1, p.Life + (dt * 0.6));
            switch (Mode)
            {
                case ParticleMode.Vortex:
                    p.Angle += dt * (0.25 + ((1 - p.Z) * 0.35)) * (1 + (220 / Math.Max(p.Radius, 30)));
                    p.Radius = Math.Max(0, p.Radius - (dt * (40 + (p.Z * 90))));
                    if (p.Radius < 8)
                    {
                        p = Spawn(width, height, initial: false);
                    }

                    p.X = cx + (Math.Cos(p.Angle) * p.Radius) + (p.Vx * dt);
                    p.Y = cy + (Math.Sin(p.Angle) * p.Radius * 0.82) + (p.Vy * dt);
                    break;
                case ParticleMode.Embers:
                    p.X += (p.Vx + (Math.Sin(time + p.Phase) * 6)) * dt;
                    p.Y += p.Vy * dt;
                    if (p.Y < -10)
                    {
                        p = Spawn(width, height, initial: false);
                    }

                    break;
                default:
                    p.X += p.Vx * dt;
                    p.Y += p.Vy * dt;
                    break;
            }

            // Impulse decay (bursts) and warp acceleration.
            p.Vx *= Math.Pow(0.12, dt);
            p.Vy *= Math.Pow(0.12, dt);
            if (Mode == ParticleMode.Drift)
            {
                p.Vx += (_random.NextDouble() - 0.5) * 6 * dt;
                p.Vy += (_random.NextDouble() - 0.5) * 6 * dt;
            }

            if (warp > 0.01)
            {
                var dx = p.X - cx;
                var dy = p.Y - cy;
                p.X += dx * warp * dt * 2.5;
                p.Y += dy * warp * dt * 2.5;
            }

            if (p.X < -40 || p.X > width + 40 || p.Y < -40 || p.Y > height + 40)
            {
                p = Spawn(width, height, initial: false);
                if (Mode == ParticleMode.Drift)
                {
                    p.X = _random.NextDouble() * width;
                    p.Y = _random.NextDouble() * height;
                }
            }
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (_particles.Length == 0)
        {
            return;
        }

        _sprites ??= BuildSprites(Color);
        var intensity = Math.Clamp(Intensity, 0, 1);
        if (intensity <= 0.001)
        {
            return;
        }

        var warp = Warp;
        var cx = ActualWidth / 2;
        var cy = ActualHeight / 2;
        var time = _clock.Elapsed.TotalSeconds;
        for (var i = 0; i < _particles.Length; i++)
        {
            ref readonly var p = ref _particles[i];
            var flicker = Mode == ParticleMode.Embers ? 0.55 + (0.45 * Math.Sin((time * 3) + p.Phase)) : 0.75 + (0.25 * Math.Sin((time * 1.3) + p.Phase));
            var alpha = intensity * p.Life * flicker * (0.25 + (p.Z * 0.75));
            if (alpha < 0.02)
            {
                continue;
            }

            var sprite = _sprites[Math.Min(_sprites.Length - 1, (int)(alpha * _sprites.Length))];
            var size = p.Size * 3;
            if (warp > 0.05)
            {
                var dx = p.X - cx;
                var dy = p.Y - cy;
                var length = Math.Sqrt((dx * dx) + (dy * dy)) + 1;
                var stretch = warp * 26 * (0.4 + p.Z);
                var pen = new Pen(sprite, p.Size * 0.9) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
                dc.DrawLine(pen, new Point(p.X, p.Y), new Point(p.X - (dx / length * stretch), p.Y - (dy / length * stretch)));
            }
            else
            {
                dc.DrawEllipse(sprite, null, new Point(p.X, p.Y), size, size);
            }
        }
    }

    private static Brush[] BuildSprites(Color color)
    {
        const int levels = 12;
        var sprites = new Brush[levels];
        for (var i = 0; i < levels; i++)
        {
            var alpha = (byte)(255 * (i + 1) / levels);
            var brush = new RadialGradientBrush
            {
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(alpha, 0xFF, 0xF6, 0xDE), 0),
                    new GradientStop(Color.FromArgb((byte)(alpha * 0.7), color.R, color.G, color.B), 0.25),
                    new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1),
                },
            };
            brush.Freeze();
            sprites[i] = brush;
        }

        return sprites;
    }
}
