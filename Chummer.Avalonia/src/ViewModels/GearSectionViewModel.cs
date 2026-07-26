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
    private bool _blnIsLoadingGearQuantity;

    public ObservableCollection<TreeNodeViewModel> Gear { get; } = new();
    public ObservableCollection<TreeNodeViewModel> Weapons { get; } = new();
    public ObservableCollection<TreeNodeViewModel> Armor { get; } = new();
    public ObservableCollection<string> ArmorCategories { get; } = new();
    public ObservableCollection<string> Lifestyles { get; } = new();
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
    public TreeNodeViewModel? SelectedArmor
    {
        get => _selectedArmor;
        set => SetField(ref _selectedArmor, value);
    }

    private TreeNodeViewModel? _selectedWeapon;
    public TreeNodeViewModel? SelectedWeapon
    {
        get => _selectedWeapon;
        set => SetField(ref _selectedWeapon, value);
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
        Gear.Clear();
        foreach (CharacterTreeItemData item in character.Gear)
            Gear.Add(TreeNodeViewModel.FromTreeItem(item));
        SelectedGear = Gear.Count > 0 ? Gear[0] : null;

        Weapons.Clear();
        foreach (CharacterTreeItemData weapon in character.WeaponTrees)
            Weapons.Add(TreeNodeViewModel.FromTreeItem(weapon));
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

        CharacterEncumbranceData encumbrance = character.ArmorEncumbrance;
        ArmorRating = "Panzerungswert: Ballistisch " + encumbrance.BallisticRating.Value
            + " / Stoß " + encumbrance.ImpactRating.Value;
        ArmorEncumbrance = "Behinderung: Ballistisch " + encumbrance.BallisticPenalty.Value
            + " / Stoß " + encumbrance.ImpactPenalty.Value;

        Lifestyles.Clear();
        decimal decTotalCost = 0;
        foreach (CharacterLifestyleData lifestyle in character.Lifestyles)
        {
            Lifestyles.Add(lifestyle.Name);
            if (decimal.TryParse(lifestyle.Cost, out var decCost))
                decTotalCost += decCost;
        }

        LifestyleCost = "Kosten/Monat: " + decTotalCost.ToString("N0", CultureInfo.InvariantCulture) + "¥";

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
}
