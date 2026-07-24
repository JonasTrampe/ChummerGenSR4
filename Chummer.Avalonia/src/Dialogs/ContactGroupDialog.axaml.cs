using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Dialogs;

public partial class ContactGroupDialog : Window
{
    public ContactGroupDialogViewModel ViewModel { get; } = new();

    public ContactGroupDialog()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    private void OnOk(object? sender, RoutedEventArgs e) => Close(true);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
