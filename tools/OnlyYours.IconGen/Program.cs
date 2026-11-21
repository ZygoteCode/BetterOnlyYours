using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OnlyYours.App.Controls;

// Usage: dotnet run --project tools/OnlyYours.IconGen -- <assets directory>
var output = args.Length > 0 ? args[0] : Path.Combine("src", "OnlyYours.App", "Assets");
Directory.CreateDirectory(output);

var thread = new Thread(() =>
{
    int[] sizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256];
    var images = new List<byte[]>();
    foreach (var size in sizes)
    {
        var png = Render(size);
        images.Add(png);
        if (size == 256)
        {
            File.WriteAllBytes(Path.Combine(output, "OnlyYours-256.png"), png);
        }
    }

    using var stream = File.Create(Path.Combine(output, "OnlyYours.ico"));
    using var writer = new BinaryWriter(stream);
    writer.Write((ushort)0);
    writer.Write((ushort)1);
    writer.Write((ushort)sizes.Length);
    var offset = 6 + (16 * sizes.Length);
    for (var i = 0; i < sizes.Length; i++)
    {
        writer.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
        writer.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write(images[i].Length);
        writer.Write(offset);
        offset += images[i].Length;
    }

    foreach (var image in images)
    {
        writer.Write(image);
    }

    File.WriteAllBytes(Path.Combine(output, "Splash.png"), RenderSplash(340));
    Console.WriteLine($"Wrote {sizes.Length} sizes and the splash to {Path.GetFullPath(output)}");
});
thread.SetApartmentState(ApartmentState.STA);
thread.Start();
thread.Join();

// The start-up splash: the seal floating in a warm golden halo (per-pixel alpha, so the
// halo melts into whatever is on the desktop).
static byte[] RenderSplash(int size)
{
    var visual = new DrawingVisual();
    using (var dc = visual.RenderOpen())
    {
        var halo = new RadialGradientBrush
        {
            GradientStops =
            {
                new GradientStop(Color.FromArgb(0x9C, 0xF0, 0xC8, 0x73), 0.0),
                new GradientStop(Color.FromArgb(0x55, 0xD8, 0xA2, 0x4C), 0.38),
                new GradientStop(Color.FromArgb(0x18, 0x8E, 0x60, 0x24), 0.7),
                new GradientStop(Color.FromArgb(0x00, 0x8E, 0x60, 0x24), 1.0),
            },
        };
        halo.Freeze();
        dc.DrawEllipse(halo, null, new Point(size / 2.0, size / 2.0), size / 2.0, size / 2.0);
        var mark = size * 0.56;
        dc.PushTransform(new TranslateTransform((size - mark) / 2, (size - mark) / 2));
        SealArt.DrawMark(dc, mark, detailed: true);
        dc.Pop();
    }

    var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
    bitmap.Render(visual);
    var encoder = new PngBitmapEncoder();
    encoder.Frames.Add(BitmapFrame.Create(bitmap));
    using var memory = new MemoryStream();
    encoder.Save(memory);
    return memory.ToArray();
}

static byte[] Render(int size)
{
    var visual = new DrawingVisual();
    using (var dc = visual.RenderOpen())
    {
        SealArt.DrawMark(dc, size, detailed: size >= 32);
    }

    var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
    bitmap.Render(visual);
    var encoder = new PngBitmapEncoder();
    encoder.Frames.Add(BitmapFrame.Create(bitmap));
    using var memory = new MemoryStream();
    encoder.Save(memory);
    return memory.ToArray();
}
