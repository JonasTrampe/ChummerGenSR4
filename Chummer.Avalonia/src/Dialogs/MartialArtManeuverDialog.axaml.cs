using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.Core;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Dialogs;

public partial class MartialArtManeuverDialog : Window
{
    public MartialArtManeuverDialogViewModel ViewModel { get; } = new();
    public MartialArtManeuverOptionViewModel? SelectedManeuver => ViewModel.Selected;

    public MartialArtManeuverDialog()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    public MartialArtManeuverDialog(CharacterDocument character)
        : this()
    {
        ViewModel.LoadOptions(character);
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.Selected != null)
            Close(true);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
