using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Chummer.NewUI.Converters;

/// <summary>
///     The connector gutter (Controls/TreeConnectorGutter) needs to be wide enough to hold one
///     16px column per ancestor level plus this item's own elbow/tee column - (Level + 1) * 16.
///     Indentation now comes entirely from this growing width rather than a separate margin, so
///     the gutter's own drawing and the row's actual indent can never drift out of sync.
/// </summary>
public sealed class LevelToGutterWidthConverter : IValueConverter
{
    private const double ColumnWidth = 16;
    public static readonly LevelToGutterWidthConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var level = value is int i ? i : 0;
        return (level + 1) * ColumnWidth;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}