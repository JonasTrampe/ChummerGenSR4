using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Chummer.NewUI.Controls;

public partial class AttributeRow : UserControl
{
    public static readonly StyledProperty<string?> CodeProperty =
        AvaloniaProperty.Register<AttributeRow, string?>(nameof(Code));

    public static readonly StyledProperty<string?> AttributeNameProperty =
        AvaloniaProperty.Register<AttributeRow, string?>(nameof(AttributeName));

    public static readonly StyledProperty<string?> BaseProperty =
        AvaloniaProperty.Register<AttributeRow, string?>(nameof(Base));

    public static readonly StyledProperty<bool> IsCreateModeProperty =
        AvaloniaProperty.Register<AttributeRow, bool>(nameof(IsCreateMode));

    public static readonly StyledProperty<int> BaseValueProperty =
        AvaloniaProperty.Register<AttributeRow, int>(nameof(BaseValue), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<int> MinValueProperty =
        AvaloniaProperty.Register<AttributeRow, int>(nameof(MinValue));

    public static readonly StyledProperty<int> MaxValueProperty =
        AvaloniaProperty.Register<AttributeRow, int>(nameof(MaxValue), 6);

    public static readonly StyledProperty<string?> AugmentedProperty =
        AvaloniaProperty.Register<AttributeRow, string?>(nameof(Augmented));

    public static readonly StyledProperty<string?> RangeProperty =
        AvaloniaProperty.Register<AttributeRow, string?>(nameof(Range));

    public static readonly StyledProperty<string?> SourceProperty =
        AvaloniaProperty.Register<AttributeRow, string?>(nameof(Source));

    public static readonly StyledProperty<bool> ShowRemoveProperty =
        AvaloniaProperty.Register<AttributeRow, bool>(nameof(ShowRemove));

    public event EventHandler? RaiseClicked;

    public AttributeRow()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }

    public string? Code
    {
        get => GetValue(CodeProperty);
        set => SetValue(CodeProperty, value);
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

    public bool IsCreateMode
    {
        get => GetValue(IsCreateModeProperty);
        set => SetValue(IsCreateModeProperty, value);
    }

    public int BaseValue
    {
        get => GetValue(BaseValueProperty);
        set => SetValue(BaseValueProperty, value);
    }

    public int MinValue
    {
        get => GetValue(MinValueProperty);
        set => SetValue(MinValueProperty, value);
    }

    public int MaxValue
    {
        get => GetValue(MaxValueProperty);
        set => SetValue(MaxValueProperty, value);
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

    private void OnRaiseButtonClick(object? sender, RoutedEventArgs e)
    {
        RaiseClicked?.Invoke(this, EventArgs.Empty);
    }
}
