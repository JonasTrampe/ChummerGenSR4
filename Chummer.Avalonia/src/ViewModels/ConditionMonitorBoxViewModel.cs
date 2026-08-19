namespace Chummer.NewUI.ViewModels;

public sealed class ConditionMonitorBoxViewModel
{
    internal ConditionMonitorBoxViewModel(bool blnFilled, int intPosition)
    {
        IsFilled = blnFilled;
        Tooltip = App.LanguageCatalog.GetString("UI_ConditionMonitorBoxTooltip") + intPosition + (blnFilled ? App.LanguageCatalog.GetString("UI_ConditionMonitorBoxDamaged") : App.LanguageCatalog.GetString("UI_ConditionMonitorBoxFree"));
    }

    public bool IsFilled { get; }
    public string Tooltip { get; }
    public string Background => IsFilled ? "#B64C4C" : "White";
}
