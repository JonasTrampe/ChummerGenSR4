using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Chummer.Core;
using Chummer.NewUI.Dialogs;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Controls.CharacterSections;

public partial class ImprovementsTab : UserControl
{
    public ImprovementsSectionViewModel ViewModel { get; } = new();
    private CharacterDocument? _character;

    public ImprovementsTab()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    public void LoadCharacter(CharacterDocument character)
    {
        _character = character;
        ViewModel.LoadCharacter(character);
    }

    private async void OnAddImprovementClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new CreateImprovementDialog(_character);
        if (await dialog.ShowDialog<bool>(window)
            && _character.AddCustomImprovement(dialog.ResultType, dialog.ResultName, dialog.ResultVal,
                dialog.ResultMin, dialog.ResultMax, dialog.ResultAug, dialog.ResultSelect, dialog.ResultApplyToRating))
            ViewModel.LoadCharacter(_character);
    }

    private void OnDeleteImprovementClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedImprovement is not { IsCustom: true } selected)
            return;
        if (_character.RemoveCustomImprovement(selected.SourceName))
            ViewModel.LoadCharacter(_character);
    }
}
