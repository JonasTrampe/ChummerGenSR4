using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Chummer.Core;

namespace Chummer.NewUI.Controls.CharacterSections;

public partial class VehiclesSectionTab : UserControl
{
    public VehiclesSectionTab()
    {
        InitializeComponent();
    }

    public void LoadCharacter(CharacterDocument character)
    {
        var vehiclesTree = this.FindControl<TreeView>("VehiclesTree")!;
        vehiclesTree.Items.Clear();
        foreach (CharacterWeaponData vehicle in character.Vehicles)
            vehiclesTree.Items.Add(new TreeViewItem { Header = vehicle.DisplayName });
    }
}
