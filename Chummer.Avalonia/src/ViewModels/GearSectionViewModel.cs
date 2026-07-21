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

        Weapons.Clear();
        foreach (CharacterTreeItemData weapon in character.WeaponTrees)
            Weapons.Add(TreeNodeViewModel.FromTreeItem(weapon));

        Armor.Clear();
        foreach (CharacterTreeItemData item in character.Armor)
            Armor.Add(TreeNodeViewModel.FromTreeItem(item));

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
