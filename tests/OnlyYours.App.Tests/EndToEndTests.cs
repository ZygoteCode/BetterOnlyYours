using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Xunit;
using Xunit.Abstractions;

namespace OnlyYours.App.Tests;

/// <summary>
/// Drives the real OnlyYours.exe through UI Automation in an isolated profile: first run,
/// vault creation, an entry, lock, unlock and exit — measuring start-up and shutdown.
/// </summary>
public class EndToEndTests(ITestOutputHelper output)
{
#if DEBUG
    private const string Configuration = "Debug";
#else
    private const string Configuration = "Release";
#endif

    private const string Password = "E2E-passphrase-Copper-Lantern-91!";

    private static string ExePath => Path.Combine(Ui.RepoRoot, "src", "OnlyYours.App", "bin", Configuration, "net11.0-windows10.0.19041.0", "win-x64", "OnlyYours.exe");

    [Fact]
    public void FirstRunCreateUseLockUnlockAndExit()
    {
        Assert.True(File.Exists(ExePath), $"build the app first: {ExePath}");
        var root = Path.Combine(Path.GetTempPath(), "onlyyours-e2e", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        using var process = Launch(root);
        try
        {
            var window = WaitFor(() => MainWindowOf(process), TimeSpan.FromSeconds(30), "main window");
            Find(window, "WelcomeView");
            Shot(process, "e2e-01-welcome");

            Invoke(Find(window, "CreateCard"));
            Find(window, "CreateVaultView");
            SetValue(Find(window, "VaultNameInput"), "E2E");
            SetValue(Find(window, "NewPassword"), Password);
            SetValue(Find(window, "ConfirmPassword"), Password);
            Shot(process, "e2e-02-create");
            Invoke(Find(window, "CreateVaultButton"));

            var ack = Find(window, "RecoveryAck", TimeSpan.FromSeconds(30));
            Shot(process, "e2e-03-recovery");
            ((TogglePattern)ack.GetCurrentPattern(TogglePattern.Pattern)).Toggle();
            Invoke(Find(window, "RecoveryContinue"));
            if (TryFind(window, "HelloSkip", TimeSpan.FromSeconds(2)) is { } skip)
            {
                Invoke(skip);
            }

            Invoke(Find(window, "OpenVaultButton"));
            Find(window, "ShellView", TimeSpan.FromSeconds(10));
            Shot(process, "e2e-04-shell");

            Invoke(Find(window, "NewEntryButton"));

            // The kinds menu is a popup: a top-level UI Automation window of its own.
            Invoke(FindInProcess(process, "NewKind.Login"));
            SetValue(Find(window, "EditorTitle"), "Created by UI Automation");
            Invoke(Find(window, "EditorSave"));
            WaitUntil(() => Find(window, "EntryList").FindAll(TreeScope.Children, System.Windows.Automation.Condition.TrueCondition).Count >= 1, TimeSpan.FromSeconds(10), "entry in list");
            Shot(process, "e2e-05-entry");

            Invoke(Find(window, "LockButton"));
            Find(window, "LockView", TimeSpan.FromSeconds(10));
            SetValue(Find(window, "MasterPassword"), "wrong password");
            Invoke(Find(window, "UnlockButton"));
            Find(window, "LockError", TimeSpan.FromSeconds(20));
            Shot(process, "e2e-06-lock-error");

            SetValue(Find(window, "MasterPassword"), Password);
            Invoke(Find(window, "UnlockButton"));
            Find(window, "ShellView", TimeSpan.FromSeconds(20));
            Assert.Contains(Find(window, "EntryList").FindAll(TreeScope.Descendants, System.Windows.Automation.Condition.TrueCondition).Cast<AutomationElement>(), e => e.Current.Name.Contains("Created by UI Automation", StringComparison.Ordinal));

            var exit = Stopwatch.StartNew();
            ((WindowPattern)window.GetCurrentPattern(WindowPattern.Pattern)).Close();
            Assert.True(process.WaitForExit(10_000), "the app did not exit");
            exit.Stop();
            output.WriteLine($"shutdown: {exit.ElapsedMilliseconds} ms");

            var trace = File.ReadAllLines(Path.Combine(root, "trace.log"))
                .Select(l => l.Split('\t'))
                .ToDictionary(p => p[0], p => double.Parse(p[1], CultureInfo.InvariantCulture));
            foreach (var (mark, ms) in trace)
            {
                output.WriteLine($"{mark,-16} {ms,8:0.0} ms");
            }

            Assert.True(trace["first-frame"] < 5000, $"first frame after {trace["first-frame"]} ms");
            Assert.True(trace["exit-done"] - trace["exit-requested"] < 2500, "exit took too long");
            Assert.True(exit.ElapsedMilliseconds < 5000);

            var errors = Path.Combine(root, "errors.log");
            Assert.False(File.Exists(errors) && File.ReadAllText(errors).Trim().Length > 0, File.Exists(errors) ? File.ReadAllText(errors) : null);
            Assert.True(File.Exists(Path.Combine(root, "home", "vault.onlyyours")));
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            TryDelete(root);
        }
    }

    [Fact]
    public void ASecondLaunchActivatesTheFirstInstance()
    {
        var root = Path.Combine(Path.GetTempPath(), "onlyyours-e2e", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        using var first = Launch(root);
        try
        {
            WaitFor(() => MainWindowOf(first), TimeSpan.FromSeconds(30), "main window");
            using var second = Launch(root, traceName: "trace2.log");
            Assert.True(second.WaitForExit(10_000), "the second instance kept running");
            Assert.Equal(0, second.ExitCode);
            Assert.False(first.HasExited);
        }
        finally
        {
            if (!first.HasExited)
            {
                first.Kill(entireProcessTree: true);
            }

            TryDelete(root);
        }
    }

    // ---------------------------------------------------------------- helpers

    private static Process Launch(string root, string traceName = "trace.log")
    {
        var info = new ProcessStartInfo(ExePath) { UseShellExecute = false };
        info.Environment["ONLYYOURS_HOME"] = Path.Combine(root, "home");
        info.Environment["ONLYYOURS_TEST_MODE"] = "1";
        info.Environment["ONLYYOURS_INSTANCE"] = "e2e-" + Path.GetFileName(root);
        info.Environment["ONLYYOURS_TRACE_STARTUP"] = "1";
        info.Environment["ONLYYOURS_TRACE_FILE"] = Path.Combine(root, traceName);
        info.Environment["ONLYYOURS_ERROR_LOG"] = Path.Combine(root, "errors.log");
        info.Environment["ONLYYOURS_INTRO"] = "off";
        info.Environment["BETTERONLYYOURS_VAULT"] = "none";
        return Process.Start(info)!;
    }

    private static AutomationElement? MainWindowOf(Process process)
    {
        process.Refresh();
        if (process.HasExited)
        {
            throw new InvalidOperationException($"OnlyYours exited early with code {process.ExitCode}.");
        }

        return AutomationElement.RootElement.FindFirst(
            TreeScope.Children,
            new AndCondition(
                new PropertyCondition(AutomationElement.ProcessIdProperty, process.Id),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window)));
    }

    /// <summary>Searches every top-level window of the process (menus and popups included).</summary>
    private static AutomationElement FindInProcess(Process process, string automationId)
    {
        var deadline = Stopwatch.StartNew();
        var owned = new PropertyCondition(AutomationElement.ProcessIdProperty, process.Id);
        var wanted = new PropertyCondition(AutomationElement.AutomationIdProperty, automationId);
        while (deadline.Elapsed < TimeSpan.FromSeconds(10))
        {
            foreach (AutomationElement top in AutomationElement.RootElement.FindAll(TreeScope.Children, owned))
            {
                if (top.FindFirst(TreeScope.Descendants, wanted) is { } match)
                {
                    return match;
                }
            }

            Thread.Sleep(100);
        }

        throw new InvalidOperationException($"'{automationId}' not found in any window of the process.");
    }

    private static AutomationElement Find(AutomationElement root, string automationId, TimeSpan? timeout = null) =>
        TryFind(root, automationId, timeout ?? TimeSpan.FromSeconds(10)) ?? throw new InvalidOperationException($"'{automationId}' not found.");

    private static AutomationElement? TryFind(AutomationElement root, string automationId, TimeSpan timeout)
    {
        var deadline = Stopwatch.StartNew();
        var condition = new PropertyCondition(AutomationElement.AutomationIdProperty, automationId);
        do
        {
            var element = root.FindFirst(TreeScope.Descendants, condition);
            if (element is not null && !element.Current.IsOffscreen)
            {
                return element;
            }

            Thread.Sleep(100);
        }
        while (deadline.Elapsed < timeout);
        return null;
    }

    private static T WaitFor<T>(Func<T?> probe, TimeSpan timeout, string what)
        where T : class
    {
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < timeout)
        {
            if (probe() is { } value)
            {
                return value;
            }

            Thread.Sleep(100);
        }

        throw new TimeoutException($"Timed out waiting for {what}.");
    }

    private static void WaitUntil(Func<bool> condition, TimeSpan timeout, string what)
    {
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < timeout)
        {
            if (condition())
            {
                return;
            }

            Thread.Sleep(100);
        }

        throw new TimeoutException($"Timed out waiting for {what}.");
    }

    private static void Invoke(AutomationElement element)
    {
        WaitUntil(() => element.Current.IsEnabled, TimeSpan.FromSeconds(15), $"{element.Current.AutomationId} enabled");
        ((InvokePattern)element.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
    }

    private static void SetValue(AutomationElement element, string value) =>
        ((ValuePattern)element.GetCurrentPattern(ValuePattern.Pattern)).SetValue(value);

    /// <summary>Captures the real window (PrintWindow, full content) next to the other snapshots.</summary>
    private void Shot(Process process, string name)
    {
        Thread.Sleep(450);
        process.Refresh();
        var hwnd = process.MainWindowHandle;
        if (hwnd == 0)
        {
            return;
        }

        var previous = SetThreadDpiAwarenessContext(-4);
        try
        {
            GetWindowRect(hwnd, out var rect);
            var width = rect.Right - rect.Left;
            var height = rect.Bottom - rect.Top;
            var screen = GetDC(0);
            var dc = CreateCompatibleDC(screen);
            var bitmap = CreateCompatibleBitmap(screen, width, height);
            var old = SelectObject(dc, bitmap);
            PrintWindow(hwnd, dc, 2);
            SelectObject(dc, old);
            var source = Imaging.CreateBitmapSourceFromHBitmap(bitmap, 0, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            DeleteObject(bitmap);
            DeleteDC(dc);
            ReleaseDC(0, screen);

            Directory.CreateDirectory(Ui.SnapshotDirectory);
            var path = Path.Combine(Ui.SnapshotDirectory, name + ".png");
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using var stream = File.Create(path);
            encoder.Save(stream);
            output.WriteLine($"snapshot {path}");
        }
        finally
        {
            SetThreadDpiAwarenessContext(previous);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern nint SetThreadDpiAwarenessContext(nint context);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(nint hwnd, out RECT rect);

    [DllImport("user32.dll")]
    private static extern bool PrintWindow(nint hwnd, nint hdc, uint flags);

    [DllImport("user32.dll")]
    private static extern nint GetDC(nint hwnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(nint hwnd, nint hdc);

    [DllImport("gdi32.dll")]
    private static extern nint CreateCompatibleDC(nint hdc);

    [DllImport("gdi32.dll")]
    private static extern nint CreateCompatibleBitmap(nint hdc, int width, int height);

    [DllImport("gdi32.dll")]
    private static extern nint SelectObject(nint hdc, nint obj);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(nint obj);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(nint hdc);
}
