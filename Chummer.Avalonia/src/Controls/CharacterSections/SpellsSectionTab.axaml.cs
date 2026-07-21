using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Chummer.Core;
using Chummer.NewUI.ViewModels;
using SpellDialog = Chummer.NewUI.Dialogs.SpellDialog;

namespace Chummer.NewUI.Controls.CharacterSections;

public partial class SpellsSectionTab : UserControl
{
    public SpellsSectionViewModel ViewModel { get; } = new();

    public SpellsSectionTab()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    public void LoadCharacter(CharacterDocument character) => ViewModel.LoadCharacter(character);

    private async void OnAddSpellClick(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new SpellDialog();
        await dialog.ShowDialog(window);
    }
}
