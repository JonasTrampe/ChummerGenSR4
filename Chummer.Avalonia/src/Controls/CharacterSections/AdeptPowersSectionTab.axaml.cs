using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Chummer.Core;
using Chummer.NewUI.Controls;
using Chummer.NewUI.Dialogs;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Controls.CharacterSections;

public partial class AdeptPowersSectionTab : UserControl
{
    public AdeptPowersSectionViewModel ViewModel { get; } = new();

    public AdeptPowersSectionTab()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    private CharacterDocument? _character;

    public void LoadCharacter(CharacterDocument character)
    {
        _character = character;
        ViewModel.LoadCharacter(character);
    }

    private async void OnAddPowerClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new PowerDialog(_character);
        bool? added = await dialog.ShowDialog<bool?>(window);
        if (added != true || dialog.SelectedPower is not { } selected)
            return;

        string strRating = dialog.SelectedRating.ToString();
        if (_character.GetSenseImprovementOptions(selected.Name).Count > 0)
        {
            var senseDialog = new SenseImprovementDialog(_character, selected.Name);
            bool senseSelected = await senseDialog.ShowDialog<bool>(window);
            if (!senseSelected || senseDialog.SelectedName == null)
                return;

            if (_character.AddImprovedSensePower(selected.Name, strRating, selected.PointsPerLevel, senseDialog.SelectedName))
                ViewModel.LoadCharacter(_character);
            return;
        }

        string strSelected = string.Empty;
        var skillOptions = _character.GetAdeptPowerSkillSelectionOptions(selected.Name);
        var attributeOptions = _character.GetAdeptPowerAttributeSelectionOptions(selected.Name);
        if (skillOptions.Count > 0 || attributeOptions.Count > 0)
        {
            var listDialog = new ListSelectionDialog($"„{selected.Name}“ - "
                    + (skillOptions.Count > 0 ? "Fertigkeit auswählen:" : "Attribut auswählen:"),
                skillOptions.Count > 0 ? skillOptions : attributeOptions);
            if (!await listDialog.ShowDialog<bool>(window) || listDialog.SelectedValue == null)
                return;
            strSelected = listDialog.SelectedValue;
        }

        _character.AddAdeptPower(selected.Name, strRating, selected.PointsPerLevel, strSelected);
        ViewModel.LoadCharacter(_character);
    }

    private void OnDeletePowerClick(object? sender, System.EventArgs e)
    {
        if (_character == null || sender is not AdeptPowerRow { DataContext: AdeptPowerRowViewModel row })
            return;

        if (_character.RemoveAdeptPower(row.Name))
            ViewModel.LoadCharacter(_character);
    }
}
