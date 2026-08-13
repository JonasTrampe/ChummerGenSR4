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
        InitializeComponent();
        Title = title;
        PromptText.Text = sign > 0 ? "Gib den verdienten Betrag und einen Grund ein." : "Gib den ausgegebenen Betrag und einen Grund ein.";
    }

    /// <summary>Edit mode: pre-fills the existing entry's amount (sign inferred from it) and
    /// reason, ported from frmCareer.cs's karma/nuyen expense edit flow.</summary>
    public ExpenseDialog(string title, decimal decExistingSignedAmount, string strExistingReason)
        : this(title, decExistingSignedAmount < 0 ? -1 : 1)
    {
        PromptText.Text = App.LanguageCatalog.GetString("UI_EditAmountAndReasonPrompt");
        AmountBox.Text = Math.Abs(decExistingSignedAmount).ToString(CultureInfo.CurrentCulture);
        ReasonBox.Text = strExistingReason;
        OkButton.Content = App.LanguageCatalog.GetString("UI_Save");
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(AmountBox.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out decimal amount) || amount <= 0)
        {
            ErrorText.Text = App.LanguageCatalog.GetString("UI_QuantityMustBeGreaterThanZero");
            return;
        }
        if (string.IsNullOrWhiteSpace(ReasonBox.Text))
        {
            ErrorText.Text = App.LanguageCatalog.GetString("UI_ReasonRequired");
            return;
        }

        Amount = amount * _sign;
        Reason = ReasonBox.Text.Trim();
        Close(true);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
