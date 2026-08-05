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
        if (added == true && dialog.SelectedPower is { } selected)
        {
            _character.AddAdeptPower(selected.Name, dialog.SelectedRating.ToString(), selected.PointsPerLevel);
            ViewModel.LoadCharacter(_character);
        }
    }

    private void OnDeletePowerClick(object? sender, System.EventArgs e)
    {
        if (_character == null || sender is not AdeptPowerRow { DataContext: AdeptPowerRowViewModel row })
            return;

        if (_character.RemoveAdeptPower(row.Name))
            ViewModel.LoadCharacter(_character);
    }
}
