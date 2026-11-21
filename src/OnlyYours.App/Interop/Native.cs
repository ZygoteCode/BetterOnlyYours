using System.Runtime.InteropServices;

namespace OnlyYours.App.Interop;

/// <summary>Win32 entry points used by the shell integration (clipboard, hotkeys, tray, window chrome, input, session events).</summary>
internal static unsafe partial class Native
{
    // ------------------------------------------------------------- clipboard
    internal const uint CF_UNICODETEXT = 13;
    internal const uint GMEM_MOVEABLE = 0x0002;

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool OpenClipboard(nint owner);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CloseClipboard();

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool EmptyClipboard();

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial nint SetClipboardData(uint format, nint memory);

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial nint GetClipboardData(uint format);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsClipboardFormatAvailable(uint format);

    [LibraryImport("user32.dll", EntryPoint = "RegisterClipboardFormatW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    internal static partial uint RegisterClipboardFormat(string name);

    [LibraryImport("user32.dll")]
    internal static partial uint GetClipboardSequenceNumber();

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial nint GlobalAlloc(uint flags, nuint bytes);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial void* GlobalLock(nint memory);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GlobalUnlock(nint memory);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial nint GlobalFree(nint memory);

    [LibraryImport("kernel32.dll")]
    internal static partial nuint GlobalSize(nint memory);

    // --------------------------------------------------------------- hotkeys
    internal const int WM_HOTKEY = 0x0312;
    internal const uint MOD_ALT = 0x1;
    internal const uint MOD_CONTROL = 0x2;
    internal const uint MOD_SHIFT = 0x4;
    internal const uint MOD_WIN = 0x8;
    internal const uint MOD_NOREPEAT = 0x4000;

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint key);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool UnregisterHotKey(nint hwnd, int id);

    // ------------------------------------------------------------------ input
    internal const uint INPUT_KEYBOARD = 1;
    internal const uint KEYEVENTF_KEYUP = 0x0002;
    internal const uint KEYEVENTF_UNICODE = 0x0004;
    internal const ushort VK_TAB = 0x09;
    internal const ushort VK_RETURN = 0x0D;
    internal const ushort VK_SHIFT = 0x10;
    internal const ushort VK_CONTROL = 0x11;
    internal const ushort VK_MENU = 0x12;
    internal const ushort VK_LWIN = 0x5B;
    internal const ushort VK_RWIN = 0x5C;
    internal const ushort VK_CAPITAL = 0x14;

    [StructLayout(LayoutKind.Sequential)]
    internal struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public nint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public nint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct INPUTUNION
    {
        [FieldOffset(0)]
        public MOUSEINPUT mi;

        [FieldOffset(0)]
        public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct INPUT
    {
        public uint type;
        public INPUTUNION u;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial uint SendInput(uint count, INPUT* inputs, int size);

    [LibraryImport("user32.dll")]
    internal static partial short GetAsyncKeyState(int key);

    [LibraryImport("user32.dll")]
    internal static partial short GetKeyState(int key);

    [StructLayout(LayoutKind.Sequential)]
    internal struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetLastInputInfo(ref LASTINPUTINFO info);

    [LibraryImport("user32.dll")]
    internal static partial nint GetKeyboardLayout(uint thread);

    // ---------------------------------------------------------------- windows
    [LibraryImport("user32.dll")]
    internal static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetForegroundWindow(nint hwnd);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowTextW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial int GetWindowText(nint hwnd, char* buffer, int max);

    [LibraryImport("user32.dll")]
    internal static partial uint GetWindowThreadProcessId(nint hwnd, out uint processId);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsWindow(nint hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsWindowVisible(nint hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ShowWindow(nint hwnd, int command);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetWindowDisplayAffinity(nint hwnd, uint affinity);

    internal const uint WDA_NONE = 0x0;
    internal const uint WDA_EXCLUDEFROMCAPTURE = 0x11;

    [LibraryImport("user32.dll")]
    internal static partial nint MonitorFromWindow(nint hwnd, uint flags);

    [LibraryImport("user32.dll")]
    internal static partial nint MonitorFromPoint(POINT point, uint flags);

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MONITORINFO
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetMonitorInfo(nint monitor, ref MONITORINFO info);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetCursorPos(out POINT point);

    // ------------------------------------------------------------------- dwm
    internal const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    internal const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    internal const int DWMWA_BORDER_COLOR = 34;
    internal const int DWMWCP_ROUND = 2;

    [LibraryImport("dwmapi.dll")]
    internal static partial int DwmSetWindowAttribute(nint hwnd, int attribute, void* value, int size);

    // ------------------------------------------------------- session / power
    internal const int WM_WTSSESSION_CHANGE = 0x02B1;
    internal const int WTS_SESSION_LOCK = 0x7;
    internal const int WTS_SESSION_UNLOCK = 0x8;
    internal const int NOTIFY_FOR_THIS_SESSION = 0;
    internal const int WM_POWERBROADCAST = 0x0218;
    internal const int PBT_APMSUSPEND = 0x4;

    [LibraryImport("wtsapi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool WTSRegisterSessionNotification(nint hwnd, int flags);

    [LibraryImport("wtsapi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool WTSUnRegisterSessionNotification(nint hwnd);

    // --------------------------------------------------------------- tray icon
    internal const int NIM_ADD = 0x0;
    internal const int NIM_MODIFY = 0x1;
    internal const int NIM_DELETE = 0x2;
    internal const int NIM_SETVERSION = 0x4;
    internal const int NIF_MESSAGE = 0x1;
    internal const int NIF_ICON = 0x2;
    internal const int NIF_TIP = 0x4;
    internal const int NIF_SHOWTIP = 0x80;
    internal const int NOTIFYICON_VERSION_4 = 4;
    internal const int WM_USER = 0x0400;
    internal const int WM_LBUTTONUP = 0x0202;
    internal const int WM_RBUTTONUP = 0x0205;
    internal const int WM_CONTEXTMENU = 0x007B;
    internal const int NIN_SELECT = WM_USER + 0;
    internal const int NIN_KEYSELECT = NIN_SELECT | 0x1;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct NOTIFYICONDATA
    {
        public uint cbSize;
        public nint hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public nint hIcon;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;

        public uint dwState;
        public uint dwStateMask;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;

        public uint uVersion;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;

        public uint dwInfoFlags;
        public Guid guidItem;
        public nint hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool Shell_NotifyIcon(int message, ref NOTIFYICONDATA data);

    [LibraryImport("user32.dll", EntryPoint = "LoadImageW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    internal static partial nint LoadImage(nint instance, string name, uint type, int cx, int cy, uint flags);

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial nint CreateIconFromResourceEx(byte* data, uint size, [MarshalAs(UnmanagedType.Bool)] bool icon, uint version, int cx, int cy, uint flags);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DestroyIcon(nint icon);

    [LibraryImport("user32.dll")]
    internal static partial int GetSystemMetrics(int index);

    [LibraryImport("user32.dll", EntryPoint = "RegisterWindowMessageW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial uint RegisterWindowMessage(string name);

    // -------------------------------------------------------- window chrome
    internal const int WM_NCHITTEST = 0x0084;
    internal const int WM_NCLBUTTONDOWN = 0x00A1;
    internal const int WM_NCLBUTTONUP = 0x00A2;
    internal const int WM_NCMOUSEMOVE = 0x00A0;
    internal const int WM_NCMOUSELEAVE = 0x02A2;
    internal const int WM_MOUSELEAVE = 0x02A3;
    internal const int WM_SYSCOMMAND = 0x0112;
    internal const int HTMAXBUTTON = 9;
    internal const int HTCLIENT = 1;

    [LibraryImport("user32.dll")]
    internal static partial uint GetCaretBlinkTime();
}
