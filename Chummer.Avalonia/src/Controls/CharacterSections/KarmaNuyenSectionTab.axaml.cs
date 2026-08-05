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
    private async void OnEditKarmaExpenseClick(object? sender, RoutedEventArgs e) => await EditExpenseAsync(ViewModel.SelectedKarmaExpense, "Karma-Aufwendung bearbeiten");
    private async void OnEditNuyenExpenseClick(object? sender, RoutedEventArgs e) => await EditExpenseAsync(ViewModel.SelectedNuyenExpense, "Nuyen-Aufwendung bearbeiten");

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

    private async System.Threading.Tasks.Task EditExpenseAsync(ExpenseRowViewModel? selected, string title)
    {
        if (_character == null || selected == null || string.IsNullOrEmpty(selected.Guid)
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        decimal decAmount = decimal.TryParse(selected.RawAmount, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out decimal d) ? d : 0m;

        var dialog = new ExpenseDialog(title, decAmount, selected.Reason);
        bool? edited = await dialog.ShowDialog<bool?>(window);
        if (edited != true)
            return;

        System.DateTime datDate = System.DateTime.TryParse(selected.RawDate, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind, out System.DateTime dt) ? dt : System.DateTime.Now;

        if (_character.UpdateExpense(selected.Guid, dialog.Reason, dialog.Amount, datDate))
            ViewModel.LoadCharacter(_character);
    }
}
