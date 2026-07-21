using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Chummer.Core;
using Chummer.NewUI.Dialogs;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Controls.CharacterSections;

public partial class KarmaNuyenSectionTab : UserControl
{
    private CharacterDocument? _character;
    public KarmaNuyenSectionViewModel ViewModel { get; } = new();

    public KarmaNuyenSectionTab()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    public void LoadCharacter(CharacterDocument character)
    {
        _character = character;
        ViewModel.LoadCharacter(character);
    }

    private async void OnKarmaEarnedClick(object? sender, RoutedEventArgs e) => await AddExpenseAsync("Karma", "Karma verdient", 1);
    private async void OnKarmaSpentClick(object? sender, RoutedEventArgs e) => await AddExpenseAsync("Karma", "Karma ausgegeben", -1);
    private async void OnNuyenEarnedClick(object? sender, RoutedEventArgs e) => await AddExpenseAsync("Nuyen", "Nuyen verdient", 1);
    private async void OnNuyenSpentClick(object? sender, RoutedEventArgs e) => await AddExpenseAsync("Nuyen", "Nuyen ausgegeben", -1);

    private async System.Threading.Tasks.Task AddExpenseAsync(string type, string title, int sign)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new ExpenseDialog(title, sign);
        bool? added = await dialog.ShowDialog<bool?>(window);
        if (added == true)
        {
            _character.AddExpense(type, dialog.Amount, dialog.Reason);
            ViewModel.LoadCharacter(_character);
        }
    }
}
