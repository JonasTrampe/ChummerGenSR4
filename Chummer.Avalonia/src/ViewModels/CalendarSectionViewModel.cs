#nullable enable
using System.Collections.ObjectModel;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

public sealed class CalendarSectionViewModel : ViewModelBase
{
    public ObservableCollection<CalendarWeekRowViewModel> Weeks { get; } = new();

    public void LoadCharacter(CharacterDocument character)
    {
        Weeks.Clear();
        foreach (CalendarWeek week in character.Calendar)
            Weeks.Add(new CalendarWeekRowViewModel(week));
    }
}

public sealed class CalendarWeekRowViewModel
{
    public string Date { get; }
    public string Notes { get; }

    public CalendarWeekRowViewModel(CalendarWeek week)
    {
        Date = week.Year + ": Monat " + week.Month + ", Woche " + week.MonthWeek;
        Notes = week.Notes;
    }
}
