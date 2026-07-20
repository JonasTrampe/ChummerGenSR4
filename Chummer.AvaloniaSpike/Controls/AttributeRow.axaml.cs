using Avalonia;
using Avalonia.Controls;

namespace Chummer.AvaloniaSpike.Controls;

public partial class AttributeRow : UserControl
{
    public static readonly StyledProperty<string?> AttributeNameProperty =
        AvaloniaProperty.Register<AttributeRow, string?>(nameof(AttributeName));

    public static readonly StyledProperty<string?> BaseProperty =
        AvaloniaProperty.Register<AttributeRow, string?>(nameof(Base));

    public static readonly StyledProperty<string?> AugmentedProperty =
        AvaloniaProperty.Register<AttributeRow, string?>(nameof(Augmented));

    public static readonly StyledProperty<string?> RangeProperty =
        AvaloniaProperty.Register<AttributeRow, string?>(nameof(Range));

    public static readonly StyledProperty<string?> SourceProperty =
        AvaloniaProperty.Register<AttributeRow, string?>(nameof(Source));

    public static readonly StyledProperty<bool> ShowRemoveProperty =
        AvaloniaProperty.Register<AttributeRow, bool>(nameof(ShowRemove));

    public AttributeRow()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }

    public string? AttributeName
    {
        get => GetValue(AttributeNameProperty);
        set => SetValue(AttributeNameProperty, value);
    }

    public string? Base
    {
        get => GetValue(BaseProperty);
        set => SetValue(BaseProperty, value);
    }

    public string? Augmented
    {
        get => GetValue(AugmentedProperty);
        set => SetValue(AugmentedProperty, value);
    }

    public string? Range
    {
        get => GetValue(RangeProperty);
        set => SetValue(RangeProperty, value);
    }

    public string? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public bool ShowRemove
    {
        get => GetValue(ShowRemoveProperty);
        set => SetValue(ShowRemoveProperty, value);
    }
}
