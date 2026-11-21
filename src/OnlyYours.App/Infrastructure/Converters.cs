using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace OnlyYours.App.Infrastructure;

/// <summary>true → Visible (or Collapsed with Invert).</summary>
public sealed class VisibleWhen : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var visible = value switch
        {
            bool b => b,
            int i => i != 0,
            double d => d != 0,
            string s => !string.IsNullOrWhiteSpace(s),
            ICollection c => c.Count > 0,
            null => false,
            _ => true,
        };
        if (parameter is string expected && value is not null)
        {
            visible = string.Equals(value.ToString(), expected, StringComparison.OrdinalIgnoreCase);
        }

        return visible ^ Invert ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Two-way "value equals parameter" for radio buttons bound to an enum or string.</summary>
public sealed class EqualsParameter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not null && parameter is not null && string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not true || parameter is null)
        {
            return Binding.DoNothing;
        }

        if (targetType.IsEnum)
        {
            return Enum.Parse(targetType, parameter.ToString()!, ignoreCase: true);
        }

        if (targetType == typeof(int) && int.TryParse(parameter.ToString(), out var number))
        {
            return number;
        }

        return parameter.ToString()!;
    }
}

/// <summary>"Icon.Key" → the geometry resource.</summary>
public sealed class IconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string key ? Application.Current?.TryFindResource(key.StartsWith("Icon.", StringComparison.Ordinal) ? key : "Icon." + key) as Geometry : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Tone name ("danger", "warning", "ok", "info", "accent") → theme brush.</summary>
public sealed class ToneBrush : IValueConverter
{
    public bool Soft { get; set; }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var tone = (value as string ?? parameter as string ?? "accent").ToLowerInvariant();
        var key = tone switch
        {
            "danger" or "error" => "Danger",
            "warning" => "Warning",
            "ok" or "success" => "Success",
            "info" => "Info",
            _ => "Accent",
        };
        return Application.Current?.TryFindResource("Brush." + key + (Soft ? (key == "Accent" ? "Soft" : "Soft") : string.Empty));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Toast kind → tone.</summary>
public sealed class ToastToneConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        ViewModels.ToastKind.Success => Application.Current.TryFindResource("Brush.Success"),
        ViewModels.ToastKind.Warning => Application.Current.TryFindResource("Brush.Warning"),
        ViewModels.ToastKind.Error => Application.Current.TryFindResource("Brush.Danger"),
        _ => Application.Current.TryFindResource("Brush.Accent"),
    } ?? Brushes.Goldenrod;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Strength level (0..5) → label key colour.</summary>
public sealed class LevelBrush : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var level = value is int i ? i : 0;
        var key = level switch
        {
            0 => "Brush.TextFaint",
            <= 1 => "Brush.Danger",
            2 => "Brush.Warning",
            3 => "Brush.TextAccent",
            _ => "Brush.Success",
        };
        return Application.Current?.TryFindResource(key);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Localized text for a key: <c>{Binding Kind, Converter={StaticResource Loc}, ConverterParameter=Kind.}</c>.</summary>
public sealed class LocConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Loc.T($"{parameter}{value}");

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>First character, upper-cased (monogram fallbacks).</summary>
public sealed class InitialConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string { Length: > 0 } text ? char.ToUpper(text.Trim()[0], culture).ToString() : "?";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Progress 0..1 → width fraction of the parent (ConverterParameter = full width).</summary>
public sealed class FractionWidth : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not double fraction || values[1] is not double width)
        {
            return 0.0;
        }

        return Math.Max(0, Math.Clamp(fraction, 0, 1) * width);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
