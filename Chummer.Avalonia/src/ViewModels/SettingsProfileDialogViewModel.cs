using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

public sealed class SettingsProfileDialogViewModel : ViewModelBase
{
    public ObservableCollection<ListItem> SettingsProfiles { get; } = new()
    {
        new ListItem { Name = App.LanguageCatalog.GetString("UI_DefaultSettingsName"), Value = "default.xml" },
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
                // Use the profile's own <name> (e.g. "Darkness in the Heart of Texas") rather than
                // its raw filename (e.g. "Darkness_in_the_heart_of_texas.xml") for the label.
                string strDisplayName = strFileName;
                try
                {
                    var objOptions = new CharacterOptions();
                    objOptions.Load(strFileName);
                    if (!string.IsNullOrWhiteSpace(objOptions.Name))
                        strDisplayName = objOptions.Name;
                }
                catch
                {
                    // Fall back to the filename if the profile fails to parse.
                }

                SettingsProfiles.Add(new ListItem { Name = strDisplayName, Value = strFileName });
            }
        }

        if (SettingsProfiles.Count == 0)
            SettingsProfiles.Add(new ListItem { Name = App.LanguageCatalog.GetString("UI_DefaultSettingsName"), Value = "default.xml" });

        // "default.xml" is always the default selection, regardless of how other profiles sort
        // alphabetically ahead of it.
        SelectedProfile = SettingsProfiles.FirstOrDefault(p => string.Equals(p.Value as string, "default.xml", System.StringComparison.OrdinalIgnoreCase))
            ?? SettingsProfiles[0];
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
