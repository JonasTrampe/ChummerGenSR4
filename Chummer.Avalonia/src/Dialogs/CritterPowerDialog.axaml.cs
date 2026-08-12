using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.Core;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Dialogs;

public partial class CritterPowerDialog : Window
{
    public CritterPowerDialogViewModel ViewModel { get; } = new();
    public CritterPowerOptionViewModel? SelectedPower => ViewModel.Selected;
    public string SelectedRating => ViewModel.SelectedRating.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public CritterPowerDialog()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    public CritterPowerDialog(CharacterDocument character)
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
