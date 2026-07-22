using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.Core;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Dialogs;

public partial class SpellDialog : Window
{
    public SpellDialogViewModel ViewModel { get; } = new();
    public SpellOptionViewModel? SelectedSpell => ViewModel.SelectedSpell;

    public SpellDialog()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    public SpellDialog(CharacterDocument character)
        : this()
    {
        ViewModel.LoadOptions(character);
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedSpell != null)
            Close(true);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
