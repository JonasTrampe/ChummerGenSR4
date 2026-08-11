using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.Core;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Dialogs;

public partial class ComplexFormDialog : Window
{
    public ComplexFormDialogViewModel ViewModel { get; } = new();
    public ComplexFormOptionViewModel? SelectedForm => ViewModel.Selected;

    public ComplexFormDialog()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    public ComplexFormDialog(CharacterDocument character)
        : this()
    {
        ViewModel.LoadOptions(character);
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.Selected != null)
            Close(true);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
