using System.Collections.ObjectModel;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

public sealed class SettingsProfileDialogViewModel : ViewModelBase
{
    public ObservableCollection<ListItem> SettingsProfiles { get; } = new()
    {
        new ListItem { Name = "Default Settings", Value = "default.xml" },
    };

    private ListItem? _selectedProfile;
    public ListItem? SelectedProfile
    {
        get => _selectedProfile;
        set => SetField(ref _selectedProfile, value);
    }

    public SettingsProfileDialogViewModel()
    {
        SelectedProfile = SettingsProfiles[0];
    }
}
