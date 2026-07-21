using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Chummer.Core;
using Chummer.NewUI.ViewModels;
using QualityDialog = Chummer.NewUI.Dialogs.QualityDialog;

namespace Chummer.NewUI.Controls.CharacterSections;

public partial class GeneralSectionTab : UserControl
{
    private CharacterDocument? _character;

    public GeneralSectionViewModel ViewModel { get; } = new();

    public GeneralSectionTab()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    public void LoadCharacter(CharacterDocument character)
    {
        _character = character;
        ViewModel.LoadCharacter(character);
    }

    private async void OnAddQualityClick(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not Window window)
            return;

        if (_character == null)
            return;

        var dialog = new QualityDialog(_character);
        bool? added = await dialog.ShowDialog<bool?>(window);
        if (added == true && dialog.SelectedQuality != null)
        {
            _character.AddQuality(dialog.SelectedQuality.Name, dialog.SelectedQuality.Category);
            ViewModel.LoadCharacter(_character);
        }
    }
}
