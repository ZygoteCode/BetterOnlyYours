using System.Windows;
using System.Windows.Media;

namespace OnlyYours.App.Controls;

/// <summary>
/// A unique seal drawn from an identity fingerprint — the visual counterpart of the hex
/// fingerprint. Ring count, ray count, the inner polygon, its rotation and the metal hue
/// all derive from the fingerprint bytes, so two identities are easy to tell apart at a
/// glance while comparing them out of band.
/// </summary>
public sealed class SealIdenticon : FrameworkElement
{
    public static readonly DependencyProperty FingerprintProperty = DependencyProperty.Register(
        nameof(Fingerprint), typeof(object), typeof(SealIdenticon), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Color[] Metals =
    [
        Color.FromRgb(0xE3, 0xB2, 0x61), Color.FromRgb(0xDE, 0x8E, 0x5E), Color.FromRgb(0xCB, 0xD0, 0xD8), Color.FromRgb(0x5F, 0xC2, 0xA0),
        Color.FromRgb(0x83, 0xA9, 0xD1), Color.FromRgb(0xB2, 0x92, 0xCC), Color.FromRgb(0xCC, 0x86, 0x95), Color.FromRgb(0xA9, 0xBE, 0x75),
    ];

    static SealIdenticon()
    {
        WidthProperty.OverrideMetadata(typeof(SealIdenticon), new FrameworkPropertyMetadata(96.0));
        HeightProperty.OverrideMetadata(typeof(SealIdenticon), new FrameworkPropertyMetadata(96.0));
    }

    public object? Fingerprint
    {
        get => GetValue(FingerprintProperty);
        set => SetValue(FingerprintProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var bytes = Fingerprint as byte[];
        var size = Math.Min(ActualWidth, ActualHeight);
        if (bytes is not { Length: >= 8 } || size <= 0)
        {
            return;
        }

        var c = new Point(ActualWidth / 2, ActualHeight / 2);
        var r = size / 2 - 2;
        var metal = Metals[bytes[0] % Metals.Length];
        var second = Metals[bytes[1] % Metals.Length];
        var brush = new SolidColorBrush(metal);
        var faint = new SolidColorBrush(Color.FromArgb(0x50, metal.R, metal.G, metal.B));
        var fill = new RadialGradientBrush(Color.FromArgb(0x40, second.R, second.G, second.B), Color.FromArgb(0x08, metal.R, metal.G, metal.B));

        dc.DrawEllipse(fill, new Pen(brush, size * 0.025), c, r, r);

        // Milled edge: tick count and pattern from byte 2.
        var ticks = 24 + (bytes[2] % 4 * 12);
        var tickPen = new Pen(faint, size * 0.012);
        for (var i = 0; i < ticks; i++)
        {
            if (((bytes[3 + (i % 5)] >> (i % 8)) & 1) == 0)
            {
                continue;
            }

            var a = i * Math.PI * 2 / ticks;
            dc.DrawLine(tickPen, Polar(c, r * 0.86, a), Polar(c, r * 0.96, a));
        }

        // Inner rings.
        var rings = 1 + (bytes[4] % 3);
        for (var i = 0; i < rings; i++)
        {
            var radius = r * (0.78 - (i * 0.07));
            dc.DrawEllipse(null, new Pen(faint, size * 0.01), c, radius, radius);
        }

        // Rays.
        var rays = 6 + (bytes[5] % 4 * 3);
        var rotation = bytes[6] / 255.0 * Math.PI * 2;
        for (var i = 0; i < rays; i++)
        {
            var a = rotation + (i * Math.PI * 2 / rays);
            var length = 0.42 + ((bytes[(7 + i) % bytes.Length] % 5) * 0.04);
            dc.DrawLine(new Pen(brush, size * 0.018) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, Polar(c, r * 0.2, a), Polar(c, r * length, a));
        }

        // Central polygon.
        var sides = 3 + (bytes[7] % 6);
        var polygon = new StreamGeometry();
        using (var context = polygon.Open())
        {
            for (var i = 0; i <= sides; i++)
            {
                var a = rotation + (i * Math.PI * 2 / sides);
                var point = Polar(c, r * 0.2, a);
                if (i == 0)
                {
                    context.BeginFigure(point, true, true);
                }
                else
                {
                    context.LineTo(point, true, true);
                }
            }
        }

        dc.DrawGeometry(new SolidColorBrush(second), new Pen(brush, size * 0.012), polygon);
    }

    private static Point Polar(Point center, double radius, double angle) =>
        new(center.X + (Math.Cos(angle) * radius), center.Y + (Math.Sin(angle) * radius));
}
