using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;
using OnlyYours.App.Interop;

namespace OnlyYours.App.Services;

/// <summary>Start-up timing marks (enabled with <c>ONLYYOURS_TRACE_STARTUP=1</c>) used by the performance tests.</summary>
public static class StartupTrace
{
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static readonly List<(string Mark, double Ms)> Marks = [];

    public static bool Enabled { get; } = Environment.GetEnvironmentVariable("ONLYYOURS_TRACE_STARTUP") == "1";

    public static void Mark(string name)
    {
        lock (Marks)
        {
            Marks.Add((name, Clock.Elapsed.TotalMilliseconds));
        }

        if (Enabled)
        {
            Flush();
        }
    }

    public static double Elapsed => Clock.Elapsed.TotalMilliseconds;

    private static void Flush()
    {
        try
        {
            var path = Environment.GetEnvironmentVariable("ONLYYOURS_TRACE_FILE") ?? System.IO.Path.Combine(System.IO.Path.GetTempPath(), "onlyyours-startup.log");
            lock (Marks)
            {
                // Machine-readable: always a dot decimal separator, whatever the UI culture.
                System.IO.File.WriteAllLines(path, Marks.Select(m => m.Mark + "\t" + m.Ms.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)));
            }
        }
        catch
        {
            // Diagnostics only.
        }
    }
}

/// <summary>
/// One OnlyYours per user session. A second launch signals the first one (which comes to
/// the front) and exits immediately — no second window, no second key in memory.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activate;
    private RegisteredWaitHandle? _wait;

    private SingleInstance(Mutex mutex, EventWaitHandle activate, bool primary)
    {
        _mutex = mutex;
        _activate = activate;
        IsPrimary = primary;
    }

    public bool IsPrimary { get; }

    public static SingleInstance Acquire()
    {
        var scope = Environment.GetEnvironmentVariable("ONLYYOURS_INSTANCE") ?? WindowsIdentity.GetCurrent().User?.Value ?? "default";
        var name = $"Local\\OnlyYours-{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(scope)))[..16]}";
        var mutex = new Mutex(initiallyOwned: true, name + "-mutex", out var created);
        var activate = new EventWaitHandle(false, EventResetMode.AutoReset, name + "-activate");
        return new SingleInstance(mutex, activate, created);
    }

    public void SignalPrimary() => _activate.Set();

    public void ListenForActivation(Action onActivate)
    {
        _wait = ThreadPool.RegisterWaitForSingleObject(_activate, (_, _) => onActivate(), null, Timeout.Infinite, executeOnlyOnce: false);
    }

    public void Dispose()
    {
        _wait?.Unregister(null);
        if (IsPrimary)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Not owned by this thread any more.
            }
        }

        _mutex.Dispose();
        _activate.Dispose();
    }
}

/// <summary>
/// Process-level defences, applied as early as possible:
/// <list type="bullet">
/// <item>DLL search order restricted to the application folder and System32 (no CWD/PATH planting);</item>
/// <item>mitigation policies: no remote or low-integrity images, System32 images preferred, no legacy extension points (blocks AppInit DLLs and global-hook injection);</item>
/// <item>a process DACL that denies other unprivileged processes of the same user the right to read or write this process's memory.</item>
/// </list>
/// </summary>
public static partial class ProcessHardening
{
    private const uint LOAD_LIBRARY_SEARCH_APPLICATION_DIR = 0x200;
    private const uint LOAD_LIBRARY_SEARCH_SYSTEM32 = 0x800;
    private const uint LOAD_LIBRARY_SEARCH_USER_DIRS = 0x400;

    public static bool DllSearchRestricted { get; private set; }

    public static bool MitigationsApplied { get; private set; }

    public static bool MemoryAccessDenied { get; private set; }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetDefaultDllDirectories(uint flags);

    [LibraryImport("kernel32.dll", EntryPoint = "SetDllDirectoryW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetDllDirectory(string? path);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool SetProcessMitigationPolicy(int policy, void* buffer, nuint length);

    [LibraryImport("advapi32.dll", EntryPoint = "ConvertStringSecurityDescriptorToSecurityDescriptorW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ConvertStringSecurityDescriptorToSecurityDescriptor(string descriptor, uint revision, out nint securityDescriptor, out uint size);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetSecurityDescriptorDacl(nint descriptor, [MarshalAs(UnmanagedType.Bool)] out bool present, out nint dacl, [MarshalAs(UnmanagedType.Bool)] out bool defaulted);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    private static partial uint SetSecurityInfo(nint handle, int objectType, uint securityInfo, nint owner, nint group, nint dacl, nint sacl);

    [LibraryImport("kernel32.dll")]
    private static partial nint LocalFree(nint memory);

    public static void ApplyEarly()
    {
        try
        {
            SetDllDirectory(string.Empty);
            DllSearchRestricted = SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_APPLICATION_DIR | LOAD_LIBRARY_SEARCH_SYSTEM32 | LOAD_LIBRARY_SEARCH_USER_DIRS);
        }
        catch
        {
            DllSearchRestricted = false;
        }

        MitigationsApplied = ApplyMitigations();
    }

    private static unsafe bool ApplyMitigations()
    {
        try
        {
            // ProcessImageLoadPolicy (10): NoRemoteImages | NoLowMandatoryLabelImages | PreferSystem32Images.
            uint imageLoad = 0b111;
            var ok = SetProcessMitigationPolicy(10, &imageLoad, sizeof(uint));

            // ProcessExtensionPointDisablePolicy (6): DisableExtensionPoints (AppInit DLLs, global hooks, legacy IMEs, LSPs).
            uint extensionPoints = 0b1;
            ok &= SetProcessMitigationPolicy(6, &extensionPoints, sizeof(uint));
            return ok;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Replaces the process DACL: SYSTEM and administrators keep full access, the owner
    /// keeps only what is needed to see and close the process — reading or writing its
    /// memory, injecting threads or duplicating handles is denied to other processes.
    /// </summary>
    public static void RestrictProcessAccess()
    {
        const uint PROCESS_TERMINATE = 0x0001;
        const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        const uint SYNCHRONIZE = 0x100000;
        const uint READ_CONTROL = 0x20000;
        var ownerRights = PROCESS_TERMINATE | PROCESS_QUERY_LIMITED_INFORMATION | SYNCHRONIZE | READ_CONTROL;

        var user = WindowsIdentity.GetCurrent().User?.Value;
        if (user is null)
        {
            return;
        }

        var sddl = $"D:P(A;;GA;;;SY)(A;;GA;;;BA)(A;;0x{ownerRights:X};;;{user})";
        if (!ConvertStringSecurityDescriptorToSecurityDescriptor(sddl, 1, out var descriptor, out _))
        {
            return;
        }

        try
        {
            if (GetSecurityDescriptorDacl(descriptor, out var present, out var dacl, out _) && present)
            {
                // SE_KERNEL_OBJECT = 6, DACL_SECURITY_INFORMATION = 4, PROTECTED_DACL_SECURITY_INFORMATION = 0x80000000
                MemoryAccessDenied = SetSecurityInfo(Process.GetCurrentProcess().Handle, 6, 0x80000004, 0, 0, dacl, 0) == 0;
            }
        }
        finally
        {
            LocalFree(descriptor);
        }
    }
}

/// <summary>Keeps the window out of screenshots, screen recordings and screen sharing (WDA_EXCLUDEFROMCAPTURE).</summary>
public static class ScreenCaptureGuard
{
    public static bool Apply(Window window, bool enabled)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == 0)
        {
            return false;
        }

        return Native.SetWindowDisplayAffinity(handle, enabled ? Native.WDA_EXCLUDEFROMCAPTURE : Native.WDA_NONE);
    }
}

/// <summary>"Start with Windows": a per-user Run key — no admin rights, no scheduled task.</summary>
public static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "OnlyYours";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is string;
    }

    public static void Set(bool enabled, bool minimized)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true) ?? Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
        {
            var exe = Environment.ProcessPath ?? System.IO.Path.Combine(AppContext.BaseDirectory, "OnlyYours.exe");
            key.SetValue(ValueName, minimized ? $"\"{exe}\" --minimized" : $"\"{exe}\"");
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}

/// <summary>Seconds since the last keyboard or mouse input anywhere in the session.</summary>
public static class IdleClock
{
    public static TimeSpan SystemIdle
    {
        get
        {
            var info = new Native.LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<Native.LASTINPUTINFO>() };
            if (!Native.GetLastInputInfo(ref info))
            {
                return TimeSpan.Zero;
            }

            var idle = unchecked((uint)Environment.TickCount - info.dwTime);
            return TimeSpan.FromMilliseconds(idle);
        }
    }
}
