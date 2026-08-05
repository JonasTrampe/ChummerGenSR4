using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.Core;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Dialogs;

public partial class MartialArtDialog : Window
{
    public MartialArtDialogViewModel ViewModel { get; } = new();
    public MartialArtOptionViewModel? SelectedMartialArt => ViewModel.Selected;

    public MartialArtDialog()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    public MartialArtDialog(CharacterDocument character)
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
