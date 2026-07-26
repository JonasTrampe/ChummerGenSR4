using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Chummer.Core;
using Chummer.NewUI.Dialogs;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Controls.CharacterSections;

public partial class CalendarTab : UserControl
{
    public CalendarSectionViewModel ViewModel { get; } = new();

    public CalendarTab()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    public void LoadCharacter(CharacterDocument character) => ViewModel.LoadCharacter(character);

    private async void OnAddWeekClick(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not Window window) return;
        var next = ViewModel.GetNextWeek();
        var dialog = new CalendarWeekDialog("Kalenderwoche hinzufügen", next.Year, next.Week);
        if (await dialog.ShowDialog<bool>(window)) ViewModel.AddWeek(dialog.Year, dialog.Week, dialog.Notes);
    }

    private async void OnEditWeekClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedWeek == null || TopLevel.GetTopLevel(this) is not Window window) return;
        var selected = ViewModel.SelectedWeek;
        var dialog = new CalendarWeekDialog("Kalenderwoche bearbeiten", selected.Year, selected.Week, selected.Notes, true);
        if (await dialog.ShowDialog<bool>(window)) ViewModel.UpdateSelectedWeekNotes(dialog.Notes);
    }

    private async void OnChangeStartClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedWeek == null || TopLevel.GetTopLevel(this) is not Window window) return;
        var selected = ViewModel.Weeks.Count == 0 ? ViewModel.SelectedWeek : ViewModel.Weeks[0];
        var dialog = new CalendarWeekDialog("Kalenderstart ändern", selected.Year, selected.Week);
        if (await dialog.ShowDialog<bool>(window)) ViewModel.ChangeStart(dialog.Year, dialog.Week);
    }
}
