using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.Core;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Dialogs;

public partial class WeaponDialog : Window
{
    public WeaponDialogViewModel ViewModel { get; } = new();
    public WeaponOptionViewModel? SelectedWeapon => ViewModel.SelectedWeapon;

    public WeaponDialog() : this(null) { }

    public WeaponDialog(CharacterDocument? character)
    {
        DataContext = ViewModel;
        InitializeComponent();
        ViewModel.LoadOptions(character);
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedWeapon != null)
            Close(true);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
