using System.Collections.ObjectModel;
using System.IO;
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
        string strSettingsDirectory = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "settings");
        if (Directory.Exists(strSettingsDirectory))
        {
            SettingsProfiles.Clear();
            foreach (string strFilePath in Directory.GetFiles(strSettingsDirectory, "*.xml"))
            {
                string strFileName = Path.GetFileName(strFilePath);
                SettingsProfiles.Add(new ListItem { Name = Path.GetFileNameWithoutExtension(strFileName), Value = strFileName });
            }
        }

        if (SettingsProfiles.Count == 0)
            SettingsProfiles.Add(new ListItem { Name = "Default Settings", Value = "default.xml" });

        SelectedProfile = SettingsProfiles[0];
    }

    public CharacterOptions LoadSelectedOptions()
    {
        CharacterOptions objOptions = new CharacterOptions();
        if (SelectedProfile?.Value is string strFileName && !string.IsNullOrWhiteSpace(strFileName))
            objOptions.Load(strFileName);
        else
            objOptions.Load("default.xml");

        return objOptions;
    }
}
