using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Chummer.NewUI.Controls;

public partial class IntegerSpinner : UserControl
{
    public static readonly StyledProperty<int> ValueProperty =
        AvaloniaProperty.Register<IntegerSpinner, int>(nameof(Value), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<int> MinimumProperty =
        AvaloniaProperty.Register<IntegerSpinner, int>(nameof(Minimum), 0);

    public static readonly StyledProperty<int> MaximumProperty =
        AvaloniaProperty.Register<IntegerSpinner, int>(nameof(Maximum), 9999);

    public static readonly StyledProperty<int> StepProperty =
        AvaloniaProperty.Register<IntegerSpinner, int>(nameof(Step), 1);

    public static readonly DirectProperty<IntegerSpinner, string> ValueTextProperty =
        AvaloniaProperty.RegisterDirect<IntegerSpinner, string>(nameof(ValueText), o => o.ValueText, (o, v) => o.ValueText = v);

    private string _strValueText = "0";

    public IntegerSpinner()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
        UpdateValueText();
    }

    public int Value
    {
        get => GetValue(ValueProperty);
        set
        {
            int intClamped = Math.Max(Minimum, Math.Min(Maximum, value));
            SetValue(ValueProperty, intClamped);
            UpdateValueText();
        }
    }

    public int Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public int Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public int Step
    {
        get => GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }

    public string ValueText
    {
        get => _strValueText;
        set
        {
            if (value == _strValueText)
                return;

            _strValueText = value;
            if (int.TryParse(value, out int intParsed))
                SetCurrentValue(ValueProperty, Math.Max(Minimum, Math.Min(Maximum, intParsed)));
            RaisePropertyChanged(ValueTextProperty, string.Empty, _strValueText);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ValueProperty)
            UpdateValueText();
    }

    private void UpdateValueText()
    {
        string strNewValue = GetValue(ValueProperty).ToString();
        if (_strValueText == strNewValue)
            return;

        string strOldValue = _strValueText;
        _strValueText = strNewValue;
        RaisePropertyChanged(ValueTextProperty, strOldValue, strNewValue);
    }

    private void OnIncreaseClick(object? sender, RoutedEventArgs e)
    {
        Value += Step;
    }

    private void OnDecreaseClick(object? sender, RoutedEventArgs e)
    {
        Value -= Step;
    }
}
