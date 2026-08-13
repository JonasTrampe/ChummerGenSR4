using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Chummer.NewUI.Dialogs;

public partial class CalendarWeekDialog : Window
{
    private readonly bool _notesOnly;
    public int Year { get; private set; }
    public int Week { get; private set; }
    public string Notes => NotesBox.Text ?? string.Empty;

    public CalendarWeekDialog()
        : this("Kalenderwoche", 2072, 1)
    {
    }

    public CalendarWeekDialog(string title, int year, int week, string notes = "", bool notesOnly = false)
    {
        _notesOnly = notesOnly;
        InitializeComponent();
        Title = title;
        YearBox.Text = year.ToString();
        WeekBox.Text = week.ToString();
        NotesBox.Text = notes;
        YearBox.IsEnabled = !notesOnly;
        WeekBox.IsEnabled = !notesOnly;
    }

    private void OnSave(object? sender, RoutedEventArgs e)
    {
        if (!int.TryParse(YearBox.Text, out int year) || !int.TryParse(WeekBox.Text, out int week) || week is < 1 or > 52)
        {
            ErrorText.Text = App.LanguageCatalog.GetString("UI_InvalidYearWeekMessage");
            return;
        }
        Year = year;
        Week = week;
        Close(true);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
