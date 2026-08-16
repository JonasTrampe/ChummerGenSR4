using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

public sealed class MetatypeDialogViewModel : ViewModelBase
{
    private const string NoMetavariantValue = "-";
    private readonly ObservableCollection<NewCharacterMetatype> _allMetatypes = new();
    public ObservableCollection<string> Categories { get; } = new();
    public ObservableCollection<NewCharacterMetatype> Metatypes { get; } = new();
    public ObservableCollection<string> Metavariants { get; } = new();

    private string? _selectedCategory;
    public string? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (!SetField(ref _selectedCategory, value))
                return;
            RefreshFilteredMetatypes();
        }
    }

    private NewCharacterMetatype? _selectedMetatype;
    public NewCharacterMetatype? SelectedMetatype
    {
        get => _selectedMetatype;
        set
        {
            if (!SetField(ref _selectedMetatype, value))
                return;
            RefreshMetavariants();
            OnPropertyChanged(nameof(SelectedCritterUsesForce));
        }
    }

    private string _selectedMetavariant = NoMetavariantValue;
    public string SelectedMetavariant
    {
        get => _selectedMetavariant;
        set => SetField(ref _selectedMetavariant, value);
    }

    /// <summary>True once LoadCritterMetatypes has populated the list instead of LoadMetatypes -
    /// switches the dialog's Metavariant/Magic-Resonance controls off and its Force input on.</summary>
    private bool _isCritterMode;
    public bool IsCritterMode
    {
        get => _isCritterMode;
        private set => SetField(ref _isCritterMode, value);
    }

    /// <summary>Ported from frmMetatype.cs's nudForce: only meaningful (and only shown) for
    /// Force-based critters (Spirits/Sprites) whose critters.xml attribute/skill ratings are "F"-
    /// style formulas rather than plain integers - see NewCharacterFactory.CreateCritterCharacter.</summary>
    private int _force = 1;
    public int Force
    {
        get => _force;
        set => SetField(ref _force, value);
    }

    /// <summary>Whether the selected critter's attribute ranges contain any Force ("F") formula -
    /// gates whether the Force input is actually meaningful to show.</summary>
    public bool SelectedCritterUsesForce => IsCritterMode && SelectedMetatype != null
        && SelectedMetatype.AttributeRanges.Values.Any(r => r.Min.Contains('F') || r.Max.Contains('F') || r.Aug.Contains('F'));

    public void LoadMetatypes(string strSettingsFileName = "default.xml")
    {
        IsCritterMode = false;
        var objOptions = new CharacterOptions();
        objOptions.Load(string.IsNullOrWhiteSpace(strSettingsFileName) ? "default.xml" : strSettingsFileName);
        LoadMetatypeList(NewCharacterFactory.LoadMetatypes(objOptions));
    }

    /// <summary>Ported from frmMain.cs's mnuNewCritter_Click opening frmMetatype pointed at
    /// critters.xml instead of metatypes.xml.</summary>
    public void LoadCritterMetatypes(string strSettingsFileName = "default.xml")
    {
        IsCritterMode = true;
        var objOptions = new CharacterOptions();
        objOptions.Load(string.IsNullOrWhiteSpace(strSettingsFileName) ? "default.xml" : strSettingsFileName);
        LoadMetatypeList(NewCharacterFactory.LoadCritterMetatypes(objOptions));
    }

    private void LoadMetatypeList(IEnumerable<NewCharacterMetatype> lstMetatypes)
    {
        _allMetatypes.Clear();
        Categories.Clear();
        Metatypes.Clear();
        foreach (NewCharacterMetatype objMetatype in lstMetatypes)
            _allMetatypes.Add(objMetatype);

        foreach (string strCategory in _allMetatypes.Select(x => x.CategoryLabel).Distinct())
            Categories.Add(strCategory);

        SelectedCategory = Categories.FirstOrDefault(x => x == "Metamenschen") ?? Categories.FirstOrDefault();
    }

    private void RefreshFilteredMetatypes()
    {
        string? strSelectedMetatype = SelectedMetatype?.Name;
        Metatypes.Clear();
        foreach (NewCharacterMetatype objMetatype in _allMetatypes.Where(x => x.CategoryLabel == SelectedCategory))
            Metatypes.Add(objMetatype);

        SelectedMetatype = Metatypes.FirstOrDefault(x => x.Name == strSelectedMetatype) ?? Metatypes.FirstOrDefault();
    }

    private void RefreshMetavariants()
    {
        string strPrevious = SelectedMetavariant;
        Metavariants.Clear();
        Metavariants.Add(NoMetavariantValue);

        if (SelectedMetatype != null)
        {
            foreach (NewCharacterMetavariant objMetavariant in SelectedMetatype.Metavariants)
                Metavariants.Add(objMetavariant.Name);
        }

        SelectedMetavariant = Metavariants.Contains(strPrevious) ? strPrevious : NoMetavariantValue;
    }
}
