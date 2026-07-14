using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Chummer.AvaloniaSpike.Converters;

/// <summary>Item count > 0 -> true. Used to hide the expander box on leaf TreeViewItems.</summary>
public sealed class CountToBoolConverter : IValueConverter
{
    public static readonly CountToBoolConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int count && count > 0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
