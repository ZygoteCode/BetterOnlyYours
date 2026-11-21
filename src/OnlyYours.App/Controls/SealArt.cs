using System.Windows;
using System.Windows.Media;

namespace OnlyYours.App.Controls;

/// <summary>
/// Static artwork of the OnlyYours mark — a minted gold seal with an obsidian keyhole —
/// used for the application icon, the tray icon and small static renderings. The
/// animated emblem (<see cref="SealEmblem"/>) follows the same geometry.
/// </summary>
public static class SealArt
{
    public static readonly Color Obsidian = Color.FromRgb(0x0E, 0x0C, 0x0A);
    public static readonly Color GoldLight = Color.FromRgb(0xFF, 0xF1, 0xC9);
    public static readonly Color GoldMid = Color.FromRgb(0xF0, 0xC8, 0x73);
    public static readonly Color Gold = Color.FromRgb(0xD8, 0xA2, 0x4C);
    public static readonly Color GoldDeep = Color.FromRgb(0x8E, 0x60, 0x24);

    public static Brush GoldBrush(double angle = 45)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1),
            GradientStops =
            {
                new GradientStop(GoldLight, 0.0),
                new GradientStop(GoldMid, 0.32),
                new GradientStop(Gold, 0.62),
                new GradientStop(GoldDeep, 1.0),
            },
        };
        if (Math.Abs(angle - 45) > 0.1)
        {
            brush.RelativeTransform = new RotateTransform(angle - 45, 0.5, 0.5);
        }

        brush.Freeze();
        return brush;
    }

    /// <summary>The keyhole, centred on (cx, cy) for a seal of radius r.</summary>
    public static Geometry Keyhole(double cx, double cy, double r)
    {
        var headRadius = r * 0.2;
        var headCenterY = cy - (r * 0.14);
        var slotTopHalf = r * 0.085;
        var slotBottomHalf = r * 0.15;
        var slotBottom = cy + (r * 0.36);

        var head = new EllipseGeometry(new Point(cx, headCenterY), headRadius, headRadius);
        var slot = new StreamGeometry();
        using (var context = slot.Open())
        {
            context.BeginFigure(new Point(cx - slotTopHalf, headCenterY + (headRadius * 0.5)), true, true);
            context.LineTo(new Point(cx + slotTopHalf, headCenterY + (headRadius * 0.5)), true, false);
            context.LineTo(new Point(cx + slotBottomHalf, slotBottom), true, false);
            context.QuadraticBezierTo(new Point(cx, slotBottom + (r * 0.04)), new Point(cx - slotBottomHalf, slotBottom), true, false);
        }

        var combined = new CombinedGeometry(GeometryCombineMode.Union, head, slot);
        var flattened = combined.GetFlattenedPathGeometry(0.05, ToleranceType.Absolute);
        flattened.Freeze();
        return flattened;
    }

    /// <summary>Draws the mark into a square of side <paramref name="size"/>.</summary>
    public static void DrawMark(DrawingContext dc, double size, bool detailed)
    {
        var c = size / 2;
        var r = size * 0.47;
        var center = new Point(c, c);

        // Soft drop shadow to lift the coin from any background.
        if (detailed)
        {
            var shadow = new RadialGradientBrush(Color.FromArgb(0x70, 0, 0, 0), Color.FromArgb(0, 0, 0, 0)) { GradientOrigin = new Point(0.5, 0.56), Center = new Point(0.5, 0.56) };
            shadow.Freeze();
            dc.DrawEllipse(shadow, null, new Point(c, c + (r * 0.05)), r * 1.06, r * 1.06);
        }

        dc.DrawEllipse(GoldBrush(), null, center, r, r);

        // Rim: a darker bevel and, when large enough, a milled edge of ticks.
        var rimPen = new Pen(new SolidColorBrush(Color.FromArgb(0xB0, GoldDeep.R, GoldDeep.G, GoldDeep.B)), Math.Max(0.8, size * 0.012));
        rimPen.Freeze();
        dc.DrawEllipse(null, rimPen, center, r * 0.86, r * 0.86);

        if (size >= 40)
        {
            var tickPen = new Pen(new SolidColorBrush(Color.FromArgb(0xC0, GoldDeep.R, GoldDeep.G, GoldDeep.B)), Math.Max(0.6, size * 0.008)) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            tickPen.Freeze();
            var count = size >= 128 ? 72 : 36;
            for (var i = 0; i < count; i++)
            {
                var angle = i * (2 * Math.PI / count);
                var major = i % (count / 12) == 0;
                var inner = r * (major ? 0.885 : 0.905);
                var outer = r * 0.965;
                dc.DrawLine(tickPen, new Point(c + (Math.Cos(angle) * inner), c + (Math.Sin(angle) * inner)), new Point(c + (Math.Cos(angle) * outer), c + (Math.Sin(angle) * outer)));
            }
        }

        // Inner medallion with a sunburst.
        var medallion = new RadialGradientBrush
        {
            GradientOrigin = new Point(0.35, 0.3),
            GradientStops =
            {
                new GradientStop(Color.FromRgb(0xFC, 0xE3, 0xA6), 0),
                new GradientStop(Color.FromRgb(0xE2, 0xB0, 0x5C), 0.55),
                new GradientStop(Color.FromRgb(0xA8, 0x74, 0x30), 1),
            },
        };
        medallion.Freeze();
        dc.DrawEllipse(medallion, null, center, r * 0.74, r * 0.74);

        if (detailed && size >= 64)
        {
            var rayBrush = new SolidColorBrush(Color.FromArgb(0x38, 0x6E, 0x48, 0x14));
            rayBrush.Freeze();
            const int rays = 24;
            for (var i = 0; i < rays; i++)
            {
                var angle = i * (2 * Math.PI / rays);
                var half = Math.PI / rays * 0.45;
                var inner = r * 0.36;
                var outer = r * (i % 2 == 0 ? 0.7 : 0.6);
                var ray = new StreamGeometry();
                using (var context = ray.Open())
                {
                    context.BeginFigure(new Point(c + (Math.Cos(angle - half) * inner), c + (Math.Sin(angle - half) * inner)), true, true);
                    context.LineTo(new Point(c + (Math.Cos(angle) * outer), c + (Math.Sin(angle) * outer)), true, false);
                    context.LineTo(new Point(c + (Math.Cos(angle + half) * inner), c + (Math.Sin(angle + half) * inner)), true, false);
                }

                ray.Freeze();
                dc.DrawGeometry(rayBrush, null, ray);
            }
        }

        var innerRing = new Pen(new SolidColorBrush(Color.FromArgb(0x90, 0x7A, 0x52, 0x1C)), Math.Max(0.6, size * 0.008));
        innerRing.Freeze();
        dc.DrawEllipse(null, innerRing, center, r * 0.74, r * 0.74);

        // The keyhole, cut in obsidian with a faint inner light.
        var keyhole = Keyhole(c, c, r);
        var hole = new LinearGradientBrush(Color.FromRgb(0x05, 0x04, 0x03), Color.FromRgb(0x24, 0x1C, 0x12), 90);
        hole.Freeze();
        dc.DrawGeometry(hole, null, keyhole);

        // Specular highlight.
        var sheen = new RadialGradientBrush(Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF), Color.FromArgb(0, 0xFF, 0xFF, 0xFF)) { Center = new Point(0.3, 0.22), GradientOrigin = new Point(0.3, 0.22), RadiusX = 0.55, RadiusY = 0.4 };
        sheen.Freeze();
        dc.DrawEllipse(sheen, null, center, r, r);
    }
}
