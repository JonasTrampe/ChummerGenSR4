using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Dialogs;

public partial class WeaponModDialog : Window
{
    public WeaponModDialogViewModel ViewModel { get; } = new();
    public WeaponModOptionViewModel? SelectedMod => ViewModel.SelectedMod;
    public decimal Rating => ViewModel.Rating;
    public WeaponModDialog() { DataContext = ViewModel; InitializeComponent(); ViewModel.LoadOptions(); }
    private void OnOk(object? sender, RoutedEventArgs e) { if (SelectedMod != null) Close(true); }
    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
