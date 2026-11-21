using System.Windows;
using System.Windows.Media;

namespace OnlyYours.App.Infrastructure;

/// <summary>
/// The OnlyYours icon set: line icons on a 24×24 grid, drawn with round caps and
/// joins (stroke width is applied by the <c>Glyph</c> control, so they stay crisp
/// at every size). Registered as <c>Icon.*</c> geometry resources.
/// </summary>
public static class Icons
{
    private static readonly Dictionary<string, string> Paths = new()
    {
        ["Lock"] = "M6,11 H18 A2,2 0 0 1 20,13 V19 A2,2 0 0 1 18,21 H6 A2,2 0 0 1 4,19 V13 A2,2 0 0 1 6,11 Z M8,11 V7.5 A4,4 0 0 1 16,7.5 V11 M12,15 V17",
        ["Unlock"] = "M6,11 H18 A2,2 0 0 1 20,13 V19 A2,2 0 0 1 18,21 H6 A2,2 0 0 1 4,19 V13 A2,2 0 0 1 6,11 Z M8,11 V7.5 A4,4 0 0 1 15.6,5.8 M12,15 V17",
        ["Key"] = "M3,16.5 A4.5,4.5 0 1 0 12,16.5 A4.5,4.5 0 1 0 3,16.5 Z M10.7,13.3 L20.5,3.5 M17,7 L20,10 M14.5,9.5 L16.5,11.5",
        ["Shield"] = "M12,3 L19.5,6 V11.5 C19.5,16.2 16.3,19.6 12,21 C7.7,19.6 4.5,16.2 4.5,11.5 V6 Z",
        ["ShieldCheck"] = "M12,3 L19.5,6 V11.5 C19.5,16.2 16.3,19.6 12,21 C7.7,19.6 4.5,16.2 4.5,11.5 V6 Z M9,12 L11.2,14.2 L15.5,9.8",
        ["ShieldAlert"] = "M12,3 L19.5,6 V11.5 C19.5,16.2 16.3,19.6 12,21 C7.7,19.6 4.5,16.2 4.5,11.5 V6 Z M12,8.5 V12.5 M12,15.5 L12.01,15.5",
        ["Search"] = "M10.5,4 A6.5,6.5 0 1 0 10.5,17 A6.5,6.5 0 1 0 10.5,4 Z M15.4,15.4 L20,20",
        ["Plus"] = "M12,5 V19 M5,12 H19",
        ["Minus"] = "M5,12 H19",
        ["Close"] = "M6,6 L18,18 M18,6 L6,18",
        ["Check"] = "M5,12.5 L10,17.5 L19,7",
        ["ChevronRight"] = "M9,5 L16,12 L9,19",
        ["ChevronLeft"] = "M15,5 L8,12 L15,19",
        ["ChevronDown"] = "M5,9 L12,16 L19,9",
        ["ChevronUp"] = "M5,15 L12,8 L19,15",
        ["Star"] = "M12,3.5 L14.6,8.9 L20.5,9.6 L16.1,13.6 L17.3,19.5 L12,16.6 L6.7,19.5 L7.9,13.6 L3.5,9.6 L9.4,8.9 Z",
        ["Copy"] = "M9,9 H18 A2,2 0 0 1 20,11 V18 A2,2 0 0 1 18,20 H11 A2,2 0 0 1 9,18 Z M15,9 V6 A2,2 0 0 0 13,4 H6 A2,2 0 0 0 4,6 V13 A2,2 0 0 0 6,15 H9",
        ["Eye"] = "M2.5,12 C4.5,7.5 8,5 12,5 C16,5 19.5,7.5 21.5,12 C19.5,16.5 16,19 12,19 C8,19 4.5,16.5 2.5,12 Z M12,9 A3,3 0 1 0 12,15 A3,3 0 1 0 12,9 Z",
        ["EyeOff"] = "M9.9,5.2 C10.6,5.1 11.3,5 12,5 C16,5 19.5,7.5 21.5,12 C20.9,13.3 20.2,14.4 19.3,15.3 M6.3,6.6 C4.7,7.8 3.4,9.6 2.5,12 C4.5,16.5 8,19 12,19 C13.8,19 15.5,18.5 16.9,17.6 M9.9,9.9 A3,3 0 0 0 14.1,14.1 M3,3 L21,21",
        ["Trash"] = "M4,7 H20 M9,7 V4.5 A1,1 0 0 1 10,3.5 H14 A1,1 0 0 1 15,4.5 V7 M6,7 L7,19 A2,2 0 0 0 9,21 H15 A2,2 0 0 0 17,19 L18,7 M10,11 V17 M14,11 V17",
        ["Restore"] = "M3.5,12 A8.5,8.5 0 1 0 6,6 M3.5,3.5 V8 H8",
        ["Edit"] = "M15.5,4.5 L19.5,8.5 L8.5,19.5 H4.5 V15.5 Z M13.5,6.5 L17.5,10.5",
        ["Wand"] = "M4,20 L14,10 M12.5,8.5 L15.5,11.5 M17,3 V6 M15.5,4.5 H18.5 M20,8 V10 M19,9 H21 M7,4 V6 M6,5 H8",
        ["Globe"] = "M12,3 A9,9 0 1 0 12,21 A9,9 0 1 0 12,3 Z M3,12 H21 M12,3 C9.5,6 9.5,18 12,21 C14.5,18 14.5,6 12,3",
        ["User"] = "M12,4 A4,4 0 1 0 12,12 A4,4 0 1 0 12,4 Z M4.5,20.5 C5.5,16.8 8.4,15 12,15 C15.6,15 18.5,16.8 19.5,20.5",
        ["Users"] = "M9,4 A3.5,3.5 0 1 0 9,11 A3.5,3.5 0 1 0 9,4 Z M2.5,20 C3.2,16.6 5.7,15 9,15 C12.3,15 14.8,16.6 15.5,20 M16,4.5 A3.2,3.2 0 0 1 16,10.5 M18,15.3 C19.8,16 21,17.6 21.5,20",
        ["Mail"] = "M4,6 H20 A1,1 0 0 1 21,7 V17 A1,1 0 0 1 20,18 H4 A1,1 0 0 1 3,17 V7 A1,1 0 0 1 4,6 Z M3.5,7 L12,13 L20.5,7",
        ["Card"] = "M4,6 H20 A1.5,1.5 0 0 1 21.5,7.5 V16.5 A1.5,1.5 0 0 1 20,18 H4 A1.5,1.5 0 0 1 2.5,16.5 V7.5 A1.5,1.5 0 0 1 4,6 Z M2.5,10 H21.5 M6,14.5 H10",
        ["Note"] = "M6.5,3.5 H14 L19,8.5 V19 A1.5,1.5 0 0 1 17.5,20.5 H6.5 A1.5,1.5 0 0 1 5,19 V5 A1.5,1.5 0 0 1 6.5,3.5 Z M14,3.5 V8.5 H19 M8.5,12.5 H15.5 M8.5,16 H13",
        ["Identity"] = "M4,5 H20 A1.5,1.5 0 0 1 21.5,6.5 V17.5 A1.5,1.5 0 0 1 20,19 H4 A1.5,1.5 0 0 1 2.5,17.5 V6.5 A1.5,1.5 0 0 1 4,5 Z M8.5,9 A2,2 0 1 0 8.5,13 A2,2 0 1 0 8.5,9 Z M5.5,16 C6,14.6 7.1,14 8.5,14 C9.9,14 11,14.6 11.5,16 M14,10 H18.5 M14,13.5 H17",
        ["Wifi"] = "M2.5,9 C8,4 16,4 21.5,9 M5.5,12.5 C9.3,9.2 14.7,9.2 18.5,12.5 M8.7,16 C10.6,14.5 13.4,14.5 15.3,16 M12,19.5 L12.01,19.5",
        ["Terminal"] = "M4,5 H20 A1.5,1.5 0 0 1 21.5,6.5 V17.5 A1.5,1.5 0 0 1 20,19 H4 A1.5,1.5 0 0 1 2.5,17.5 V6.5 A1.5,1.5 0 0 1 4,5 Z M6.5,9.5 L9.5,12 L6.5,14.5 M11.5,15 H16",
        ["Code"] = "M8,7 L3,12 L8,17 M16,7 L21,12 L16,17 M13.5,5 L10.5,19",
        ["Bank"] = "M3,9.5 L12,4 L21,9.5 Z M5,9.5 V17 M9.5,9.5 V17 M14.5,9.5 V17 M19,9.5 V17 M3,20 H21 M3.5,17 H20.5",
        ["Tag"] = "M3.5,12.3 V4.5 A1,1 0 0 1 4.5,3.5 H12.3 L20.5,11.7 A1.4,1.4 0 0 1 20.5,13.7 L13.7,20.5 A1.4,1.4 0 0 1 11.7,20.5 Z M8,7 A1,1 0 1 0 8,9 A1,1 0 1 0 8,7 Z",
        ["Clock"] = "M12,3 A9,9 0 1 0 12,21 A9,9 0 1 0 12,3 Z M12,7 V12 L15.5,14",
        ["History"] = "M3.5,12 A8.5,8.5 0 1 0 6,6 M3.5,3.5 V8 H8 M12,7.5 V12 L15,14",
        ["Warning"] = "M12,3.5 L21.5,20 H2.5 Z M12,9.5 V14 M12,17 L12.01,17",
        ["Info"] = "M12,3 A9,9 0 1 0 12,21 A9,9 0 1 0 12,3 Z M12,11 V16.5 M12,7.5 L12.01,7.5",
        ["More"] = "M5,12 L5.01,12 M12,12 L12.01,12 M19,12 L19.01,12",
        ["MoreVertical"] = "M12,5 L12.01,5 M12,12 L12.01,12 M12,19 L12.01,19",
        ["Export"] = "M12,15 V3.5 M7.5,8 L12,3.5 L16.5,8 M4,14 V18.5 A1.5,1.5 0 0 0 5.5,20 H18.5 A1.5,1.5 0 0 0 20,18.5 V14",
        ["Import"] = "M12,3.5 V15 M7.5,10.5 L12,15 L16.5,10.5 M4,14 V18.5 A1.5,1.5 0 0 0 5.5,20 H18.5 A1.5,1.5 0 0 0 20,18.5 V14",
        ["Share"] = "M18,3 A2.5,2.5 0 1 0 18,8 A2.5,2.5 0 1 0 18,3 Z M6,9.5 A2.5,2.5 0 1 0 6,14.5 A2.5,2.5 0 1 0 6,9.5 Z M18,16 A2.5,2.5 0 1 0 18,21 A2.5,2.5 0 1 0 18,16 Z M8.2,10.8 L15.8,6.7 M8.2,13.2 L15.8,17.3",
        ["External"] = "M14,4 H20 V10 M20,4 L11,13 M18,14 V19 A1,1 0 0 1 17,20 H5 A1,1 0 0 1 4,19 V7 A1,1 0 0 1 5,6 H10",
        ["Refresh"] = "M20,12 A8,8 0 1 1 17.66,6.34 M20,4 V9 H15",
        ["Fingerprint"] = "M12,11 V14.5 C12,17 11,19 9.5,20.5 M8.5,9.2 A4,4 0 0 1 16,11 V13 M16,16.5 C15.7,18 15.2,19.2 14.5,20.3 M5.5,14.5 C5.8,13.3 6,12.2 6,11 A6,6 0 0 1 17.5,8.6 M18,12.5 V14 M7,18.5 C7.8,17.6 8.4,16.6 8.8,15.5",
        ["Sun"] = "M12,8 A4,4 0 1 0 12,16 A4,4 0 1 0 12,8 Z M12,2.5 V4.5 M12,19.5 V21.5 M2.5,12 H4.5 M19.5,12 H21.5 M5.3,5.3 L6.7,6.7 M17.3,17.3 L18.7,18.7 M5.3,18.7 L6.7,17.3 M17.3,6.7 L18.7,5.3",
        ["Moon"] = "M20,14.5 A8,8 0 1 1 9.5,4 A6.5,6.5 0 0 0 20,14.5 Z",
        ["Sparkle"] = "M12,3 L13.8,10.2 L21,12 L13.8,13.8 L12,21 L10.2,13.8 L3,12 L10.2,10.2 Z",
        ["Folder"] = "M3.5,6.5 A1.5,1.5 0 0 1 5,5 H9.5 L11.5,7 H19 A1.5,1.5 0 0 1 20.5,8.5 V17.5 A1.5,1.5 0 0 1 19,19 H5 A1.5,1.5 0 0 1 3.5,17.5 Z",
        ["Filter"] = "M4,5 H20 L14,12.5 V19 L10,17 V12.5 Z",
        ["Sort"] = "M7,4 V20 M3.5,16.5 L7,20 L10.5,16.5 M17,20 V4 M13.5,7.5 L17,4 L20.5,7.5",
        ["Grid"] = "M4,4 H10 V10 H4 Z M14,4 H20 V10 H14 Z M4,14 H10 V20 H4 Z M14,14 H20 V20 H14 Z",
        ["List"] = "M8,6 H20 M8,12 H20 M8,18 H20 M4,6 L4.01,6 M4,12 L4.01,12 M4,18 L4.01,18",
        ["WinMinimize"] = "M6,12 H18",
        ["WinMaximize"] = "M6.5,6.5 H17.5 V17.5 H6.5 Z",
        ["WinRestore"] = "M8.5,8.5 H17.5 V17.5 H8.5 Z M6.5,15 V6.5 H15",
        ["Power"] = "M12,3 V12 M18.4,6.6 A8.5,8.5 0 1 1 5.6,6.6",
        ["Hourglass"] = "M6,3 H18 M6,21 H18 M7,3 C7,8 12,10 12,12 C12,14 7,16 7,21 M17,3 C17,8 12,10 12,12 C12,14 17,16 17,21",
        ["Clipboard"] = "M9,4 H15 V6.5 H9 Z M15,5 H17.5 A1.5,1.5 0 0 1 19,6.5 V19.5 A1.5,1.5 0 0 1 17.5,21 H6.5 A1.5,1.5 0 0 1 5,19.5 V6.5 A1.5,1.5 0 0 1 6.5,5 H9",
        ["Chip"] = "M7,7 H17 V17 H7 Z M10,10 H14 V14 H10 Z M9,4 V7 M12,4 V7 M15,4 V7 M9,17 V20 M12,17 V20 M15,17 V20 M4,9 H7 M4,12 H7 M4,15 H7 M17,9 H20 M17,12 H20 M17,15 H20",
        ["Keyboard"] = "M3.5,6 H20.5 A1,1 0 0 1 21.5,7 V17 A1,1 0 0 1 20.5,18 H3.5 A1,1 0 0 1 2.5,17 V7 A1,1 0 0 1 3.5,6 Z M6,10 L6.01,10 M9.5,10 L9.51,10 M13,10 L13.01,10 M16.5,10 L16.51,10 M7,14.5 H17",
        ["Timer"] = "M12,7 A7,7 0 1 0 12,21 A7,7 0 1 0 12,7 Z M12,10.5 V14 L14.5,15.5 M10,3 H14 M12,3 V7",
        ["Calendar"] = "M5,5 H19 A1.5,1.5 0 0 1 20.5,6.5 V19 A1.5,1.5 0 0 1 19,20.5 H5 A1.5,1.5 0 0 1 3.5,19 V6.5 A1.5,1.5 0 0 1 5,5 Z M3.5,10 H20.5 M8,3 V7 M16,3 V7",
        ["Phone"] = "M6.5,3.5 H9 L10.5,8 L8.5,9.5 C9.5,11.7 12.3,14.5 14.5,15.5 L16,13.5 L20.5,15 V17.5 A2,2 0 0 1 18.5,19.5 C10.5,19 5,13.5 4.5,5.5 A2,2 0 0 1 6.5,3.5 Z",
        ["MapPin"] = "M12,21 C12,21 5,14.5 5,9.5 A7,7 0 0 1 19,9.5 C19,14.5 12,21 12,21 Z M12,7 A2.5,2.5 0 1 0 12,12 A2.5,2.5 0 1 0 12,7 Z",
        ["Hash"] = "M5,9 H19 M5,15 H19 M10,4 L8,20 M16,4 L14,20",
        ["Text"] = "M5,6 H19 M12,6 V19 M9,19 H15",
        ["Server"] = "M4,4 H20 V10 H4 Z M4,14 H20 V20 H4 Z M7.5,7 L7.51,7 M7.5,17 L7.51,17",
        ["Package"] = "M12,3 L20.5,7.5 V16.5 L12,21 L3.5,16.5 V7.5 Z M3.5,7.5 L12,12 L20.5,7.5 M12,12 V21",
        ["Archive"] = "M4,5 H20 V9 H4 Z M5,9 V19 A1,1 0 0 0 6,20 H18 A1,1 0 0 0 19,19 V9 M10,13 H14",
        ["Bolt"] = "M13,2.5 L4.5,13.5 H11.5 L10.5,21.5 L19.5,10 H12.5 Z",
        ["Sidebar"] = "M4,5 H20 V19 H4 Z M9,5 V19",
        ["Palette"] = "M12,3 A9,9 0 1 0 12,21 C13.2,21 13.6,20 13.2,19 C12.7,17.9 13.4,16.5 14.6,16.5 H17 A4,4 0 0 0 21,12.5 C21,7.3 17,3 12,3 Z M7.5,12 L7.51,12 M9.5,7.5 L9.51,7.5 M14.5,7.5 L14.51,7.5 M17,11.5 L17.01,11.5",
        ["Command"] = "M9,6 V18 M15,6 V18 M6,9 H18 M6,15 H18 M9,6 A3,3 0 1 0 6,9 M15,6 A3,3 0 1 1 18,9 M9,18 A3,3 0 1 1 6,15 M15,18 A3,3 0 1 0 18,15",
        ["Dice"] = "M5,4 H19 A1,1 0 0 1 20,5 V19 A1,1 0 0 1 19,20 H5 A1,1 0 0 1 4,19 V5 A1,1 0 0 1 5,4 Z M8.5,8.5 L8.51,8.5 M15.5,8.5 L15.51,8.5 M12,12 L12.01,12 M8.5,15.5 L8.51,15.5 M15.5,15.5 L15.51,15.5",
        ["Seal"] = "M12,2.5 A9.5,9.5 0 1 0 12,21.5 A9.5,9.5 0 1 0 12,2.5 Z M12,5.5 A6.5,6.5 0 1 0 12,18.5 A6.5,6.5 0 1 0 12,5.5 Z M12,8.6 A1.7,1.7 0 1 0 12,12 A1.7,1.7 0 1 0 12,8.6 Z M11.2,11.6 L10.6,15.2 H13.4 L12.8,11.6",
        ["Rotate"] = "M4,12 A8,8 0 0 1 18.2,7 M20,4 V8.5 H15.5 M20,12 A8,8 0 0 1 5.8,17 M4,20 V15.5 H8.5",
        ["Layers"] = "M12,3 L21,8 L12,13 L3,8 Z M3,12.5 L12,17.5 L21,12.5 M3,16.5 L12,21.5 L21,16.5",
    };

    public static IReadOnlyCollection<string> Names => Paths.Keys;

    public static void Register(ResourceDictionary dictionary)
    {
        foreach (var (name, data) in Paths)
        {
            var geometry = Geometry.Parse(data);
            geometry.Freeze();
            dictionary["Icon." + name] = geometry;
        }

        dictionary["Icon.Atom"] = Atom();
        dictionary["Icon.Gear"] = Gear();
    }

    public static Geometry Get(string name) =>
        Application.Current?.TryFindResource("Icon." + name) as Geometry ?? Geometry.Parse(Paths.GetValueOrDefault(name, Paths["Info"]));

    private static Geometry Atom()
    {
        var group = new GeometryGroup { FillRule = FillRule.Nonzero };
        group.Children.Add(new EllipseGeometry(new Point(12, 12), 1.4, 1.4));
        for (var i = 0; i < 3; i++)
        {
            group.Children.Add(new EllipseGeometry(new Point(12, 12), 9.5, 3.6, new RotateTransform(i * 60, 12, 12)));
        }

        group.Freeze();
        return group;
    }

    private static Geometry Gear()
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            const int teeth = 8;
            const double outer = 9.6, inner = 7.4;
            var first = true;
            for (var i = 0; i < teeth * 4; i++)
            {
                var angle = (i * Math.PI * 2 / (teeth * 4)) - (Math.PI / (teeth * 4));
                var radius = i % 4 is 1 or 2 ? outer : inner;
                var point = new Point(12 + (Math.Cos(angle) * radius), 12 + (Math.Sin(angle) * radius));
                if (first)
                {
                    context.BeginFigure(point, false, true);
                    first = false;
                }
                else
                {
                    context.LineTo(point, true, true);
                }
            }

            context.BeginFigure(new Point(15, 12), false, true);
            context.ArcTo(new Point(9, 12), new Size(3, 3), 0, false, SweepDirection.Clockwise, true, true);
            context.ArcTo(new Point(15, 12), new Size(3, 3), 0, false, SweepDirection.Clockwise, true, true);
        }

        geometry.Freeze();
        return geometry;
    }
}
