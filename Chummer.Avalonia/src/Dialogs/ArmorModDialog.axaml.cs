using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.Core;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Dialogs;

public partial class ArmorModDialog : Window
{
    public ArmorModDialogViewModel ViewModel { get; } = new();
    public ArmorModOptionViewModel? SelectedMod => ViewModel.SelectedMod;
    public decimal Rating => ViewModel.Rating;
    public ArmorModDialog() : this(null) { }
    public ArmorModDialog(CharacterDocument? character) { DataContext = ViewModel; InitializeComponent(); ViewModel.LoadOptions(character); }
    private void OnOk(object? sender, RoutedEventArgs e) { if (SelectedMod != null) Close(true); }
    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
