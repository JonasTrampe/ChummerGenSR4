#nullable enable
using System.Collections.ObjectModel;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

public sealed class CalendarSectionViewModel : ViewModelBase
{
    private CharacterDocument? _character;
    private CalendarWeekRowViewModel? _selectedWeek;
    public ObservableCollection<CalendarWeekRowViewModel> Weeks { get; } = new();
    public CalendarWeekRowViewModel? SelectedWeek { get => _selectedWeek; set => SetField(ref _selectedWeek, value); }

    public void LoadCharacter(CharacterDocument character)
    {
        _character = character;
        Weeks.Clear();
        foreach (CalendarWeek week in character.Calendar)
            Weeks.Add(new CalendarWeekRowViewModel(week));
        SelectedWeek = Weeks.Count == 0 ? null : Weeks[0];
    }

    public (int Year, int Week) GetNextWeek()
    {
        CalendarWeekRowViewModel? last = Weeks.Count == 0 ? null : Weeks[^1];
        if (last == null) return (2072, 1);
        return last.Week == 52 ? (last.Year + 1, 1) : (last.Year, last.Week + 1);
    }

    public bool AddWeek(int year, int week, string notes)
    {
        if (_character == null) return false;
        _character.AddCalendarWeek(year, week, notes);
        LoadCharacter(_character);
        return true;
    }

    public bool UpdateSelectedWeekNotes(string notes)
    {
        if (_character == null || SelectedWeek == null) return false;
        bool updated = _character.UpdateCalendarWeekNotes(SelectedWeek.Id, notes);
        if (updated) LoadCharacter(_character);
        return updated;
    }

    public bool ChangeStart(int year, int week)
    {
        if (_character == null || !_character.ChangeCalendarStart(year, week)) return false;
        LoadCharacter(_character);
        return true;
    }
}

public sealed class CalendarWeekRowViewModel
{
    public string Id { get; }
    public int Year { get; }
    public int Week { get; }
    public string Date { get; }
    public string Notes { get; }

    public CalendarWeekRowViewModel(CalendarWeek week)
    {
        Id = week.InternalId;
        Year = week.Year;
        Week = week.Week;
        Date = Year + ": Monat " + week.Month + ", Woche " + week.MonthWeek;
        Notes = week.Notes;
    }
}
