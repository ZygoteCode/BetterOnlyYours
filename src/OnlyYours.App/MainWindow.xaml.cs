using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using OnlyYours.App.Controls;
using OnlyYours.App.Infrastructure;
using OnlyYours.App.Interop;
using OnlyYours.App.Services;
using OnlyYours.App.ViewModels;
using OnlyYours.App.Views;
using OnlyYours.Core.Settings;

namespace OnlyYours.App;

public partial class MainWindow : Window
{
    private readonly AppViewModel _vm;
    private readonly DispatcherTimer _boundsSave = new() { Interval = TimeSpan.FromMilliseconds(600) };
    private bool _maxPressed;
    private bool _introStarted;

    public MainWindow(AppViewModel vm)
    {
        _vm = vm;
        InitializeComponent();
        DataContext = vm;
        ApplySavedBounds(vm.Settings.Window);
        _boundsSave.Tick += (_, _) =>
        {
            _boundsSave.Stop();
            SaveBounds();
        };
        SizeChanged += (_, _) => ScheduleBoundsSave();
        LocationChanged += (_, _) => ScheduleBoundsSave();
        StateChanged += OnStateChanged;
        Loaded += OnLoaded;
        Closing += OnClosing;
        PreviewKeyDown += OnPreviewKeyDown;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppViewModel.IsUnlocked))
            {
                Title = vm.IsUnlocked ? $"OnlyYours — {vm.VaultDisplayName}" : "OnlyYours";
            }
        };
    }

    // ================================================================ chrome

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var handle = new WindowInteropHelper(this).Handle;
        _vm.Clipboard.OwnerHandle = handle;
        unsafe
        {
            var round = Native.DWMWCP_ROUND;
            Native.DwmSetWindowAttribute(handle, Native.DWMWA_WINDOW_CORNER_PREFERENCE, &round, sizeof(int));
            var dark = ThemeManager.IsDark ? 1 : 0;
            Native.DwmSetWindowAttribute(handle, Native.DWMWA_USE_IMMERSIVE_DARK_MODE, &dark, sizeof(int));
        }

        HwndSource.FromHwnd(handle)?.AddHook(WindowHook);
        ScreenCaptureGuard.Apply(this, _vm.Settings.ScreenCaptureProtection);
        UpdateMaximizedMargin();
    }

    /// <summary>
    /// Reports the custom maximise button to Windows as HTMAXBUTTON so that hovering it
    /// opens the Windows 11 Snap Layouts flyout, and handles its clicks.
    /// </summary>
    private nint WindowHook(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        switch (message)
        {
            case Native.WM_NCHITTEST:
                if (IsOverMaximize(lParam))
                {
                    Hint.SetIsActive(MaximizeButton, true);
                    handled = true;
                    return Native.HTMAXBUTTON;
                }

                Hint.SetIsActive(MaximizeButton, false);
                break;
            case Native.WM_NCMOUSELEAVE:
                Hint.SetIsActive(MaximizeButton, false);
                _maxPressed = false;
                break;
            case Native.WM_NCLBUTTONDOWN when (int)wParam == Native.HTMAXBUTTON:
                _maxPressed = true;
                handled = true;
                break;
            case Native.WM_NCLBUTTONUP when (int)wParam == Native.HTMAXBUTTON:
                if (_maxPressed)
                {
                    ToggleMaximize();
                }

                _maxPressed = false;
                handled = true;
                break;
        }

        return 0;
    }

    private bool IsOverMaximize(nint lParam)
    {
        if (!MaximizeButton.IsVisible)
        {
            return false;
        }

        var x = (short)(lParam.ToInt64() & 0xFFFF);
        var y = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
        try
        {
            var point = MaximizeButton.PointFromScreen(new Point(x, y));
            return point.X >= 0 && point.Y >= 0 && point.X < MaximizeButton.ActualWidth && point.Y < MaximizeButton.ActualHeight;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        UpdateMaximizedMargin();
        Hint.SetIcon(MaximizeButton, (Geometry)FindResource(WindowState == WindowState.Maximized ? "Icon.WinRestore" : "Icon.WinMaximize"));
        if (WindowState == WindowState.Minimized)
        {
            if (_vm.Settings.LockOnMinimize && _vm.IsUnlocked)
            {
                _ = _vm.LockAsync("Lock.Reason.Minimize");
            }

            if (_vm.Settings.MinimizeToTray && _vm.Tray is not null)
            {
                _vm.Tray.Show();
                Hide();
            }
        }

        ScheduleBoundsSave();
    }

    private void UpdateMaximizedMargin()
    {
        if (WindowState != WindowState.Maximized)
        {
            Root.Margin = new Thickness(0);
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        var frameX = (Native.GetSystemMetrics(32) + Native.GetSystemMetrics(92)) / dpi.DpiScaleX;
        var frameY = (Native.GetSystemMetrics(33) + Native.GetSystemMetrics(92)) / dpi.DpiScaleY;
        Root.Margin = new Thickness(frameX, frameY, frameX, frameY);
    }

    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximize(object sender, RoutedEventArgs e) => ToggleMaximize();

    private void ToggleMaximize() => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    /// <summary>Lets the visual tests close a window without ending their own process.</summary>
    internal bool ClosesWithoutExit { get; set; }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (ClosesWithoutExit)
        {
            return;
        }

        e.Cancel = true;
        SaveBounds();
        if (_vm.Settings.CloseToTray && _vm.Tray is not null)
        {
            _vm.Tray.Show();
            Hide();
            return;
        }

        _ = ((App)Application.Current).ExitAsync();
    }

    // ================================================================ intro

    /// <summary>The opening sequence while (and after) it plays; null when it was off.</summary>
    internal IntroView? Intro { get; private set; }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_introStarted)
        {
            return;
        }

        _introStarted = true;
        var mode = _vm.Settings.Intro;
        if (mode == "off" || Motion.IsOff || _vm.Stage is ShellViewModel)
        {
            return;
        }

        var intro = new IntroView();
        Intro = intro;
        IntroHost.Content = intro;
        intro.Play(mode, FindHeroEmblem, () =>
        {
            intro.Visibility = Visibility.Collapsed;
            IntroHost.Content = null;
        });
    }

    private FrameworkElement? FindHeroEmblem() => FindDescendant<SealEmblem>(StageHost, e => e.Tag as string == "Hero");

    private static T? FindDescendant<T>(DependencyObject root, Func<T, bool> predicate)
        where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match && predicate(match))
            {
                return match;
            }

            if (FindDescendant(child, predicate) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    // ============================================================= keyboard

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Intro is { IsVisible: true })
        {
            Intro.Skip();
            e.Handled = true;
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        var shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        if (_vm.Dialogs.Count > 0)
        {
            if (key == Key.Escape)
            {
                _vm.Dialogs[^1].Close(null);
                e.Handled = true;
            }

            return;
        }

        if (_vm.HasPalette)
        {
            return;
        }

        var shell = _vm.Shell;
        if (shell is null)
        {
            return;
        }

        var typing = Keyboard.FocusedElement is TextBox or SecureField;
        switch (key)
        {
            case Key.K when ctrl:
                _vm.OpenPalette();
                break;
            case Key.L when ctrl:
                _ = _vm.LockAsync(null);
                break;
            case Key.N when ctrl:
                shell.NewEntry(Core.Vault.Model.EntryKind.Login);
                break;
            case Key.F when ctrl:
                shell.Navigate(ShellPage.Vault);
                shell.FocusSearch();
                break;
            case Key.G when ctrl:
                shell.Navigate(ShellPage.Generator);
                break;
            case Key.OemComma when ctrl:
                shell.Navigate(ShellPage.Settings);
                break;
            case Key.C when ctrl && shift:
                shell.CopyPasswordCommand.Execute(null);
                break;
            case Key.U when ctrl && shift:
                shell.CopyUsernameCommand.Execute(null);
                break;
            case Key.T when ctrl && shift:
                shell.CopyTotpCommand.Execute(null);
                break;
            case Key.A when ctrl && shift:
                shell.AutoTypeCommand.Execute(null);
                break;
            case Key.O when ctrl && shift:
                shell.OpenUrlCommand.Execute(null);
                break;
            case Key.D when ctrl:
                shell.FavoriteCommand.Execute(null);
                break;
            case Key.E when ctrl:
            case Key.F2:
                shell.BeginEdit();
                break;
            case Key.S when ctrl:
                if (shell.IsEditing)
                {
                    shell.SaveEditCommand.Execute(null);
                }

                break;
            case Key.Escape:
                if (shell.IsEditing)
                {
                    shell.Editor?.CancelCommand.Execute(null);
                }
                else if (shell.HasQuery)
                {
                    shell.Query = string.Empty;
                }
                else
                {
                    return;
                }

                break;
            case Key.Delete when !typing && !shell.IsEditing:
                shell.TrashCommand.Execute(null);
                break;
            case >= Key.D1 and <= Key.D7 when ctrl:
                shell.Navigate((ShellPage)(key - Key.D1));
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private void OnPaletteScrim(object sender, MouseButtonEventArgs e) => _vm.ClosePalette();

    // ================================================================= tray

    public void ShowTrayMenu()
    {
        var menu = new ContextMenu { Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint };
        MenuItem Item(string key, string icon, Action action)
        {
            var item = new MenuItem { Header = Loc.T(key) };
            Hint.SetIcon(item, (Geometry)FindResource(icon));
            item.Click += (_, _) => action();
            return item;
        }

        menu.Items.Add(Item("Tray.Open", "Icon.Seal", () => _vm.RequestActivate()));
        if (_vm.IsUnlocked)
        {
            menu.Items.Add(Item("Tray.QuickSearch", "Icon.Search", _vm.RequestQuickSearch));
            menu.Items.Add(Item("Tray.Generate", "Icon.Wand", () =>
            {
                using var secret = Core.Generators.PasswordGenerator.Generate(_vm.Settings.Generator);
                _vm.Clipboard.CopySecret(secret.Value.Span, Loc.T("Clipboard.Generated"));
            }));
            menu.Items.Add(Item("Tray.Lock", "Icon.Lock", () => _ = _vm.LockAsync(null)));
        }

        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Tray.Exit", "Icon.Power", () => _ = ((App)Application.Current).ExitAsync()));

        // A context menu needs an active owner window to close correctly when focus leaves.
        var host = new Window { WindowStyle = WindowStyle.None, ShowInTaskbar = false, Width = 0, Height = 0, AllowsTransparency = true, Background = Brushes.Transparent, Topmost = true };
        host.Show();
        host.Activate();
        menu.Closed += (_, _) => host.Close();
        menu.IsOpen = true;
    }

    // =============================================================== bounds

    private void ScheduleBoundsSave()
    {
        if (!IsLoaded)
        {
            return;
        }

        _boundsSave.Stop();
        _boundsSave.Start();
    }

    private void ApplySavedBounds(WindowPlacement? placement)
    {
        if (placement is null || placement.Width < 400 || placement.Height < 300)
        {
            return;
        }

        var virtualLeft = SystemParameters.VirtualScreenLeft;
        var virtualTop = SystemParameters.VirtualScreenTop;
        var virtualRight = virtualLeft + SystemParameters.VirtualScreenWidth;
        var virtualBottom = virtualTop + SystemParameters.VirtualScreenHeight;
        if (placement.Left + 100 > virtualRight || placement.Top + 50 > virtualBottom || placement.Left + placement.Width < virtualLeft + 100 || placement.Top < virtualTop - 20)
        {
            return;
        }

        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = placement.Left;
        Top = placement.Top;
        Width = placement.Width;
        Height = placement.Height;
        if (placement.Maximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    private void SaveBounds()
    {
        if (WindowState == WindowState.Minimized || !IsLoaded)
        {
            return;
        }

        var bounds = WindowState == WindowState.Maximized ? RestoreBounds : new Rect(Left, Top, Width, Height);
        if (bounds.IsEmpty)
        {
            return;
        }

        _vm.Settings.Window = new WindowPlacement
        {
            Left = bounds.Left,
            Top = bounds.Top,
            Width = bounds.Width,
            Height = bounds.Height,
            Maximized = WindowState == WindowState.Maximized,
        };
        _vm.SaveSettings();
    }
}
