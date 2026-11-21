using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using OnlyYours.App.Infrastructure;
using OnlyYours.App.Services;
using OnlyYours.App.ViewModels;
using OnlyYours.App.Views;
using OnlyYours.Core.Platform;
using OnlyYours.Core.Security.Memory;
using OnlyYours.Core.Settings;

namespace OnlyYours.App;

public partial class App : Application
{
    private AppViewModel? _vm;
    private MainWindow? _window;
    private MessageWindow? _messages;
    private QuickSearchWindow? _quick;
    private bool _exiting;

    public bool StartMinimized { get; init; }

    /// <summary>Resolved by <see cref="Program"/> before WPF loads (null when started elsewhere).</summary>
    public AppPaths? Paths { get; init; }

    public SettingsStore? Store { get; init; }

    public AppSettings? PreloadedSettings { get; init; }

    /// <summary>The native splash shown while WPF starts; closed on the first frame.</summary>
    public SplashScreen? Splash { get; set; }

    public static bool TestMode { get; } = Environment.GetEnvironmentVariable("ONLYYOURS_TEST_MODE") == "1";

    public AppViewModel? ViewModel => _vm;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        StartupTrace.Mark("startup");
        AppErrors.EnableDiagnostics();
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppErrors.Reported += error => _vm?.Toast(Loc.T("Error.Unexpected"), error.Message, ToastKind.Error);

        Timeline.DesiredFrameRateProperty.OverrideMetadata(typeof(Timeline), new FrameworkPropertyMetadata { DefaultValue = DisplayRefreshRate() });

        var paths = Paths ?? AppPaths.Resolve();
        var store = Store ?? new SettingsStore(paths.SettingsPath);
        var settings = PreloadedSettings ?? store.Load();
        if (TestMode)
        {
            settings.ScreenCaptureProtection = false;
            settings.GlobalHotkeys = false;
            settings.Intro = Environment.GetEnvironmentVariable("ONLYYOURS_INTRO") ?? "off";
        }

        Icons.Register(Resources);
        ApplyAppearance(settings);
        StartupTrace.Mark("resources");

        _vm = new AppViewModel(paths, store, settings);
        StartupTrace.Mark("viewmodel");
        _vm.ActivateRequested += ActivateMainWindow;
        _vm.QuickSearchRequested += ShowQuickSearch;
        _vm.Start();
        StartupTrace.Mark("stage");

        _window = new MainWindow(_vm);
        StartupTrace.Mark("window-created");
        MainWindow = _window;
        _window.ContentRendered += OnFirstFrame;
        if (StartMinimized && (settings.MinimizeToTray || settings.CloseToTray))
        {
            Dispatcher.BeginInvoke(DeferredInit, DispatcherPriority.ApplicationIdle);
        }
        else
        {
            _window.Show();
        }

        StartupTrace.Mark("window-shown");
    }

    private void OnFirstFrame(object? sender, EventArgs e)
    {
        _window!.ContentRendered -= OnFirstFrame;
        StartupTrace.Mark("first-frame");
        Splash?.Close(TimeSpan.FromMilliseconds(240));
        Splash = null;
        Dispatcher.BeginInvoke(DeferredInit, DispatcherPriority.ApplicationIdle);
    }

    /// <summary>Everything not needed for the first frame happens once the UI is idle.</summary>
    private void DeferredInit()
    {
        if (_vm is null || _messages is not null)
        {
            return;
        }

        _messages = new MessageWindow();
        _vm.Hotkeys = new HotkeyService(_messages);
        _vm.Tray = new TrayIcon(_messages);
        _vm.Tray.Activated += ActivateMainWindow;
        _vm.Tray.MenuRequested += () => _window?.ShowTrayMenu();
        _vm.AutoType.Start();
        ApplyBehavior(_vm);
        if (_vm.Settings.ProcessHardening && !TestMode)
        {
            ProcessHardening.RestrictProcessAccess();
        }

        if (StartMinimized && _window is { IsVisible: false })
        {
            _vm.Tray.Show();
        }

        StartupTrace.Mark("deferred-init");
    }

    public static void ApplyAppearance(AppSettings settings)
    {
        Motion.Level = Motion.Parse(settings.Motion);
        ThemeManager.Apply(Current.Resources, settings.Theme, settings.Accent);
        Loc.Apply(Current.Resources);
    }

    public static void ApplyBehavior(AppViewModel vm)
    {
        var settings = vm.Settings;
        if (vm.Hotkeys is { } hotkeys)
        {
            hotkeys.UnregisterAll();
            if (settings.GlobalHotkeys)
            {
                hotkeys.Register(HotkeyGesture.QuickSearch, vm.RequestQuickSearch);
                hotkeys.Register(HotkeyGesture.AutoType, () => _ = vm.GlobalAutoTypeAsync());
            }
        }

        if (vm.Tray is { } tray)
        {
            if (settings.MinimizeToTray || settings.CloseToTray)
            {
                tray.Show();
            }
            else
            {
                tray.Hide();
            }
        }

        if (Current?.MainWindow is { } window)
        {
            ScreenCaptureGuard.Apply(window, settings.ScreenCaptureProtection);
        }
    }

    public void ActivateFromSecondInstance() => ActivateMainWindow();

    private void ActivateMainWindow()
    {
        if (_window is null)
        {
            return;
        }

        if (!_window.IsVisible)
        {
            _window.Show();
        }

        if (_window.WindowState == WindowState.Minimized)
        {
            _window.WindowState = WindowState.Normal;
        }

        _window.Activate();
        _window.Topmost = true;
        _window.Topmost = false;
        _window.Focus();
    }

    private void ShowQuickSearch()
    {
        if (_vm?.Shell is not { } shell)
        {
            ActivateMainWindow();
            return;
        }

        _quick?.Close();
        _quick = new QuickSearchWindow(new PaletteViewModel(_vm, shell, quickMode: true));
        _quick.Closed += (_, _) => _quick = null;
        _quick.Show();
        _quick.Activate();
    }

    /// <summary>
    /// Fast, safe exit: the window disappears at once, pending writes get a short budget,
    /// every secret is wiped, then the process ends without waiting for WPF teardown.
    /// </summary>
    public async Task ExitAsync()
    {
        if (_exiting)
        {
            return;
        }

        _exiting = true;
        StartupTrace.Mark("exit-requested");
        _window?.Hide();
        _quick?.Close();
        if (_vm is not null)
        {
            await _vm.ShutdownAsync();
        }

        _messages?.Dispose();
        SecureMemory.WipeAll();
        StartupTrace.Mark("exit-done");
        Environment.Exit(0);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppErrors.Report(e.Exception);
        e.Handled = true;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;

        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public int dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public int dmDisplayOrientation;
        public int dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;

        public short dmLogPixels;
        public int dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;
        public int dmDisplayFlags;
        public int dmDisplayFrequency;
        public int dmICMMethod;
        public int dmICMIntent;
        public int dmMediaType;
        public int dmDitherType;
        public int dmReserved1;
        public int dmReserved2;
        public int dmPanningWidth;
        public int dmPanningHeight;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplaySettings(string? deviceName, int modeNumber, ref DEVMODE mode);

    /// <summary>Animations tick at the display's refresh rate (capped at 144 Hz) — buttery on high-refresh panels.</summary>
    private static int DisplayRefreshRate()
    {
        try
        {
            var mode = new DEVMODE { dmDeviceName = string.Empty, dmFormName = string.Empty, dmSize = (short)Marshal.SizeOf<DEVMODE>() };
            if (EnumDisplaySettings(null, -1, ref mode) && mode.dmDisplayFrequency is > 1)
            {
                return Math.Clamp(mode.dmDisplayFrequency, 60, 144);
            }
        }
        catch
        {
            // Fall back to 60 Hz.
        }

        return 60;
    }
}
