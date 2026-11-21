using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using OnlyYours.App.Interop;
using OnlyYours.Core.Security.Memory;
using OnlyYours.Core.Vault;
using Windows.Security.Credentials;
using Windows.Security.Cryptography;

namespace OnlyYours.App.Services;

/// <summary>
/// Locks the vault on inactivity (system-wide idle time, not just inside the app),
/// when Windows is locked, before sleep/hibernation and — optionally — on minimise.
/// </summary>
public sealed class AutoLockService : IDisposable
{
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
    private bool _systemEventsHooked;

    public AutoLockService()
    {
        _timer.Tick += (_, _) => Tick();
    }

    public Func<int> IdleMinutes { get; set; } = () => 10;

    public Func<bool> OnWindowsLock { get; set; } = () => true;

    public Func<bool> OnSleep { get; set; } = () => true;

    public bool IsArmed { get; private set; }

    /// <summary>Raised on the UI thread with the localization key of the reason.</summary>
    public event Action<string>? LockRequested;

    public event Action? Ticked;

    public TimeSpan? Remaining
    {
        get
        {
            var minutes = IdleMinutes();
            if (!IsArmed || minutes <= 0)
            {
                return null;
            }

            var left = TimeSpan.FromMinutes(minutes) - IdleClock.SystemIdle;
            return left < TimeSpan.Zero ? TimeSpan.Zero : left;
        }
    }

    public void Arm()
    {
        IsArmed = true;
        HookSystemEvents();
        _timer.Start();
    }

    public void Disarm()
    {
        IsArmed = false;
        _timer.Stop();
    }

    private void Tick()
    {
        if (!IsArmed)
        {
            return;
        }

        Ticked?.Invoke();
        var minutes = IdleMinutes();
        if (minutes > 0 && IdleClock.SystemIdle >= TimeSpan.FromMinutes(minutes))
        {
            Request("Lock.Reason.Idle");
        }
    }

    private void HookSystemEvents()
    {
        if (_systemEventsHooked)
        {
            return;
        }

        _systemEventsHooked = true;
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason is SessionSwitchReason.SessionLock or SessionSwitchReason.ConsoleDisconnect or SessionSwitchReason.RemoteDisconnect && OnWindowsLock())
        {
            Request("Lock.Reason.Session");
        }
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Suspend && OnSleep())
        {
            Request("Lock.Reason.Sleep");
        }
    }

    public void Request(string reason)
    {
        if (!IsArmed)
        {
            return;
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            LockRequested?.Invoke(reason);
        }
        else
        {
            // Sleep and session lock must not wait: lock synchronously on the UI thread.
            dispatcher.Invoke(() => LockRequested?.Invoke(reason), DispatcherPriority.Send);
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        if (_systemEventsHooked)
        {
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        }
    }
}

/// <summary>One step of an auto-type sequence.</summary>
public readonly record struct AutoTypeToken(AutoTypeTokenKind Kind, string? Text = null, int Delay = 0);

public enum AutoTypeTokenKind
{
    Literal,
    Username,
    Password,
    Totp,
    Url,
    Title,
    Tab,
    Enter,
    Delay,
}

/// <summary>
/// Types credentials into another application with SendInput (Unicode keystrokes,
/// layout-independent). Secrets are fetched into secure memory at the last moment
/// and wiped token by token. Typing aborts if the target window loses focus.
/// </summary>
public sealed partial class AutoTypeService : IDisposable
{
    public const string DefaultSequence = "{USERNAME}{TAB}{PASSWORD}{ENTER}";
    private const uint EVENT_SYSTEM_FOREGROUND = 3;
    private const uint WINEVENT_OUTOFCONTEXT = 0;
    private readonly WinEventProc _callback;
    private nint _hook;
    private readonly uint _ownProcess = (uint)Environment.ProcessId;

    private delegate void WinEventProc(nint hook, uint eventType, nint hwnd, int idObject, int idChild, uint thread, uint time);

    [DllImport("user32.dll")]
    private static extern nint SetWinEventHook(uint eventMin, uint eventMax, nint module, WinEventProc callback, uint process, uint thread, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWinEvent(nint hook);

    public AutoTypeService()
    {
        _callback = OnForegroundChanged;
    }

    /// <summary>The last window outside OnlyYours that had the focus — where auto-type goes.</summary>
    public nint LastExternalWindow { get; private set; }

    public void Start()
    {
        if (_hook == 0)
        {
            _hook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, 0, _callback, 0, 0, WINEVENT_OUTOFCONTEXT);
        }
    }

    private void OnForegroundChanged(nint hook, uint eventType, nint hwnd, int idObject, int idChild, uint thread, uint time)
    {
        if (hwnd == 0)
        {
            return;
        }

        Native.GetWindowThreadProcessId(hwnd, out var process);
        if (process != _ownProcess)
        {
            LastExternalWindow = hwnd;
        }
    }

    public static unsafe string WindowTitle(nint hwnd)
    {
        Span<char> buffer = stackalloc char[512];
        fixed (char* pointer = buffer)
        {
            var length = Native.GetWindowText(hwnd, pointer, buffer.Length);
            return length > 0 ? new string(buffer[..length]) : string.Empty;
        }
    }

    public static IReadOnlyList<AutoTypeToken> Parse(string? sequence)
    {
        var text = string.IsNullOrWhiteSpace(sequence) ? DefaultSequence : sequence;
        var tokens = new List<AutoTypeToken>();
        var literal = new System.Text.StringBuilder();
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '{')
            {
                var end = text.IndexOf('}', i + 1);
                if (end > i)
                {
                    var name = text[(i + 1)..end].Trim().ToUpperInvariant();
                    AutoTypeToken? token = name switch
                    {
                        "USERNAME" or "USER" => new AutoTypeToken(AutoTypeTokenKind.Username),
                        "PASSWORD" or "PASS" => new AutoTypeToken(AutoTypeTokenKind.Password),
                        "TOTP" or "OTP" => new AutoTypeToken(AutoTypeTokenKind.Totp),
                        "URL" => new AutoTypeToken(AutoTypeTokenKind.Url),
                        "TITLE" => new AutoTypeToken(AutoTypeTokenKind.Title),
                        "TAB" => new AutoTypeToken(AutoTypeTokenKind.Tab),
                        "ENTER" or "RETURN" => new AutoTypeToken(AutoTypeTokenKind.Enter),
                        "SPACE" => new AutoTypeToken(AutoTypeTokenKind.Literal, " "),
                        _ when name.StartsWith("DELAY", StringComparison.Ordinal) && int.TryParse(name.AsSpan(name.IndexOfAny(['=', ' ']) + 1), out var ms) => new AutoTypeToken(AutoTypeTokenKind.Delay, Delay: Math.Clamp(ms, 0, 10000)),
                        _ => null,
                    };
                    if (token is { } parsed)
                    {
                        if (literal.Length > 0)
                        {
                            tokens.Add(new AutoTypeToken(AutoTypeTokenKind.Literal, literal.ToString()));
                            literal.Clear();
                        }

                        tokens.Add(parsed);
                        i = end;
                        continue;
                    }
                }
            }

            literal.Append(text[i]);
        }

        if (literal.Length > 0)
        {
            tokens.Add(new AutoTypeToken(AutoTypeTokenKind.Literal, literal.ToString()));
        }

        return tokens;
    }

    /// <summary>Types <paramref name="tokens"/> into <paramref name="target"/>. <paramref name="resolve"/> supplies secrets.</summary>
    public async Task<bool> TypeAsync(nint target, IReadOnlyList<AutoTypeToken> tokens, Func<AutoTypeTokenKind, SecureCharBuffer?> resolve, int keyDelayMs)
    {
        if (target == 0 || !Native.IsWindow(target))
        {
            return false;
        }

        Native.SetForegroundWindow(target);
        await Task.Delay(120);
        await WaitForModifiersReleased();
        foreach (var token in tokens)
        {
            if (Native.GetForegroundWindow() != target)
            {
                return false;
            }

            switch (token.Kind)
            {
                case AutoTypeTokenKind.Literal:
                    await TypeChars(token.Text.AsMemory(), keyDelayMs);
                    break;
                case AutoTypeTokenKind.Tab:
                    SendVirtualKey(Native.VK_TAB);
                    break;
                case AutoTypeTokenKind.Enter:
                    SendVirtualKey(Native.VK_RETURN);
                    break;
                case AutoTypeTokenKind.Delay:
                    await Task.Delay(token.Delay);
                    break;
                default:
                    using (var secret = resolve(token.Kind))
                    {
                        if (secret is not null)
                        {
                            await TypeSecure(secret, keyDelayMs);
                        }
                    }

                    break;
            }

            await Task.Delay(Math.Max(10, keyDelayMs));
        }

        return true;
    }

    private static async Task WaitForModifiersReleased()
    {
        for (var i = 0; i < 60; i++)
        {
            var down = (Native.GetAsyncKeyState(Native.VK_CONTROL) & 0x8000) != 0
                || (Native.GetAsyncKeyState(Native.VK_MENU) & 0x8000) != 0
                || (Native.GetAsyncKeyState(Native.VK_SHIFT) & 0x8000) != 0
                || (Native.GetAsyncKeyState(Native.VK_LWIN) & 0x8000) != 0
                || (Native.GetAsyncKeyState(Native.VK_RWIN) & 0x8000) != 0;
            if (!down)
            {
                return;
            }

            await Task.Delay(25);
        }
    }

    private static async Task TypeChars(ReadOnlyMemory<char> text, int delay)
    {
        for (var i = 0; i < text.Length; i++)
        {
            SendUnicode(text.Span[i]);
            if (delay > 0)
            {
                await Task.Delay(delay);
            }
        }
    }

    private static async Task TypeSecure(SecureCharBuffer secret, int delay)
    {
        for (var i = 0; i < secret.Length; i++)
        {
            SendUnicode(secret.Span[i]);
            if (delay > 0)
            {
                await Task.Delay(delay);
            }
        }
    }

    private static unsafe void SendUnicode(char c)
    {
        var inputs = stackalloc Native.INPUT[2];
        inputs[0] = new Native.INPUT { type = Native.INPUT_KEYBOARD, u = new Native.INPUTUNION { ki = new Native.KEYBDINPUT { wScan = c, dwFlags = Native.KEYEVENTF_UNICODE } } };
        inputs[1] = new Native.INPUT { type = Native.INPUT_KEYBOARD, u = new Native.INPUTUNION { ki = new Native.KEYBDINPUT { wScan = c, dwFlags = Native.KEYEVENTF_UNICODE | Native.KEYEVENTF_KEYUP } } };
        Native.SendInput(2, inputs, sizeof(Native.INPUT));
        inputs[0] = default;
        inputs[1] = default;
    }

    private static unsafe void SendVirtualKey(ushort key)
    {
        var inputs = stackalloc Native.INPUT[2];
        inputs[0] = new Native.INPUT { type = Native.INPUT_KEYBOARD, u = new Native.INPUTUNION { ki = new Native.KEYBDINPUT { wVk = key } } };
        inputs[1] = new Native.INPUT { type = Native.INPUT_KEYBOARD, u = new Native.INPUTUNION { ki = new Native.KEYBDINPUT { wVk = key, dwFlags = Native.KEYEVENTF_KEYUP } } };
        Native.SendInput(2, inputs, sizeof(Native.INPUT));
    }

    public void Dispose()
    {
        if (_hook != 0)
        {
            UnhookWinEvent(_hook);
            _hook = 0;
        }
    }
}

/// <summary>
/// Windows Hello quick unlock. A TPM-backed key (KeyCredentialManager) signs a random
/// challenge stored in the vault's Hello slot; the deterministic RSA signature, which
/// can only be produced after Hello verification on this device, derives the key that
/// unwraps the VMK. Nothing reusable is stored outside the vault.
/// </summary>
public sealed partial class WindowsHelloService : IHelloProvider
{
    [LibraryImport("user32.dll", EntryPoint = "FindWindowW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint FindWindow(string? className, string? windowName);

    private bool? _available;

    public async Task<bool> IsAvailableAsync()
    {
        if (_available is { } known)
        {
            return known;
        }

        try
        {
            _available = await KeyCredentialManager.IsSupportedAsync();
        }
        catch
        {
            _available = false;
        }

        return _available.Value;
    }

    public async Task<byte[]?> SignAsync(string credentialName, byte[] challenge, bool createIfMissing, CancellationToken cancellationToken)
    {
        using var focus = BringPromptForward();
        var retrieval = await KeyCredentialManager.OpenAsync(credentialName);
        if (retrieval.Status == KeyCredentialStatus.NotFound && createIfMissing)
        {
            retrieval = await KeyCredentialManager.RequestCreateAsync(credentialName, KeyCredentialCreationOption.ReplaceExisting);
        }

        if (retrieval.Status == KeyCredentialStatus.UserCanceled)
        {
            return null;
        }

        if (retrieval.Status != KeyCredentialStatus.Success)
        {
            throw new VaultException(VaultError.HelloFailed, $"Windows Hello is not available ({retrieval.Status}).");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var result = await retrieval.Credential.RequestSignAsync(CryptographicBuffer.CreateFromByteArray(challenge));
        if (result.Status == KeyCredentialStatus.UserCanceled)
        {
            return null;
        }

        if (result.Status != KeyCredentialStatus.Success)
        {
            throw new VaultException(VaultError.HelloFailed, $"Windows Hello could not sign ({result.Status}).");
        }

        CryptographicBuffer.CopyToByteArray(result.Result, out var signature);
        return signature;
    }

    public async Task DeleteAsync(string credentialName)
    {
        try
        {
            await KeyCredentialManager.DeleteAsync(credentialName);
        }
        catch
        {
            // Already gone.
        }
    }

    /// <summary>The Hello prompt of a desktop app sometimes opens behind it; nudge it to the front.</summary>
    private static IDisposable BringPromptForward()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        var ticks = 0;
        timer.Tick += (_, _) =>
        {
            var prompt = FindWindow("Credential Dialog Xaml Host", null);
            if (prompt != 0)
            {
                Native.SetForegroundWindow(prompt);
                timer.Stop();
            }
            else if (++ticks > 40)
            {
                timer.Stop();
            }
        };
        timer.Start();
        return new Stopper(timer);
    }

    private sealed class Stopper(DispatcherTimer timer) : IDisposable
    {
        public void Dispose() => timer.Stop();
    }
}
