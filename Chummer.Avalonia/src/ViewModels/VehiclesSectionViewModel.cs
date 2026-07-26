using System.Collections.ObjectModel;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

public sealed class VehiclesSectionViewModel : ViewModelBase
{
    public ObservableCollection<TreeNodeViewModel> Vehicles { get; } = new();

    private TreeNodeViewModel? _selectedVehicle;
    public TreeNodeViewModel? SelectedVehicle
    {
        get => _selectedVehicle;
        set => SetField(ref _selectedVehicle, value);
    }

    private string? _selectedVehicleLocation;
    public string? SelectedVehicleLocation { get => _selectedVehicleLocation; set => SetField(ref _selectedVehicleLocation, value); }

    public void LoadCharacter(CharacterDocument character)
    {
        Vehicles.Clear();
        foreach (CharacterVehicleData vehicle in character.Vehicles)
            Vehicles.Add(TreeNodeViewModel.FromVehicle(vehicle));
        SelectedVehicle = Vehicles.Count > 0 ? Vehicles[0] : null;
        SelectedVehicleLocation = null;
    }
}
