using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using OnlyYours.App.Interop;
using OnlyYours.App.Services;
using OnlyYours.App.ViewModels;

namespace OnlyYours.App.Views;

/// <summary>
/// The global quick search (Ctrl+Alt+P from anywhere): a borderless, topmost palette
/// centred on the monitor under the cursor. It closes on Esc, on deactivation and after
/// an action — copy a credential without ever switching to the main window.
/// </summary>
public sealed class QuickSearchWindow : Window
{
    public QuickSearchWindow(PaletteViewModel palette)
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Title = "OnlyYours";
        Content = new System.Windows.Controls.Border
        {
            Padding = new Thickness(30),
            Child = new PaletteView { DataContext = palette },
        };
        Deactivated += (_, _) => Close();
        SourceInitialized += (_, _) =>
        {
            ScreenCaptureGuard.Apply(this, AppServices.App?.Settings.ScreenCaptureProtection ?? true);
            Position();
        };
    }

    private void Position()
    {
        Native.GetCursorPos(out var cursor);
        var monitor = Native.MonitorFromPoint(cursor, 2);
        var info = new Native.MONITORINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Native.MONITORINFO>() };
        if (!Native.GetMonitorInfo(monitor, ref info))
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return;
        }

        var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        var transform = source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        var topLeft = transform.Transform(new Point(info.rcWork.Left, info.rcWork.Top));
        var bottomRight = transform.Transform(new Point(info.rcWork.Right, info.rcWork.Bottom));
        UpdateLayout();
        var width = ActualWidth > 0 ? ActualWidth : 720;
        Left = topLeft.X + ((bottomRight.X - topLeft.X - width) / 2);
        Top = topLeft.Y + ((bottomRight.Y - topLeft.Y) * 0.2);
    }
}
