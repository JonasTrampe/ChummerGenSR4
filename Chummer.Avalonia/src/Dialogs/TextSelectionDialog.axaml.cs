using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Chummer.NewUI.Dialogs;

/// <summary>Ported from frmSelectText.cs: a plain free-text prompt, used by clsImprovement.cs's
/// selecttext bonus node (e.g. Allergy's substance, Codeslinger's Matrix action, Prejudiced's
/// target) - the app doesn't have a curated list for these, same as legacy.</summary>
public partial class TextSelectionDialog : Window
{
    public string EnteredText => ValueBox.Text?.Trim() ?? string.Empty;
    private bool AllowEmpty { get; }

    public TextSelectionDialog(string strDescription, string strInitialValue = "", bool blnAllowEmpty = false)
    {
        InitializeComponent();
        DescriptionText.Text = strDescription;
        ValueBox.Text = strInitialValue;
        ValueBox.CaretIndex = ValueBox.Text?.Length ?? 0;
        AllowEmpty = blnAllowEmpty;
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        if (AllowEmpty || !string.IsNullOrWhiteSpace(ValueBox.Text))
            Close(true);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
