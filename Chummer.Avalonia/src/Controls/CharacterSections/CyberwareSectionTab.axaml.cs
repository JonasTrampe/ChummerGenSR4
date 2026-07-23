using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Chummer.Core;
using Chummer.NewUI.ViewModels;
using CyberwareDialog = Chummer.NewUI.Dialogs.CyberwareDialog;

namespace Chummer.NewUI.Controls.CharacterSections;

public partial class CyberwareSectionTab : UserControl
{
    private CharacterDocument? _character;

    public CyberwareSectionViewModel ViewModel { get; } = new();

    public CyberwareSectionTab()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    public void LoadCharacter(CharacterDocument character)
    {
        _character = character;
        ViewModel.LoadCharacter(character);
    }

    private async void OnAddCyberwareClick(object? sender, RoutedEventArgs e) => await AddAsync(blnBioware: false);

    private async void OnAddBiowareClick(object? sender, RoutedEventArgs e) => await AddAsync(blnBioware: true);

    private async System.Threading.Tasks.Task AddAsync(bool blnBioware)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new CyberwareDialog(blnBioware);
        bool added = await dialog.ShowDialog<bool>(window);
        if (added && dialog.SelectedCyberware != null)
        {
            var item = dialog.SelectedCyberware;
            var viewModel = dialog.ViewModel;
            _character.AddCyberware(item.Name, item.Category, item.Rating, viewModel.FinalEssence,
                viewModel.FinalCost, viewModel.FinalAvailability, item.SourcePage, string.Empty,
                viewModel.SelectedGrade?.Name ?? "Standard", blnBioware: blnBioware);
            ViewModel.LoadCharacter(_character);
        }
    }

    private void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        // Roots is CyberwareRoot/BiowareRoot, two synthetic header nodes - actual saved items are
        // their immediate children, so only allow deleting a node one level under one of those
        // (deeper nesting would be an installed mod, not deletable this way yet).
        if (_character == null || ViewModel.SelectedNode is not { Parent: { Parent: null } parent } node)
            return;

        bool blnBioware = ReferenceEquals(parent, ViewModel.BiowareRoot);
        if (_character.RemoveCyberware(node.SourceName, node.Category, node.Rating, blnBioware))
            ViewModel.LoadCharacter(_character);
    }
}
