using System.Collections.ObjectModel;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

public sealed class VehiclesSectionViewModel : ViewModelBase
{
    public ObservableCollection<TreeNodeViewModel> Vehicles { get; } = new();

    public void LoadCharacter(CharacterDocument character)
    {
        Vehicles.Clear();
        foreach (CharacterWeaponData vehicle in character.Vehicles)
            Vehicles.Add(new TreeNodeViewModel(vehicle.DisplayName));
    }
}
