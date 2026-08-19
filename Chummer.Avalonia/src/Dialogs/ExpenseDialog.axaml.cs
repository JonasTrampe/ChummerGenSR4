using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Dialogs;

public partial class ExpenseDialog : Window
{
    private readonly int _sign;
    public ExpenseDialogViewModel ViewModel { get; }

    public decimal Amount { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public DateTime Date => ViewModel.SelectedDateTime;

    public ExpenseDialog()
        : this("Aufwendung", 1)
    {
    }

    public ExpenseDialog(string title, int sign)
        : this(title, sign, DateTime.Now)
    {
    }

    private ExpenseDialog(string title, int sign, DateTime datInitialDate)
    {
        _sign = sign;
        ViewModel = new ExpenseDialogViewModel(datInitialDate)
        {
            Prompt = sign > 0 ? "Gib den verdienten Betrag und einen Grund ein." : "Gib den ausgegebenen Betrag und einen Grund ein."
        };
        DataContext = ViewModel;
        InitializeComponent();
        Title = title;
    }

    /// <summary>Edit mode: pre-fills the existing entry's amount (sign inferred from it) and
    /// reason, ported from frmCareer.cs's karma/nuyen expense edit flow.</summary>
    public ExpenseDialog(string title, decimal decExistingSignedAmount, string strExistingReason)
        : this(title, decExistingSignedAmount, strExistingReason, DateTime.Now)
    {
    }

    public ExpenseDialog(string title, decimal decExistingSignedAmount, string strExistingReason, DateTime datExistingDate)
        : this(title, decExistingSignedAmount < 0 ? -1 : 1, datExistingDate)
    {
        ViewModel.Prompt = App.LanguageCatalog.GetString("UI_EditAmountAndReasonPrompt");
        ViewModel.AmountText = Math.Abs(decExistingSignedAmount).ToString(CultureInfo.CurrentCulture);
        ViewModel.Reason = strExistingReason;
        OkButton.Content = App.LanguageCatalog.GetString("UI_Save");
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(ViewModel.AmountText, NumberStyles.Number, CultureInfo.CurrentCulture, out decimal amount) || amount <= 0)
        {
            ViewModel.Error = App.LanguageCatalog.GetString("UI_QuantityMustBeGreaterThanZero");
            return;
        }
        if (string.IsNullOrWhiteSpace(ViewModel.Reason))
        {
            ViewModel.Error = App.LanguageCatalog.GetString("UI_ReasonRequired");
            return;
        }

        Amount = amount * _sign;
        Reason = ViewModel.Reason.Trim();
        Close(true);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
