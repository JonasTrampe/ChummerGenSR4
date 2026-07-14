using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Chummer.AvaloniaSpike.Converters;

/// <summary>
///     Simple boolean negation for template bindings (e.g. "show the plus-sign stroke
///     only while collapsed").
/// </summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public static readonly InverseBoolConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return !(value is true);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}