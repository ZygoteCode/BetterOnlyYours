using System.Windows;
using OnlyYours.App.Services;
using OnlyYours.Core.Security.Memory;

namespace OnlyYours.App;

/// <summary>
/// Hand-written entry point: the single-instance check and the process hardening run
/// before WPF is even loaded, so a second launch costs a few milliseconds and the
/// defences are in place before any secret can exist.
/// </summary>
public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        StartupTrace.Mark("main");
        using var instance = SingleInstance.Acquire();
        StartupTrace.Mark("single-instance");
        if (!instance.IsPrimary)
        {
            instance.SignalPrimary();
            return 0;
        }

        ProcessHardening.ApplyEarly();
        SecureMemory.EnsureLockHeadroom();
        StartupTrace.Mark("hardened");
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Infrastructure.AppErrors.Log("fatal", e.ExceptionObject?.ToString() ?? "unknown");
            SecureMemory.WipeAll();
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Infrastructure.AppErrors.Report(e.Exception);
            e.SetObserved();
        };

        // Settings are tiny and source-generated: reading them here decides the very first pixels.
        var paths = Core.Platform.AppPaths.Resolve();
        var store = new Core.Settings.SettingsStore(paths.SettingsPath);
        var settings = store.Load();
        var minimized = args.Contains("--minimized", StringComparer.OrdinalIgnoreCase);

        // Without the opening sequence, a native splash puts the seal on screen within a few
        // milliseconds, while WPF is still loading; it dissolves into the first real frame.
        SplashScreen? splash = null;
        if (!minimized && !App.TestMode && settings.Intro != "cinematic")
        {
            try
            {
                splash = new SplashScreen(typeof(Program).Assembly, "Assets/Splash.png");
                splash.Show(autoClose: false, topMost: false);
                StartupTrace.Mark("splash");
            }
            catch (Exception error) when (error is System.IO.IOException or InvalidOperationException)
            {
                splash = null;
            }
        }

        var app = new App { StartMinimized = minimized, Paths = paths, Store = store, PreloadedSettings = settings, Splash = splash };
        StartupTrace.Mark("app-instance");
        app.InitializeComponent();
        instance.ListenForActivation(() => app.Dispatcher.BeginInvoke(app.ActivateFromSecondInstance));
        StartupTrace.Mark("app-created");
        return app.Run();
    }
}
