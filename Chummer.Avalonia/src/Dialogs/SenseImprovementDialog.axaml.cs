using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.Core;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Dialogs;

/// <summary>Ported from clsImprovement.cs's selectsenseware picker (frmSelectItem usage inside
/// CreateImprovements) - lets the player pick which Cyberware/Bioware/Gear item an "Improved
/// Sense"-style Adept Power's sensory bonus comes from.</summary>
public partial class SenseImprovementDialog : Window
{
    public SenseImprovementDialogViewModel ViewModel { get; } = new();
    public string? SelectedName => ViewModel.SelectedOption?.Name;

    public SenseImprovementDialog(CharacterDocument character, string strPowerName)
    {
        DataContext = ViewModel;
        InitializeComponent();
        ViewModel.LoadOptions(character, strPowerName);
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedOption != null)
            Close(true);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
