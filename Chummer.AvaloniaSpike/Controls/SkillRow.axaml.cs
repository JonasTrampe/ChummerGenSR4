using Avalonia;
using Avalonia.Controls;

namespace Chummer.AvaloniaSpike.Controls;

public partial class SkillRow : UserControl
{
    public static readonly StyledProperty<string?> SkillNameProperty =
        AvaloniaProperty.Register<SkillRow, string?>(nameof(SkillName));

    public static readonly StyledProperty<string?> AttributeProperty =
        AvaloniaProperty.Register<SkillRow, string?>(nameof(Attribute));

    public static readonly StyledProperty<string?> RatingProperty =
        AvaloniaProperty.Register<SkillRow, string?>(nameof(Rating));

    public static readonly StyledProperty<string?> PoolProperty =
        AvaloniaProperty.Register<SkillRow, string?>(nameof(Pool));

    public static readonly StyledProperty<bool> IsDisabledProperty =
        AvaloniaProperty.Register<SkillRow, bool>(nameof(IsDisabled));

    public static readonly StyledProperty<bool> IsInactiveProperty =
        AvaloniaProperty.Register<SkillRow, bool>(nameof(IsDisabled));

    public SkillRow()
    {
        InitializeComponent();
    }

    public string? SkillName
    {
        get => GetValue(SkillNameProperty);
        set => SetValue(SkillNameProperty, value);
    }

    public string? Attribute
    {
        get => GetValue(AttributeProperty);
        set => SetValue(AttributeProperty, value);
    }

    public string? Rating
    {
        get => GetValue(RatingProperty);
        set => SetValue(RatingProperty, value);
    }

    public string? Pool
    {
        get => GetValue(PoolProperty);
        set => SetValue(PoolProperty, value);
    }

    public bool IsDisabled
    {
        get => GetValue(IsDisabledProperty);
        set => SetValue(IsDisabledProperty, value);
    }

    public bool IsInactive
    {
        get => GetValue(IsInactiveProperty);
        set => SetValue(IsInactiveProperty, value);
    }
}