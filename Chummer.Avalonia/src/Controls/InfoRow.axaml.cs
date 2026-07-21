using Avalonia;
using Avalonia.Controls;

namespace Chummer.NewUI.Controls;

public partial class InfoRow : UserControl
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<InfoRow, string?>(nameof(Label));

    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<InfoRow, string?>(nameof(Value));

    public static readonly StyledProperty<string?> TooltipProperty =
        AvaloniaProperty.Register<InfoRow, string?>(nameof(Tooltip));

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>Mouseover explanation of how Value was calculated - e.g. a breakdown of which
    /// attributes and Improvements fed into a derived stat.</summary>
    public string? Tooltip
    {
        get => GetValue(TooltipProperty);
        set => SetValue(TooltipProperty, value);
    }

    public InfoRow()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }
}
