using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Chummer.NewUI.Controls;

public partial class GroupRow : UserControl
{
    public static readonly StyledProperty<string?> GroupNameProperty =
        AvaloniaProperty.Register<GroupRow, string?>(nameof(GroupName));

    public static readonly StyledProperty<string?> RatingProperty =
        AvaloniaProperty.Register<GroupRow, string?>(nameof(Rating));

    public static readonly StyledProperty<bool> IsCreateModeProperty =
        AvaloniaProperty.Register<GroupRow, bool>(nameof(IsCreateMode));

    public static readonly StyledProperty<int> RatingValueProperty =
        AvaloniaProperty.Register<GroupRow, int>(nameof(RatingValue), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public event EventHandler? RaiseClicked;

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

    public bool IsCreateMode
    {
        get => GetValue(IsCreateModeProperty);
        set => SetValue(IsCreateModeProperty, value);
    }

    public int RatingValue
    {
        get => GetValue(RatingValueProperty);
        set => SetValue(RatingValueProperty, value);
    }

    private void OnRaiseButtonClick(object? sender, RoutedEventArgs e)
    {
        RaiseClicked?.Invoke(this, EventArgs.Empty);
    }
}
