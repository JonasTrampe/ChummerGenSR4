using System;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

/// <summary>Bindable state for manually adding or editing a Karma/Nuyen expense. The time picker
/// follows the global DatesIncludeTime option, while the saved value remains an ISO date/time.</summary>
public sealed class ExpenseDialogViewModel : ViewModelBase
{
    private string _strPrompt = string.Empty;
    public string Prompt
    {
        get => _strPrompt;
        set => SetField(ref _strPrompt, value);
    }

    private string _strAmountText = "1";
    public string AmountText
    {
        get => _strAmountText;
        set => SetField(ref _strAmountText, value);
    }

    private string _strReason = string.Empty;
    public string Reason
    {
        get => _strReason;
        set => SetField(ref _strReason, value);
    }

    private DateTimeOffset? _datSelectedDate = DateTimeOffset.Now;
    public DateTimeOffset? SelectedDate
    {
        get => _datSelectedDate;
        set => SetField(ref _datSelectedDate, value);
    }

    private TimeSpan? _timSelectedTime = DateTime.Now.TimeOfDay;
    public TimeSpan? SelectedTime
    {
        get => _timSelectedTime;
        set => SetField(ref _timSelectedTime, value);
    }

    public bool IncludeTime { get; }

    private string _strError = string.Empty;
    public string Error
    {
        get => _strError;
        set => SetField(ref _strError, value);
    }

    public ExpenseDialogViewModel(DateTime? datInitialDate = null)
    {
        DateTime datValue = datInitialDate ?? DateTime.Now;
        IncludeTime = GlobalOptions.Instance.DatesIncludeTime;
        SelectedDate = new DateTimeOffset(datValue.Date);
        SelectedTime = datValue.TimeOfDay;
    }

    /// <summary>Normalizes the user selection for persisted expense XML. With time disabled,
    /// the date is saved at midnight so toggling the option later cannot reveal a stale time.</summary>
    public DateTime SelectedDateTime
    {
        get
        {
            DateTime datDate = SelectedDate?.LocalDateTime.Date ?? DateTime.Today;
            return IncludeTime ? datDate.Add(SelectedTime ?? TimeSpan.Zero) : datDate;
        }
    }
}
