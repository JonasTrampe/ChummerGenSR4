using Avalonia;
using Avalonia.Controls;

namespace Chummer.AvaloniaSpike.Controls;

public partial class AdeptPowerRow : UserControl
{
    public static readonly StyledProperty<string?> PowerNameProperty =
        AvaloniaProperty.Register<SkillRow, string?>(nameof(PowerName));

    public static readonly StyledProperty<string?> PowerLevelProperty =
        AvaloniaProperty.Register<SkillRow, string?>(nameof(PowerLevel));

    public static readonly StyledProperty<string?> PricePerLevelProperty =
        AvaloniaProperty.Register<SkillRow, string?>(nameof(PricePerLevel));

    public static readonly StyledProperty<string?> TotalCostProperty =
        AvaloniaProperty.Register<SkillRow, string?>(nameof(TotalCost));

    public static readonly StyledProperty<bool> IsWayOfTheAdeptProperty =
        AvaloniaProperty.Register<SkillRow, bool>(nameof(IsWayOfTheAdept));

    public static readonly StyledProperty<bool> IsMagicFocusProperty =
        AvaloniaProperty.Register<SkillRow, bool>(nameof(IsMagicFocus));

    public static readonly StyledProperty<bool> IsNudEnabledProperty =
        AvaloniaProperty.Register<SkillRow, bool>(nameof(IsNudEnabled));

    public AdeptPowerRow()
    {
        InitializeComponent();
    }


    public string? PowerName
    {
        get => GetValue(PowerNameProperty);
        set => SetValue(PowerNameProperty, value);
    }

    public string? PowerLevel
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