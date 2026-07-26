using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

public sealed class GearSectionViewModel : ViewModelBase
{
    private CharacterDocument? _character;
    private List<CharacterTreeItemData> _lstAllArmor = new();
    private List<CharacterTreeItemData> _lstAllGear = new();
    private bool _blnIsLoadingGearQuantity;

    public ObservableCollection<TreeNodeViewModel> Gear { get; } = new();
    private bool _blnShowOnlyCommlinks;
    public bool ShowOnlyCommlinks { get => _blnShowOnlyCommlinks; set { if (SetField(ref _blnShowOnlyCommlinks, value)) ApplyGearFilter(); } }
    public ObservableCollection<TreeNodeViewModel> Weapons { get; } = new();
    public ObservableCollection<string> WeaponLocations { get; } = new();
    public ObservableCollection<TreeNodeViewModel> Armor { get; } = new();
    public ObservableCollection<string> ArmorCategories { get; } = new();
    public ObservableCollection<string> ArmorSets { get; } = new();
    public ObservableCollection<LifestyleRowViewModel> Lifestyles { get; } = new();
    private LifestyleRowViewModel? _selectedLifestyle;
    public LifestyleRowViewModel? SelectedLifestyle { get => _selectedLifestyle; set => SetField(ref _selectedLifestyle, value); }
    public ObservableCollection<ContactRowViewModel> Pets { get; } = new();
    private ContactRowViewModel? _selectedPet;
    public ContactRowViewModel? SelectedPet { get => _selectedPet; set => SetField(ref _selectedPet, value); }

    private string? _strSelectedArmorCategory;
    public string? SelectedArmorCategory
    {
        get => _strSelectedArmorCategory;
        set
        {
            if (!SetField(ref _strSelectedArmorCategory, value))
                return;
            ApplyArmorFilter();
        }
    }

    private TreeNodeViewModel? _selectedGear;
    public TreeNodeViewModel? SelectedGear
    {
        get => _selectedGear;
        set
        {
            if (!SetField(ref _selectedGear, value))
                return;

            _blnIsLoadingGearQuantity = true;
            SelectedGearQuantity = int.TryParse(value?.Qty, out int intQty) ? intQty : 1;
            _blnIsLoadingGearQuantity = false;
        }
    }

    private int _intSelectedGearQuantity = 1;
    public int SelectedGearQuantity
    {
        get => _intSelectedGearQuantity;
        set
        {
            if (!SetField(ref _intSelectedGearQuantity, value))
                return;
            if (_blnIsLoadingGearQuantity || _character == null || SelectedGear is not { GearId: >= 0 } node)
                return;

            if (_character.SetGearQuantity(node.GearId, value.ToString()))
                LoadCharacter(_character);
        }
    }

    private TreeNodeViewModel? _selectedArmor;
    private string _strSelectedArmorSet = "Kein Set";
    private bool _blnIsLoadingArmorSet;
    public TreeNodeViewModel? SelectedArmor
    {
        get => _selectedArmor;
        set
        {
            if (!SetField(ref _selectedArmor, value)) return;
            _blnIsLoadingArmorSet = true;
            SelectedArmorSet = value is { Category: not "Armor set" } ?
                (string.IsNullOrEmpty(value.ArmorSetName) ? "Kein Set" : value.ArmorSetName) : "Kein Set";
            _blnIsLoadingArmorSet = false;
            OnPropertyChanged(nameof(IsArmorSetSelected));
        }
    }

    public bool IsArmorSetSelected => SelectedArmor?.Category == "Armor set";

    public string SelectedArmorSet
    {
        get => _strSelectedArmorSet;
        set
        {
            if (!SetField(ref _strSelectedArmorSet, value) || _blnIsLoadingArmorSet || _character == null
                || SelectedArmor is not { Category: not "Armor set" } armor) return;
            _character.SetArmorSet(armor.SourceName, armor.Category, value == "Kein Set" ? string.Empty : value);
            LoadCharacter(_character);
        }
    }

    private TreeNodeViewModel? _selectedWeapon;
    private string _strSelectedWeaponLocation = "Kein Ort";
    private bool _blnIsLoadingWeaponLocation;
    public TreeNodeViewModel? SelectedWeapon
    {
        get => _selectedWeapon;
        set
        {
            if (!SetField(ref _selectedWeapon, value)) return;
            _blnIsLoadingWeaponLocation = true;
            SelectedWeaponLocation = value is { Category: not "Weapon location" } ?
                (string.IsNullOrEmpty(value.Location) ? "Kein Ort" : value.Location) : "Kein Ort";
            _blnIsLoadingWeaponLocation = false;
            OnPropertyChanged(nameof(IsWeaponLocationSelected));
        }
    }
    public bool IsWeaponLocationSelected => SelectedWeapon?.Category == "Weapon location";
    public string SelectedWeaponLocation
    {
        get => _strSelectedWeaponLocation;
        set
        {
            if (!SetField(ref _strSelectedWeaponLocation, value) || _blnIsLoadingWeaponLocation || _character == null
                || SelectedWeapon is not { Category: not "Weapon location" } weapon) return;
            _character.SetWeaponLocation(weapon.SourceName, weapon.Category, value == "Kein Ort" ? string.Empty : value);
            LoadCharacter(_character);
        }
    }

    private string _strLifestyleCost = "Kosten/Monat:";
    public string LifestyleCost
    {
        get => _strLifestyleCost;
        set => SetField(ref _strLifestyleCost, value);
    }

    private string _strArmorRating = string.Empty;
    public string ArmorRating
    {
        get => _strArmorRating;
        set => SetField(ref _strArmorRating, value);
    }

    private string _strArmorEncumbrance = string.Empty;
    public string ArmorEncumbrance
    {
        get => _strArmorEncumbrance;
        set => SetField(ref _strArmorEncumbrance, value);
    }

    public void LoadCharacter(CharacterDocument character)
    {
        _character = character;
        _lstAllGear = character.Gear.ToList();
        ApplyGearFilter();

        Weapons.Clear();
        foreach (CharacterTreeItemData weapon in character.WeaponTrees)
            Weapons.Add(TreeNodeViewModel.FromTreeItem(weapon));
        WeaponLocations.Clear();
        WeaponLocations.Add("Kein Ort");
        foreach (string strLocation in character.WeaponLocations) WeaponLocations.Add(strLocation);
        SelectedWeapon = Weapons.Count > 0 ? Weapons[0] : null;

        _lstAllArmor = character.Armor.ToList();
        ArmorCategories.Clear();
        ArmorCategories.Add("Alle");
        foreach (string strCategory in _lstAllArmor.Select(a => a.Category).Where(c => !string.IsNullOrEmpty(c))
                     .Distinct().OrderBy(c => c))
            ArmorCategories.Add(strCategory);
        _strSelectedArmorCategory = "Alle";
        OnPropertyChanged(nameof(SelectedArmorCategory));
        ApplyArmorFilter();

        ArmorSets.Clear();
        ArmorSets.Add("Kein Set");
        foreach (string strSetName in character.ArmorSets)
            ArmorSets.Add(strSetName);

        CharacterEncumbranceData encumbrance = character.ArmorEncumbrance;
        ArmorRating = "Panzerungswert: Ballistisch " + encumbrance.BallisticRating.Value
            + " / Stoß " + encumbrance.ImpactRating.Value;
        ArmorEncumbrance = "Behinderung: Ballistisch " + encumbrance.BallisticPenalty.Value
            + " / Stoß " + encumbrance.ImpactPenalty.Value;

        Lifestyles.Clear();
        decimal decTotalCost = 0;
        foreach (CharacterLifestyleData lifestyle in character.Lifestyles)
        {
            Lifestyles.Add(new LifestyleRowViewModel(lifestyle));
            if (decimal.TryParse(lifestyle.Cost, out var decCost))
                decTotalCost += decCost;
        }

        LifestyleCost = "Kosten/Monat: " + decTotalCost.ToString("N0", CultureInfo.InvariantCulture) + "¥";
        SelectedLifestyle = Lifestyles.Count > 0 ? Lifestyles[0] : null;

        Pets.Clear();
        foreach (CharacterContactData pet in character.Pets)
            Pets.Add(new ContactRowViewModel(character, pet));
        SelectedPet = Pets.Count > 0 ? Pets[0] : null;
    }

    private void ApplyArmorFilter()
    {
        Armor.Clear();
        IEnumerable<CharacterTreeItemData> query = _lstAllArmor;
        if (!string.IsNullOrEmpty(SelectedArmorCategory) && SelectedArmorCategory != "Alle")
            query = query.Where(a => a.Category == SelectedArmorCategory);

        foreach (CharacterTreeItemData item in query)
            Armor.Add(TreeNodeViewModel.FromTreeItem(item));
        SelectedArmor = Armor.Count > 0 ? Armor[0] : null;
    }

    private void ApplyGearFilter()
    {
        Gear.Clear();
        IEnumerable<CharacterTreeItemData> query = _lstAllGear;
        if (ShowOnlyCommlinks) query = query.Where(item => item.Category == "Commlink");
        foreach (CharacterTreeItemData item in query) Gear.Add(TreeNodeViewModel.FromTreeItem(item));
        SelectedGear = Gear.Count > 0 ? Gear[0] : null;
    }
}

public sealed class LifestyleRowViewModel
{
    public LifestyleRowViewModel(CharacterLifestyleData lifestyle)
    {
        Name = lifestyle.Name;
        Cost = lifestyle.Cost;
        Months = lifestyle.Months;
        DisplayName = Name + " (" + Cost + "¥/Monat)";
    }
    public string Name { get; }
    public string Cost { get; }
    public string Months { get; }
    public string DisplayName { get; }
}
