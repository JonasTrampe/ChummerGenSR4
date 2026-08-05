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

    private void OnRollClick(object? sender, RoutedEventArgs e) => ViewModel.Roll();

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
