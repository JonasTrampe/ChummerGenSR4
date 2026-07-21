namespace Chummer.NewUI.ViewModels;

/// <summary>One row of the Allgemein tab's attribute grid. Name/ShowRemove/IsEnabled are fixed
/// per attribute code (there's no such data in the save file), Base/Augmented/Range/Source come
/// from the loaded character.</summary>
public sealed class AttributeRowViewModel : ViewModelBase
{
    public string Code { get; }
    public string AttributeName { get; }
    public bool ShowRemove { get; }
    public bool IsAttributeEnabled { get; }

    private string _strBase = string.Empty;
    public string Base
    {
        get => _strBase;
        set => SetField(ref _strBase, value);
    }

    private string _strAugmented = string.Empty;
    public string Augmented
    {
        get => _strAugmented;
        set => SetField(ref _strAugmented, value);
    }

    private string _strRange = string.Empty;
    public string Range
    {
        get => _strRange;
        set => SetField(ref _strRange, value);
    }

    private string _strSource = string.Empty;
    public string Source
    {
        get => _strSource;
        set => SetField(ref _strSource, value);
    }

    public AttributeRowViewModel(string strCode, string strAttributeName, bool blnShowRemove = false,
        bool blnIsAttributeEnabled = true)
    {
        Code = strCode;
        AttributeName = strAttributeName;
        ShowRemove = blnShowRemove;
        IsAttributeEnabled = blnIsAttributeEnabled;
    }
}
