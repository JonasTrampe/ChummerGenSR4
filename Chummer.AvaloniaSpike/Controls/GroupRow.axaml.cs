using Avalonia;
using Avalonia.Controls;

namespace Chummer.AvaloniaSpike.Controls;

public partial class GroupRow : UserControl
{
    public static readonly StyledProperty<string?> GroupNameProperty =
        AvaloniaProperty.Register<GroupRow, string?>(nameof(GroupName));

    public static readonly StyledProperty<string?> RatingProperty =
        AvaloniaProperty.Register<GroupRow, string?>(nameof(Rating));

    public GroupRow()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }

    public string? GroupName
    {
        get => GetValue(GroupNameProperty);
        set => SetValue(GroupNameProperty, value);
    }

    public string? Rating
    {
        get => GetValue(RatingProperty);
        set => SetValue(RatingProperty, value);
    }
}
