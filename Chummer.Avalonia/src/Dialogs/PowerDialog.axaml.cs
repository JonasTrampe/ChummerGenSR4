using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.Core;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Dialogs;

public partial class PowerDialog : Window
{
    public PowerDialogViewModel ViewModel { get; } = new();
    public PowerOptionViewModel? SelectedPower => ViewModel.Selected;
    public int SelectedRating => ViewModel.CanChooseRating ? ViewModel.Rating : 1;

    public PowerDialog()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    public PowerDialog(CharacterDocument character)
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
