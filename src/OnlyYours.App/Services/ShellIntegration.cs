using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using OnlyYours.App.Interop;

namespace OnlyYours.App.Services;

/// <summary>
/// A hidden message-only window that receives global hotkeys, tray-icon callbacks and
/// session/power notifications, independently of the main window's state.
/// </summary>
public sealed class MessageWindow : IDisposable
{
    private const int HWND_MESSAGE = -3;
    private readonly HwndSource _source;
    private readonly uint _taskbarCreated;

    public MessageWindow()
    {
        var parameters = new HwndSourceParameters("OnlyYoursMessageWindow")
        {
            ParentWindow = HWND_MESSAGE,
            Width = 0,
            Height = 0,
            WindowStyle = 0,
        };
        _source = new HwndSource(parameters);
        _source.AddHook(Hook);
        _taskbarCreated = Native.RegisterWindowMessage("TaskbarCreated");
        Native.WTSRegisterSessionNotification(Handle, Native.NOTIFY_FOR_THIS_SESSION);
    }

    public nint Handle => _source.Handle;

    public event Action<int>? Hotkey;

    public event Action<int>? TrayMessage;

    public event Action? TaskbarCreated;

    public event Action? SessionLocked;

    public event Action? Suspending;

    private nint Hook(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        switch (message)
        {
            case Native.WM_HOTKEY:
                Hotkey?.Invoke((int)wParam);
                handled = true;
                break;
            case TrayIcon.CallbackMessage:
                TrayMessage?.Invoke((int)(lParam & 0xFFFF));
                handled = true;
                break;
            case Native.WM_WTSSESSION_CHANGE when (int)wParam == Native.WTS_SESSION_LOCK:
                SessionLocked?.Invoke();
                break;
            case Native.WM_POWERBROADCAST when (int)wParam == Native.PBT_APMSUSPEND:
                Suspending?.Invoke();
                break;
            default:
                if (message == _taskbarCreated && _taskbarCreated != 0)
                {
                    TaskbarCreated?.Invoke();
                }

                break;
        }

        return 0;
    }

    public void Dispose()
    {
        Native.WTSUnRegisterSessionNotification(Handle);
        _source.RemoveHook(Hook);
        _source.Dispose();
    }
}

public sealed record HotkeyGesture(ModifierKeys Modifiers, Key Key)
{
    public static HotkeyGesture QuickSearch { get; } = new(ModifierKeys.Control | ModifierKeys.Alt, Key.P);

    public static HotkeyGesture AutoType { get; } = new(ModifierKeys.Control | ModifierKeys.Alt, Key.A);

    public override string ToString()
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(ModifierKeys.Control))
        {
            parts.Add("Ctrl");
        }

        if (Modifiers.HasFlag(ModifierKeys.Alt))
        {
            parts.Add("Alt");
        }

        if (Modifiers.HasFlag(ModifierKeys.Shift))
        {
            parts.Add("Shift");
        }

        if (Modifiers.HasFlag(ModifierKeys.Windows))
        {
            parts.Add("Win");
        }

        parts.Add(Key.ToString());
        return string.Join('+', parts);
    }
}

/// <summary>System-wide shortcuts via RegisterHotKey. A shortcut taken by another app is reported, never fatal.</summary>
public sealed class HotkeyService : IDisposable
{
    private readonly MessageWindow _window;
    private readonly Dictionary<int, Action> _actions = [];
    private int _nextId = 0x5160;

    public HotkeyService(MessageWindow window)
    {
        _window = window;
        _window.Hotkey += id =>
        {
            if (_actions.TryGetValue(id, out var action))
            {
                Application.Current?.Dispatcher.BeginInvoke(action);
            }
        };
    }

    public List<string> Failures { get; } = [];

    public bool Register(HotkeyGesture gesture, Action action)
    {
        uint modifiers = Native.MOD_NOREPEAT;
        if (gesture.Modifiers.HasFlag(ModifierKeys.Control))
        {
            modifiers |= Native.MOD_CONTROL;
        }

        if (gesture.Modifiers.HasFlag(ModifierKeys.Alt))
        {
            modifiers |= Native.MOD_ALT;
        }

        if (gesture.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            modifiers |= Native.MOD_SHIFT;
        }

        if (gesture.Modifiers.HasFlag(ModifierKeys.Windows))
        {
            modifiers |= Native.MOD_WIN;
        }

        var id = _nextId++;
        if (!Native.RegisterHotKey(_window.Handle, id, modifiers, (uint)KeyInterop.VirtualKeyFromKey(gesture.Key)))
        {
            Failures.Add(gesture.ToString());
            return false;
        }

        _actions[id] = action;
        return true;
    }

    public void UnregisterAll()
    {
        foreach (var id in _actions.Keys)
        {
            Native.UnregisterHotKey(_window.Handle, id);
        }

        _actions.Clear();
        Failures.Clear();
    }

    public void Dispose() => UnregisterAll();
}

/// <summary>Notification-area icon (Shell_NotifyIcon) — no WinForms dependency, so start-up stays lean.</summary>
public sealed class TrayIcon : IDisposable
{
    public const int CallbackMessage = Native.WM_USER + 0x51;
    private readonly MessageWindow _window;
    private nint _icon;
    private bool _added;
    private string _tooltip = "OnlyYours";

    public TrayIcon(MessageWindow window)
    {
        _window = window;
        _window.TrayMessage += OnMessage;
        _window.TaskbarCreated += () =>
        {
            if (_added)
            {
                _added = false;
                Show();
            }
        };
    }

    public event Action? Activated;

    public event Action? MenuRequested;

    public bool IsVisible => _added;

    public void Show()
    {
        if (_added)
        {
            return;
        }

        _icon = _icon != 0 ? _icon : LoadIcon();
        var data = Data(Native.NIF_MESSAGE | Native.NIF_ICON | Native.NIF_TIP | Native.NIF_SHOWTIP);
        _added = Native.Shell_NotifyIcon(Native.NIM_ADD, ref data);
        if (_added)
        {
            data.uVersion = Native.NOTIFYICON_VERSION_4;
            Native.Shell_NotifyIcon(Native.NIM_SETVERSION, ref data);
        }
    }

    public void SetTooltip(string text)
    {
        _tooltip = text.Length > 120 ? text[..120] : text;
        if (_added)
        {
            var data = Data(Native.NIF_TIP | Native.NIF_SHOWTIP);
            Native.Shell_NotifyIcon(Native.NIM_MODIFY, ref data);
        }
    }

    public void Hide()
    {
        if (!_added)
        {
            return;
        }

        var data = Data(0);
        Native.Shell_NotifyIcon(Native.NIM_DELETE, ref data);
        _added = false;
    }

    private Native.NOTIFYICONDATA Data(int flags) => new()
    {
        cbSize = (uint)Marshal.SizeOf<Native.NOTIFYICONDATA>(),
        hWnd = _window.Handle,
        uID = 1,
        uFlags = (uint)flags,
        uCallbackMessage = CallbackMessage,
        hIcon = _icon,
        szTip = _tooltip,
        szInfo = string.Empty,
        szInfoTitle = string.Empty,
    };

    private void OnMessage(int message)
    {
        switch (message)
        {
            case Native.WM_LBUTTONUP:
            case Native.NIN_SELECT:
            case Native.NIN_KEYSELECT:
                Activated?.Invoke();
                break;
            case Native.WM_RBUTTONUP:
            case Native.WM_CONTEXTMENU:
                MenuRequested?.Invoke();
                break;
        }
    }

    private static unsafe nint LoadIcon()
    {
        try
        {
            var resource = Application.GetResourceStream(new Uri("pack://application:,,,/OnlyYours;component/Assets/OnlyYours.ico"));
            if (resource is null)
            {
                return 0;
            }

            using var memory = new MemoryStream();
            resource.Stream.CopyTo(memory);
            var bytes = memory.ToArray();

            // Pick the 32×32 PNG entry from the ICO directory.
            var count = BitConverter.ToUInt16(bytes, 4);
            var size = Native.GetSystemMetrics(49) is > 0 and var metric ? metric : 32; // SM_CXSMICON
            for (var i = 0; i < count; i++)
            {
                var entry = 6 + (i * 16);
                var width = bytes[entry] == 0 ? 256 : bytes[entry];
                if (width < size)
                {
                    continue;
                }

                var length = BitConverter.ToInt32(bytes, entry + 8);
                var offset = BitConverter.ToInt32(bytes, entry + 12);
                fixed (byte* data = &bytes[offset])
                {
                    return Native.CreateIconFromResourceEx(data, (uint)length, true, 0x00030000, width, width, 0);
                }
            }
        }
        catch
        {
            // Fall through: no icon is better than no app.
        }

        return 0;
    }

    public void Dispose()
    {
        Hide();
        if (_icon != 0)
        {
            Native.DestroyIcon(_icon);
            _icon = 0;
        }
    }
}
