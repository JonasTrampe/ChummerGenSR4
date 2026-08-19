using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.Core;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Dialogs;

public partial class WeaponAccessoryDialog : Window
{
    public WeaponAccessoryDialogViewModel ViewModel { get; } = new();
    public WeaponAccessoryOptionViewModel? SelectedAccessory => ViewModel.Selected;
    public WeaponAccessoryDialog() : this(null) { }
    public WeaponAccessoryDialog(CharacterDocument? character) { DataContext = ViewModel; InitializeComponent(); ViewModel.LoadOptions(character); }
    private void OnOk(object? sender, RoutedEventArgs e) { if (SelectedAccessory != null) Close(true); }
    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
