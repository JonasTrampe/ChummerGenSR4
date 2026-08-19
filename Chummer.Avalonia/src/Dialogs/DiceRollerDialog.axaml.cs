using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Dialogs;

public partial class DiceRollerDialog : Window
{
    public DiceRollerDialogViewModel ViewModel { get; } = new();

    public DiceRollerDialog()
    {
        DataContext = ViewModel;
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }

    /// <summary>Opens the generic roller prefilled with an already calculated character pool.</summary>
    public DiceRollerDialog(int intDiceCount) : this()
    {
        ViewModel.DiceCount = intDiceCount;
    }

    private void OnRollClick(object? sender, RoutedEventArgs e) => ViewModel.Roll();

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
