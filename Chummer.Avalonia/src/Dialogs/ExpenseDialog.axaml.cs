using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Chummer.NewUI.Dialogs;

public partial class ExpenseDialog : Window
{
    private readonly int _sign;

    public decimal Amount { get; private set; }
    public string Reason { get; private set; } = string.Empty;

    public ExpenseDialog()
        : this("Aufwendung", 1)
    {
    }

    public ExpenseDialog(string title, int sign)
    {
        _sign = sign;
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
        Title = title;
        PromptText.Text = sign > 0 ? "Gib den verdienten Betrag und einen Grund ein." : "Gib den ausgegebenen Betrag und einen Grund ein.";
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(AmountBox.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out decimal amount) || amount <= 0)
        {
            ErrorText.Text = "Die Menge muss größer als 0 sein.";
            return;
        }
        if (string.IsNullOrWhiteSpace(ReasonBox.Text))
        {
            ErrorText.Text = "Ein Grund ist erforderlich.";
            return;
        }

        Amount = amount * _sign;
        Reason = ReasonBox.Text.Trim();
        Close(true);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
