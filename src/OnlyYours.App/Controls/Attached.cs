using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace OnlyYours.App.Controls;

/// <summary>Attached presentation hints used by the control templates (placeholder text, leading icon).</summary>
public static class Hint
{
    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.RegisterAttached(
        "Placeholder", typeof(string), typeof(Hint), new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.Inherits));

    public static readonly DependencyProperty IconProperty = DependencyProperty.RegisterAttached(
        "Icon", typeof(Geometry), typeof(Hint), new PropertyMetadata(null));

    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.RegisterAttached(
        "IsActive", typeof(bool), typeof(Hint), new PropertyMetadata(false));

    public static readonly DependencyProperty ToneProperty = DependencyProperty.RegisterAttached(
        "Tone", typeof(string), typeof(Hint), new PropertyMetadata(null));

    public static string GetPlaceholder(DependencyObject d) => (string)d.GetValue(PlaceholderProperty);

    public static void SetPlaceholder(DependencyObject d, string value) => d.SetValue(PlaceholderProperty, value);

    public static Geometry? GetIcon(DependencyObject d) => (Geometry?)d.GetValue(IconProperty);

    public static void SetIcon(DependencyObject d, Geometry? value) => d.SetValue(IconProperty, value);

    public static bool GetIsActive(DependencyObject d) => (bool)d.GetValue(IsActiveProperty);

    public static void SetIsActive(DependencyObject d, bool value) => d.SetValue(IsActiveProperty, value);

    public static string? GetTone(DependencyObject d) => (string?)d.GetValue(ToneProperty);

    public static void SetTone(DependencyObject d, string? value) => d.SetValue(ToneProperty, value);
}

/// <summary>Renders a TextBlock's text with matched characters emphasised (search highlighting).</summary>
public static class HighlightText
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
        "Text", typeof(string), typeof(HighlightText), new PropertyMetadata(null, OnChanged));

    public static readonly DependencyProperty PositionsProperty = DependencyProperty.RegisterAttached(
        "Positions", typeof(IEnumerable), typeof(HighlightText), new PropertyMetadata(null, OnChanged));

    public static string? GetText(DependencyObject d) => (string?)d.GetValue(TextProperty);

    public static void SetText(DependencyObject d, string? value) => d.SetValue(TextProperty, value);

    public static IEnumerable? GetPositions(DependencyObject d) => (IEnumerable?)d.GetValue(PositionsProperty);

    public static void SetPositions(DependencyObject d, IEnumerable? value) => d.SetValue(PositionsProperty, value);

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock block)
        {
            return;
        }

        var text = GetText(block) ?? string.Empty;
        var positions = GetPositions(block)?.OfType<int>().ToHashSet() ?? [];
        block.Inlines.Clear();
        if (positions.Count == 0)
        {
            block.Inlines.Add(new Run(text));
            return;
        }

        var start = 0;
        while (start < text.Length)
        {
            var matched = positions.Contains(start);
            var end = start;
            while (end < text.Length && positions.Contains(end) == matched)
            {
                end++;
            }

            var run = new Run(text[start..end]);
            if (matched)
            {
                run.SetResourceReference(TextElement.ForegroundProperty, "Brush.TextAccent");
                run.FontWeight = FontWeights.Bold;
            }

            block.Inlines.Add(run);
            start = end;
        }
    }
}

/// <summary>Small behaviours: select-all on focus, focus on load, Enter executes a command.</summary>
public static class Behaviors
{
    public static readonly DependencyProperty FocusOnLoadProperty = DependencyProperty.RegisterAttached(
        "FocusOnLoad", typeof(bool), typeof(Behaviors), new PropertyMetadata(false, OnFocusOnLoad));

    public static readonly DependencyProperty EnterCommandProperty = DependencyProperty.RegisterAttached(
        "EnterCommand", typeof(ICommand), typeof(Behaviors), new PropertyMetadata(null, OnEnterCommand));

    public static readonly DependencyProperty SubmitCommandProperty = DependencyProperty.RegisterAttached(
        "SubmitCommand", typeof(ICommand), typeof(Behaviors), new PropertyMetadata(null, OnSubmitCommand));

    public static bool GetFocusOnLoad(DependencyObject d) => (bool)d.GetValue(FocusOnLoadProperty);

    public static void SetFocusOnLoad(DependencyObject d, bool value) => d.SetValue(FocusOnLoadProperty, value);

    public static ICommand? GetEnterCommand(DependencyObject d) => (ICommand?)d.GetValue(EnterCommandProperty);

    public static void SetEnterCommand(DependencyObject d, ICommand? value) => d.SetValue(EnterCommandProperty, value);

    public static ICommand? GetSubmitCommand(DependencyObject d) => (ICommand?)d.GetValue(SubmitCommandProperty);

    public static void SetSubmitCommand(DependencyObject d, ICommand? value) => d.SetValue(SubmitCommandProperty, value);

    private static void OnFocusOnLoad(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement element && e.NewValue is true)
        {
            element.Loaded += (_, _) => element.Dispatcher.BeginInvoke(() =>
            {
                if (element is SecureField secure)
                {
                    secure.FocusField();
                }
                else
                {
                    element.Focus();
                    Keyboard.Focus(element);
                }
            }, System.Windows.Threading.DispatcherPriority.Input);
        }
    }

    private static void OnEnterCommand(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element || e.OldValue is not null)
        {
            return;
        }

        element.PreviewKeyDown += (_, args) =>
        {
            if (args.Key == Key.Enter && GetEnterCommand(element) is { } command && command.CanExecute(null))
            {
                command.Execute(null);
                args.Handled = true;
            }
        };
    }

    private static void OnSubmitCommand(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not SecureField field || e.OldValue is not null)
        {
            return;
        }

        field.Submitted += (_, _) =>
        {
            if (GetSubmitCommand(field) is { } command && command.CanExecute(null))
            {
                command.Execute(null);
            }
        };
    }
}
