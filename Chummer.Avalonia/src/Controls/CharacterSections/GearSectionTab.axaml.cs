using Avalonia.Controls;
using Chummer.Core;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Controls.CharacterSections;

// Split by sub-tab into partial-class files (GearSectionTab.Lifestyle.cs, .Gear.cs, .Weapon.cs,
// .Armor.cs, .Pets.cs, .DragDrop.cs) - this file only keeps the shared construction/load surface.
public partial class GearSectionTab : UserControl
{
    private CharacterDocument? _character;

    public GearSectionViewModel ViewModel { get; } = new();

    public GearSectionTab()
    {
        DataContext = ViewModel;
        InitializeComponent();
        SetUpGearDragDrop();
        SetUpRootTreeReorderDragDrop(this.FindControl<TreeView>("WeaponsTree")!, MoveWeapon);
        SetUpRootTreeReorderDragDrop(this.FindControl<TreeView>("ArmorTree")!, MoveArmor);
    }

    public void LoadCharacter(CharacterDocument character)
    {
        _character = character;
        ViewModel.LoadCharacter(character);
    }
}
