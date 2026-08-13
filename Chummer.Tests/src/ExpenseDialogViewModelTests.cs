using System;
using Chummer.Core;
using Chummer.NewUI.ViewModels;
using Xunit;

namespace Chummer.Tests;

public class ExpenseDialogViewModelTests
{
    [Fact]
    public void DatesIncludeTime_ControlsWhetherExpenseSelectionPersistsATime()
    {
        bool blnPrevious = GlobalOptions.Instance.DatesIncludeTime;
        try
        {
            var datValue = new DateTime(2026, 8, 13, 14, 37, 0);

            GlobalOptions.Instance.DatesIncludeTime = true;
            var withTime = new ExpenseDialogViewModel(datValue);
            Assert.True(withTime.IncludeTime);
            Assert.Equal(datValue, withTime.SelectedDateTime);

            GlobalOptions.Instance.DatesIncludeTime = false;
            var dateOnly = new ExpenseDialogViewModel(datValue);
            Assert.False(dateOnly.IncludeTime);
            Assert.Equal(datValue.Date, dateOnly.SelectedDateTime);
        }
        finally
        {
            GlobalOptions.Instance.DatesIncludeTime = blnPrevious;
        }
    }
}
