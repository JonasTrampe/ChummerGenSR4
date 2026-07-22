using System.Collections.ObjectModel;
using System.Globalization;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

public sealed class GearSectionViewModel : ViewModelBase
{
    public ObservableCollection<TreeNodeViewModel> Gear { get; } = new();
    public ObservableCollection<TreeNodeViewModel> Weapons { get; } = new();
    public ObservableCollection<TreeNodeViewModel> Armor { get; } = new();
    public ObservableCollection<string> Lifestyles { get; } = new();

    private TreeNodeViewModel? _selectedGear;
    public TreeNodeViewModel? SelectedGear
    {
        get => _selectedGear;
        set => SetField(ref _selectedGear, value);
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

    public void LoadCharacter(CharacterDocument character)
    {
        Gear.Clear();
        foreach (CharacterTreeItemData item in character.Gear)
            Gear.Add(TreeNodeViewModel.FromTreeItem(item));
        SelectedGear = Gear.Count > 0 ? Gear[0] : null;

        Weapons.Clear();
        foreach (CharacterTreeItemData weapon in character.WeaponTrees)
            Weapons.Add(TreeNodeViewModel.FromTreeItem(weapon));
        SelectedWeapon = Weapons.Count > 0 ? Weapons[0] : null;

        Armor.Clear();
        foreach (CharacterTreeItemData item in character.Armor)
            Armor.Add(TreeNodeViewModel.FromTreeItem(item));
        SelectedArmor = Armor.Count > 0 ? Armor[0] : null;

        Lifestyles.Clear();
        decimal decTotalCost = 0;
        foreach (CharacterLifestyleData lifestyle in character.Lifestyles)
        {
            Lifestyles.Add(lifestyle.Name);
            if (decimal.TryParse(lifestyle.Cost, out var decCost))
                decTotalCost += decCost;
        }

        LifestyleCost = "Kosten/Monat: " + decTotalCost.ToString("N0", CultureInfo.InvariantCulture) + "¥";
    }
}
