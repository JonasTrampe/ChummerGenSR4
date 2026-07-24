using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Chummer.NewUI.Dialogs;

public partial class ContactNotesDialog : Window
{
    public string Notes
    {
        get => this.FindControl<TextBox>("NotesTextBox")!.Text ?? string.Empty;
        set => this.FindControl<TextBox>("NotesTextBox")!.Text = value;
    }

    public ContactNotesDialog()
    {
        InitializeComponent();
    }

    private void OnOk(object? sender, RoutedEventArgs e) => Close(true);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
