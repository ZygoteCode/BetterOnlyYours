using System.Runtime.InteropServices;
using System.Windows.Threading;
using OnlyYours.App.Infrastructure;
using OnlyYours.App.Interop;
using OnlyYours.Core.Security.Memory;

namespace OnlyYours.App.Services;

/// <summary>
/// Secure clipboard. Secrets are copied straight from locked memory with the Win32 API —
/// never through a managed string — and tagged so that Windows Clipboard History
/// (Win+V), cloud clipboard sync and clipboard monitors ignore them. After the
/// configured delay the clipboard is wiped, but only if it still holds what OnlyYours put
/// there (tracked by the clipboard sequence number), and the memory block is zeroed
/// before it is released.
/// </summary>
public sealed class ClipboardService : ObservableObject
{
    private static uint _excludeMonitor;
    private static uint _historyFormat;
    private static uint _cloudFormat;
    private static uint _viewerIgnore;

    private readonly DispatcherTimer _timer = new(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(250) };
    private uint _ourSequence;
    private DateTimeOffset _clearAt;
    private int _totalSeconds;
    private string? _label;
    private bool _pending;

    public ClipboardService()
    {
        _timer.Tick += (_, _) => Tick();
    }

    /// <summary>Window that owns clipboard operations (required by SetClipboardData).</summary>
    public nint OwnerHandle { get; set; }

    public Func<int> ClearAfterSeconds { get; set; } = () => 30;

    public Func<bool> ExcludeFromHistory { get; set; } = () => true;

    public event EventHandler<string>? Copied;

    public string? Label
    {
        get => _label;
        private set => Set(ref _label, value);
    }

    public bool IsPending
    {
        get => _pending;
        private set => Set(ref _pending, value);
    }

    public int SecondsRemaining => _pending ? Math.Max(0, (int)Math.Ceiling((_clearAt - DateTimeOffset.Now).TotalSeconds)) : 0;

    public double Progress => _pending && _totalSeconds > 0 ? Math.Clamp((_clearAt - DateTimeOffset.Now).TotalSeconds / _totalSeconds, 0, 1) : 0;

    public bool CopySecret(ReadOnlySpan<char> text, string label) => Copy(text, label, sensitive: true);

    public bool CopyText(string text, string label) => Copy(text, label, sensitive: false);

    private unsafe bool Copy(ReadOnlySpan<char> text, string label, bool sensitive)
    {
        EnsureFormats();
        if (!Open())
        {
            return false;
        }

        try
        {
            Native.EmptyClipboard();
            var bytes = (nuint)((text.Length + 1) * sizeof(char));
            var memory = Native.GlobalAlloc(Native.GMEM_MOVEABLE, bytes);
            if (memory == 0)
            {
                return false;
            }

            var target = (char*)Native.GlobalLock(memory);
            if (target is null)
            {
                Native.GlobalFree(memory);
                return false;
            }

            text.CopyTo(new Span<char>(target, text.Length));
            target[text.Length] = '\0';
            Native.GlobalUnlock(memory);
            if (Native.SetClipboardData(Native.CF_UNICODETEXT, memory) == 0)
            {
                Native.GlobalFree(memory);
                return false;
            }

            if (sensitive && ExcludeFromHistory())
            {
                SetDword(_excludeMonitor, 0);
                SetDword(_historyFormat, 0);
                SetDword(_cloudFormat, 0);
                SetDword(_viewerIgnore, 0);
            }
        }
        finally
        {
            Native.CloseClipboard();
        }

        _ourSequence = Native.GetClipboardSequenceNumber();
        Label = label;
        var seconds = sensitive ? ClearAfterSeconds() : 0;
        if (seconds > 0)
        {
            _totalSeconds = seconds;
            _clearAt = DateTimeOffset.Now.AddSeconds(seconds);
            IsPending = true;
            _timer.Start();
        }
        else
        {
            IsPending = false;
            _timer.Stop();
        }

        OnPropertyChanged(nameof(SecondsRemaining));
        OnPropertyChanged(nameof(Progress));
        Copied?.Invoke(this, label);
        return true;
    }

    /// <summary>Reads clipboard text into secure memory (used by password fields' paste).</summary>
    public unsafe bool TryReadText(SecureCharBuffer output)
    {
        if (!Native.IsClipboardFormatAvailable(Native.CF_UNICODETEXT) || !Open())
        {
            return false;
        }

        try
        {
            var handle = Native.GetClipboardData(Native.CF_UNICODETEXT);
            if (handle == 0)
            {
                return false;
            }

            var source = (char*)Native.GlobalLock(handle);
            if (source is null)
            {
                return false;
            }

            try
            {
                var max = (int)(Native.GlobalSize(handle) / sizeof(char));
                var length = 0;
                while (length < max && source[length] != '\0')
                {
                    length++;
                }

                output.Append(new ReadOnlySpan<char>(source, length));
                return true;
            }
            finally
            {
                Native.GlobalUnlock(handle);
            }
        }
        finally
        {
            Native.CloseClipboard();
        }
    }

    /// <summary>Clears the clipboard if it still holds what OnlyYours copied (zeroing the block first).</summary>
    public unsafe void ClearIfOurs()
    {
        _timer.Stop();
        var wasPending = IsPending;
        IsPending = false;
        if (_ourSequence == 0 || Native.GetClipboardSequenceNumber() != _ourSequence)
        {
            _ourSequence = 0;
            return;
        }

        if (Open())
        {
            try
            {
                var handle = Native.GetClipboardData(Native.CF_UNICODETEXT);
                if (handle != 0)
                {
                    var data = (byte*)Native.GlobalLock(handle);
                    if (data is not null)
                    {
                        new Span<byte>(data, (int)Native.GlobalSize(handle)).Clear();
                        Native.GlobalUnlock(handle);
                    }
                }

                Native.EmptyClipboard();
            }
            finally
            {
                Native.CloseClipboard();
            }
        }

        _ourSequence = 0;
        if (wasPending)
        {
            Label = null;
        }
    }

    /// <summary>Stops the countdown and leaves the clipboard as it is.</summary>
    public void Keep()
    {
        _timer.Stop();
        IsPending = false;
        _ourSequence = 0;
        OnPropertyChanged(nameof(SecondsRemaining));
    }

    private void Tick()
    {
        if (!_pending)
        {
            _timer.Stop();
            return;
        }

        if (Native.GetClipboardSequenceNumber() != _ourSequence)
        {
            // The user copied something else: never destroy it.
            _timer.Stop();
            IsPending = false;
            _ourSequence = 0;
            return;
        }

        if (DateTimeOffset.Now >= _clearAt)
        {
            ClearIfOurs();
        }

        OnPropertyChanged(nameof(SecondsRemaining));
        OnPropertyChanged(nameof(Progress));
    }

    private bool Open()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            if (Native.OpenClipboard(OwnerHandle))
            {
                return true;
            }

            Thread.Sleep(10);
        }

        return false;
    }

    private static unsafe void SetDword(uint format, uint value)
    {
        if (format == 0)
        {
            return;
        }

        var memory = Native.GlobalAlloc(Native.GMEM_MOVEABLE, 4);
        if (memory == 0)
        {
            return;
        }

        var pointer = (uint*)Native.GlobalLock(memory);
        if (pointer is not null)
        {
            *pointer = value;
            Native.GlobalUnlock(memory);
        }

        if (Native.SetClipboardData(format, memory) == 0)
        {
            Native.GlobalFree(memory);
        }
    }

    private static void EnsureFormats()
    {
        if (_historyFormat != 0)
        {
            return;
        }

        _excludeMonitor = Native.RegisterClipboardFormat("ExcludeClipboardContentFromMonitorProcessing");
        _historyFormat = Native.RegisterClipboardFormat("CanIncludeInClipboardHistory");
        _cloudFormat = Native.RegisterClipboardFormat("CanUploadToCloudClipboard");
        _viewerIgnore = Native.RegisterClipboardFormat("Clipboard Viewer Ignore");
    }
}
