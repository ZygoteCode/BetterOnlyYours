using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using OnlyYours.App.Infrastructure;
using OnlyYours.App.ViewModels;
using OnlyYours.Core.Platform;
using OnlyYours.Core.Settings;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace OnlyYours.App.Tests;

/// <summary>
/// One WPF application for the whole test run, living on its own STA thread exactly like
/// the real app's UI thread. Tests marshal their work onto it with <see cref="Run(Func{Task})"/>.
/// </summary>
internal static class Ui
{
    private static readonly Lazy<Dispatcher> Host = new(Start, LazyThreadSafetyMode.ExecutionAndPublication);
    private static readonly ConcurrentQueue<string> BindingErrors = new();
    private static readonly ConcurrentQueue<Exception> Exceptions = new();

    public static Dispatcher Dispatcher => Host.Value;

    public static string RepoRoot { get; } = FindRepoRoot();

    public static string SnapshotDirectory { get; } = Path.Combine(RepoRoot, "tests", "OnlyYours.App.Tests", "Snapshots");

    private static Dispatcher Start()
    {
        // Hermetic: no real profile, no legacy discovery outside the test folders.
        Environment.SetEnvironmentVariable("BETTERONLYYOURS_VAULT", "none");
        Dispatcher? dispatcher = null;
        Exception? failure = null;
        using var ready = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            try
            {
                var app = new App();
                app.InitializeComponent();
                Icons.Register(app.Resources);
                Motion.Level = MotionLevel.Off;
                ThemeManager.Apply(app.Resources, "dark", "aurum");
                Loc.Apply(app.Resources);

                PresentationTraceSources.Refresh();
                PresentationTraceSources.DataBindingSource.Listeners.Add(new Collector());
                PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;
                dispatcher = Dispatcher.CurrentDispatcher;
                dispatcher.UnhandledException += (_, e) =>
                {
                    Exceptions.Enqueue(e.Exception);
                    e.Handled = true;
                };
                AppErrors.Reported += Exceptions.Enqueue;
            }
            catch (Exception error)
            {
                failure = error;
            }
            finally
            {
                ready.Set();
            }

            if (failure is null)
            {
                Dispatcher.Run();
            }
        })
        {
            IsBackground = true,
            Name = "OnlyYours test UI thread",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait();
        if (failure is not null)
        {
            throw new InvalidOperationException("The WPF test host failed to start.", failure);
        }

        return dispatcher!;
    }

    public static Task Run(Action action) => Dispatcher.InvokeAsync(action).Task;

    public static Task<T> Run<T>(Func<T> action) => Dispatcher.InvokeAsync(action).Task;

    public static Task Run(Func<Task> action) => Dispatcher.InvokeAsync(action).Task.Unwrap();

    public static Task<T> Run<T>(Func<Task<T>> action) => Dispatcher.InvokeAsync(action).Task.Unwrap();

    /// <summary>Lets queued layout, bindings, Loaded handlers and async continuations run.</summary>
    public static async Task Settle(int rounds = 3)
    {
        for (var i = 0; i < rounds; i++)
        {
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            await Task.Delay(15);
            await Dispatcher.Yield(DispatcherPriority.ContextIdle);
        }
    }

    public static void Theme(string theme, string accent = "aurum") => ThemeManager.Apply(Application.Current.Resources, theme, accent);

    public static IReadOnlyList<Exception> TakeExceptions()
    {
        var list = new List<Exception>();
        while (Exceptions.TryDequeue(out var error))
        {
            list.Add(error);
        }

        return list;
    }

    public static IReadOnlyList<string> TakeBindingErrors()
    {
        var list = new List<string>();
        while (BindingErrors.TryDequeue(out var message))
        {
            list.Add(message);
        }

        return list;
    }

    /// <summary>Renders <paramref name="element"/> to a PNG under Snapshots/ and returns its path.</summary>
    public static string Capture(FrameworkElement element, string name)
    {
        element.UpdateLayout();
        var width = Math.Max(1, (int)Math.Ceiling(element.ActualWidth));
        var height = Math.Max(1, (int)Math.Ceiling(element.ActualHeight));
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        Directory.CreateDirectory(SnapshotDirectory);
        var path = Path.Combine(SnapshotDirectory, name + ".png");
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
        return path;
    }

    /// <summary>
    /// Visible text painted with the Windows system text colour (pure black) instead of the
    /// palette — the tell-tale of a control whose theme style overrides inherited colours.
    /// </summary>
    public static List<string> SystemColoredText(DependencyObject root)
    {
        var offenders = new List<string>();
        void Walk(DependencyObject node)
        {
            if (node is FrameworkElement { IsVisible: false })
            {
                return;
            }

            if (node is System.Windows.Controls.TextBlock { ActualWidth: > 0 } block && !string.IsNullOrWhiteSpace(block.Text))
            {
                if (block.Foreground is SolidColorBrush { Color: { A: 255, R: 0, G: 0, B: 0 } })
                {
                    offenders.Add(block.Text);
                }

                foreach (var inline in block.Inlines.OfType<System.Windows.Documents.Run>())
                {
                    if (inline.Foreground is SolidColorBrush { Color: { A: 255, R: 0, G: 0, B: 0 } } && !string.IsNullOrWhiteSpace(inline.Text))
                    {
                        offenders.Add(inline.Text);
                    }
                }
            }

            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            {
                Walk(VisualTreeHelper.GetChild(node, i));
            }
        }

        Walk(root);
        return offenders;
    }

    /// <summary>Fraction of pixels that differ from the most common colour: catches blank renders.</summary>
    public static double Detail(FrameworkElement element)
    {
        element.UpdateLayout();
        var width = Math.Max(1, (int)element.ActualWidth);
        var height = Math.Max(1, (int)element.ActualHeight);
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var pixels = new int[width * height];
        bitmap.CopyPixels(pixels, width * 4, 0);
        var dominant = pixels.GroupBy(p => p).OrderByDescending(g => g.Count()).First().Key;
        return pixels.Count(p => p != dominant) / (double)pixels.Length;
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OnlyYours.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private sealed class Collector : TraceListener
    {
        private readonly System.Text.StringBuilder _line = new();

        public override void Write(string? message)
        {
            lock (_line)
            {
                _line.Append(message);
            }
        }

        public override void WriteLine(string? message)
        {
            string text;
            lock (_line)
            {
                _line.Append(message);
                text = _line.ToString();
                _line.Clear();
            }

            BindingErrors.Enqueue(text);
        }
    }
}

/// <summary>A temporary data folder and an <see cref="AppViewModel"/> wired to it.</summary>
internal sealed class TestApp : IDisposable
{
    private TestApp(string root, AppViewModel vm)
    {
        Root = root;
        Vm = vm;
    }

    public string Root { get; }

    public AppViewModel Vm { get; }

    public static TestApp Create(Action<AppSettings>? configure = null)
    {
        var root = Path.Combine(Path.GetTempPath(), "onlyyours-app-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var store = new SettingsStore(Path.Combine(root, "settings.json"));
        var settings = store.Load();
        settings.Intro = "off";
        settings.ScreenCaptureProtection = false;
        settings.GlobalHotkeys = false;
        settings.KdfStrength = "standard";
        configure?.Invoke(settings);
        var vm = new AppViewModel(AppPaths.ForDirectory(root), store, settings);
        return new TestApp(root, vm);
    }

    public void Dispose()
    {
        try
        {
            Vm.Session?.Dispose();
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
