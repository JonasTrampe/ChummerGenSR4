using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Chummer.NewUI.Dialogs;

/// <summary>Small reusable Yes/No dialog for destructive actions. The caller supplies already
/// localized title/message text so it stays presentation-only and has no character-model logic.</summary>
public partial class ConfirmationDialog : Window
{
    public ConfirmationDialog(string title, string message)
    {
        InitializeComponent();
        Title = title;
        MessageText.Text = message;
    }

    private void OnConfirm(object? sender, RoutedEventArgs e) => Close(true);
    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
