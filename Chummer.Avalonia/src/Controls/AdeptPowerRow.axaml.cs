using Avalonia;
using Avalonia.Controls;

namespace Chummer.NewUI.Controls;

public partial class AdeptPowerRow : UserControl
{
    public static readonly StyledProperty<string?> PowerNameProperty =
        AvaloniaProperty.Register<AdeptPowerRow, string?>(nameof(PowerName));

    public static readonly StyledProperty<int> PowerLevelProperty =
        AvaloniaProperty.Register<AdeptPowerRow, int>(nameof(PowerLevel));

    public static readonly StyledProperty<string?> PricePerLevelProperty =
        AvaloniaProperty.Register<AdeptPowerRow, string?>(nameof(PricePerLevel));

    public static readonly StyledProperty<string?> TotalCostProperty =
        AvaloniaProperty.Register<AdeptPowerRow, string?>(nameof(TotalCost));

    public static readonly StyledProperty<bool> IsWayOfTheAdeptProperty =
        AvaloniaProperty.Register<AdeptPowerRow, bool>(nameof(IsWayOfTheAdept));

    public static readonly StyledProperty<bool> IsMagicFocusProperty =
        AvaloniaProperty.Register<AdeptPowerRow, bool>(nameof(IsMagicFocus));

    public static readonly StyledProperty<bool> IsNudEnabledProperty =
        AvaloniaProperty.Register<AdeptPowerRow, bool>(nameof(IsNudEnabled));

    public AdeptPowerRow()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }


    public string? PowerName
    {
        get => GetValue(PowerNameProperty);
        set => SetValue(PowerNameProperty, value);
    }

    public int PowerLevel
    {
        get => GetValue(PowerLevelProperty);
        set => SetValue(PowerLevelProperty, value);
    }

    public string? PricePerLevel
    {
        get => GetValue(PricePerLevelProperty);
        set => SetValue(PricePerLevelProperty, value);
    }

    public string? TotalCost
    {
        get => GetValue(TotalCostProperty);
        set => SetValue(TotalCostProperty, value);
    }

    public bool IsWayOfTheAdept
    {
        get => GetValue(IsWayOfTheAdeptProperty);
        set => SetValue(IsWayOfTheAdeptProperty, value);
    }

    public bool IsMagicFocus
    {
        get => GetValue(IsMagicFocusProperty);
        set => SetValue(IsMagicFocusProperty, value);
    }

    public bool IsNudEnabled
    {
        get => GetValue(IsNudEnabledProperty);
        set => SetValue(IsNudEnabledProperty, value);
    }
}
