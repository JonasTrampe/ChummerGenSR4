using Avalonia.Controls;
using Chummer.Core;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Controls.CharacterSections;

// Split by concern into partial-class files: .Vehicle.cs (root-level Add/Sell/Copy/Paste/Delete/
// Notes/Damage) and .Equipment.cs (Mods/Weapons/Gear/Locations attached to a Vehicle). This file
// keeps the shared construction/load surface.
public partial class VehiclesSectionTab : UserControl
{
    public VehiclesSectionViewModel ViewModel { get; } = new();
    private CharacterDocument? _character;

    public VehiclesSectionTab()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    public void LoadCharacter(CharacterDocument character)
    {
        _character = character;
        ViewModel.LoadCharacter(character);
    }
}
