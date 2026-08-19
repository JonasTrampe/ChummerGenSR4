using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Chummer.NewUI.Dialogs;

/// <summary>A minimal OK-only info dialog, for the handful of call sites that just need to show a
/// message (legacy uses WinForms' MessageBox.Show for these).</summary>
public partial class MessageBoxDialog : Window
{
    public MessageBoxDialog(string strTitle, string strMessage)
    {
        InitializeComponent();
        Title = strTitle;
        MessageText.Text = strMessage;
    }

    private void OnOk(object? sender, RoutedEventArgs e) => Close();
}
