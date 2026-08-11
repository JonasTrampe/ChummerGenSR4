using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Chummer.NewUI.Dialogs;

/// <summary>Ported from frmSelectText.cs: a plain free-text prompt, used by clsImprovement.cs's
/// selecttext bonus node (e.g. Allergy's substance, Codeslinger's Matrix action, Prejudiced's
/// target) - the app doesn't have a curated list for these, same as legacy.</summary>
public partial class TextSelectionDialog : Window
{
    public string EnteredText => ValueBox.Text?.Trim() ?? string.Empty;

    public TextSelectionDialog(string strDescription)
    {
        InitializeComponent();
        DescriptionText.Text = strDescription;
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(ValueBox.Text))
            Close(true);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
