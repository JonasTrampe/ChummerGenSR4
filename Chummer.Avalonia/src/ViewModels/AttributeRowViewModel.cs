using System;

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

    /// <summary>Set by GeneralSectionViewModel while populating rows from a loaded character, so
    /// setting BaseValue during load doesn't re-trigger a write back to the character.</summary>
    public bool IsLoading { get; set; }

    /// <summary>Fired when the user edits BaseValue via the create-mode spinner (not during load).</summary>
    public event Action<AttributeRowViewModel, int>? BaseValueEdited;

    private string _strBase = string.Empty;
    public string Base
    {
        get => _strBase;
        set => SetField(ref _strBase, value);
    }

    private bool _blnIsCreateMode;
    public bool IsCreateMode
    {
        get => _blnIsCreateMode;
        set => SetField(ref _blnIsCreateMode, value);
    }

    private int _intBaseValue;
    public int BaseValue
    {
        get => _intBaseValue;
        set
        {
            if (!SetField(ref _intBaseValue, value))
                return;
            if (!IsLoading)
                BaseValueEdited?.Invoke(this, value);
        }
    }

    private int _intMinValue;
    public int MinValue
    {
        get => _intMinValue;
        set => SetField(ref _intMinValue, value);
    }

    private int _intMaxValue = 6;
    public int MaxValue
    {
        get => _intMaxValue;
        set => SetField(ref _intMaxValue, value);
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
