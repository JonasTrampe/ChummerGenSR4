using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Chummer.NewUI.Dialogs;

/// <summary>Ported from frmSellItem.cs: asks what percentage of an item's current value to
/// refund in Nuyen when it's removed from the character.</summary>
public partial class SellItemDialog : Window
{
    public SellItemDialog() => InitializeComponent();

    /// <summary>0.0-1.0, matching frmSellItem.SellPercent.</summary>
    public double SellPercent { get; private set; }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        SellPercent = (double)(PercentBox.Value ?? 0) / 100.0;
        Close(true);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
