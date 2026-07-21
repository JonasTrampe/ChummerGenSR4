using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Chummer.Core;
using Chummer.NewUI.ViewModels;
using QualityDialog = Chummer.NewUI.Dialogs.QualityDialog;

namespace Chummer.NewUI.Controls.CharacterSections;

public partial class GeneralSectionTab : UserControl
{
    public GeneralSectionViewModel ViewModel { get; } = new();

    public GeneralSectionTab()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    public void LoadCharacter(CharacterDocument character) => ViewModel.LoadCharacter(character);

    private async void OnAddQualityClick(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new QualityDialog();
        await dialog.ShowDialog(window);
    }
}
