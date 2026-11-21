using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace OnlyYours.App.Infrastructure;

/// <summary>Minimal INotifyPropertyChanged base — no third-party MVVM framework needed.</summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(name);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>Refreshes every binding on this object (used after a language switch).</summary>
    protected void OnAllPropertiesChanged() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
}

public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute is null ? null : _ => canExecute())
    {
    }

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => _execute(parameter);
}

/// <summary>Async command that cannot re-enter while running and exposes <see cref="IsRunning"/>.</summary>
public sealed class AsyncCommand : ObservableObject, ICommand
{
    private readonly Func<object?, Task> _execute;
    private readonly Func<object?, bool>? _canExecute;
    private bool _isRunning;

    public AsyncCommand(Func<Task> execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute is null ? null : _ => canExecute())
    {
    }

    public AsyncCommand(Func<object?, Task> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set => Set(ref _isRunning, value);
    }

    public bool CanExecute(object? parameter) => !_isRunning && (_canExecute?.Invoke(parameter) ?? true);

    public async void Execute(object? parameter)
    {
        await ExecuteAsync(parameter);
    }

    public async Task ExecuteAsync(object? parameter = null)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        IsRunning = true;
        CommandManager.InvalidateRequerySuggested();
        try
        {
            await _execute(parameter);
        }
        catch (Exception error)
        {
            AppErrors.Report(error);
        }
        finally
        {
            IsRunning = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }
}

/// <summary>ObservableCollection that can swap its whole content with a single Reset notification.</summary>
public sealed class BulkObservableCollection<T> : ObservableCollection<T>
{
    public void ReplaceAll(IEnumerable<T> items)
    {
        Items.Clear();
        foreach (var item in items)
        {
            Items.Add(item);
        }

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
/// <summary>
/// Last-resort error sink: never shows secrets, never crashes the UI for a background failure.
/// With <c>ONLYYOURS_ERROR_LOG</c> set (diagnostics and automated tests only) exceptions and WPF
/// binding failures are also appended to that file.
/// </summary>
public static class AppErrors
{
    private static readonly string? LogPath = Environment.GetEnvironmentVariable("ONLYYOURS_ERROR_LOG");
    private static readonly Lock Gate = new();

    public static event Action<Exception>? Reported;

    public static bool Logging => LogPath is not null;

    public static void Report(Exception error)
    {
        System.Diagnostics.Debug.WriteLine(error);
        Log("exception", error.ToString());
        Reported?.Invoke(error);
    }

    public static void Log(string category, string text)
    {
        if (LogPath is null)
        {
            return;
        }

        try
        {
            lock (Gate)
            {
                System.IO.File.AppendAllText(LogPath, $"[{category}] {text}{Environment.NewLine}");
            }
        }
        catch
        {
            // Diagnostics must never break the app.
        }
    }

    /// <summary>Routes WPF binding and resource warnings to the diagnostics log.</summary>
    public static void EnableDiagnostics()
    {
        if (LogPath is null)
        {
            return;
        }

        System.Diagnostics.PresentationTraceSources.Refresh();
        var listener = new LogListener();
        System.Diagnostics.PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
        System.Diagnostics.PresentationTraceSources.DataBindingSource.Switch.Level = System.Diagnostics.SourceLevels.Warning;
        System.Diagnostics.PresentationTraceSources.ResourceDictionarySource.Listeners.Add(listener);
        System.Diagnostics.PresentationTraceSources.ResourceDictionarySource.Switch.Level = System.Diagnostics.SourceLevels.Warning;
    }

    private sealed class LogListener : System.Diagnostics.TraceListener
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

            Log("wpf", text);
        }
    }
}
