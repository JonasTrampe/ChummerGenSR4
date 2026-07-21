namespace Chummer.NewUI.ViewModels;

/// <summary>Bindable Value+Tooltip pair for an InfoRow, backed by a
/// Chummer.Core.CharacterDerivedValueData or CharacterInitiativeData.</summary>
public sealed class DerivedValueViewModel : ViewModelBase
{
    private string _strValue = string.Empty;
    public string Value
    {
        get => _strValue;
        set => SetField(ref _strValue, value);
    }

    private string _strTooltip = string.Empty;
    public string Tooltip
    {
        get => _strTooltip;
        set => SetField(ref _strTooltip, value);
    }
}
