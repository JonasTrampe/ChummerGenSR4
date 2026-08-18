using System.Collections.ObjectModel;
using System.Linq;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

// Split by concern into partial-class files: .Loading.cs (populating the picker lists from disk/
// XML and syncing selections), .Defaults.cs (BP/Karma cost reset buttons), .Save.cs (persisting
// CurrentOptions + GlobalOptions to the registry). This file keeps the constructor and all
// bindable properties.
public sealed partial class OptionsDialogViewModel : ViewModelBase
{
    private CharacterOptions _currentOptions = new CharacterOptions();
    private ListItem? _selectedSettingsProfile;
    private ListItem? _selectedLanguage;
    private ListItem? _selectedSheet;
    private ListItem? _selectedBuildMethod;
    private ListItem? _selectedEssenceDecimals;
    private ListItem? _selectedLimbCount;

    public ObservableCollection<ListItem> SettingsProfiles { get; } = new();
    public ObservableCollection<ListItem> Languages { get; } = new();
    public ObservableCollection<ListItem> Sheets { get; } = new();
    public ObservableCollection<ListItem> BuildMethods { get; } = new();
    public ObservableCollection<ListItem> EssenceDecimals { get; } = new();
    public ObservableCollection<ListItem> LimbCounts { get; } = new();
    public ObservableCollection<ListItem> PdfArgumentStyles { get; } = new();
    public ObservableCollection<OptionsBookItemViewModel> Sourcebooks { get; } = new();

    public CharacterOptions CurrentOptions
    {
        get => _currentOptions;
        private set
        {
            _currentOptions = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SettingsProfileName));
            SyncSelectionsFromOptions();
            SyncSourcebookSelection();
        }
    }

    public ListItem? SelectedSettingsProfile
    {
        get => _selectedSettingsProfile;
        set
        {
            if (!SetField(ref _selectedSettingsProfile, value))
                return;

            if (value?.Value == null)
                return;

            CharacterOptions objOptions = new CharacterOptions();
            objOptions.Load(value.Value);
            CurrentOptions = objOptions;
        }
    }

    public string SettingsProfileName
    {
        get => CurrentOptions.Name;
        set
        {
            CurrentOptions.Name = value;
            OnPropertyChanged();
        }
    }

    public ListItem? SelectedLanguage
    {
        get => _selectedLanguage;
        set => SetField(ref _selectedLanguage, value);
    }

    public ListItem? SelectedSheet
    {
        get => _selectedSheet;
        set => SetField(ref _selectedSheet, value);
    }

    public ListItem? SelectedBuildMethod
    {
        get => _selectedBuildMethod;
        set
        {
            if (!SetField(ref _selectedBuildMethod, value))
                return;

            if (value?.Value == null)
                return;

            CurrentOptions.BuildMethod = value.Value;
            if (value.Value == "BP")
                CurrentOptions.BuildPoints = 400;
            else if (CurrentOptions.BuildPoints == 400 || CurrentOptions.BuildPoints == 0)
                CurrentOptions.BuildPoints = 750;
            OnPropertyChanged(nameof(CurrentOptions));
        }
    }

    public ListItem? SelectedEssenceDecimals
    {
        get => _selectedEssenceDecimals;
        set
        {
            if (!SetField(ref _selectedEssenceDecimals, value))
                return;
            if (value?.Value != null && int.TryParse(value.Value, out int intValue))
            {
                CurrentOptions.EssenceDecimals = intValue;
                OnPropertyChanged(nameof(CurrentOptions));
            }
        }
    }

    public ListItem? SelectedLimbCount
    {
        get => _selectedLimbCount;
        set
        {
            if (!SetField(ref _selectedLimbCount, value))
                return;
            if (value?.Value == null)
                return;

            switch (value.Value)
            {
                case "torso":
                    CurrentOptions.LimbCount = 5;
                    CurrentOptions.ExcludeLimbSlot = "skull";
                    break;
                case "skull":
                    CurrentOptions.LimbCount = 5;
                    CurrentOptions.ExcludeLimbSlot = "torso";
                    break;
                default:
                    CurrentOptions.LimbCount = 6;
                    CurrentOptions.ExcludeLimbSlot = string.Empty;
                    break;
            }

            OnPropertyChanged(nameof(CurrentOptions));
        }
    }

    public bool AutomaticUpdate
    {
        get => GlobalOptions.Instance.AutomaticUpdate;
        set
        {
            GlobalOptions.Instance.AutomaticUpdate = value;
            OnPropertyChanged();
        }
    }

    public bool LocalisedUpdatesOnly
    {
        get => GlobalOptions.Instance.LocalisedUpdatesOnly;
        set
        {
            GlobalOptions.Instance.LocalisedUpdatesOnly = value;
            OnPropertyChanged();
        }
    }

    public bool StartupFullscreen
    {
        get => GlobalOptions.Instance.StartupFullscreen;
        set
        {
            GlobalOptions.Instance.StartupFullscreen = value;
            OnPropertyChanged();
        }
    }

    public bool SingleDiceRoller
    {
        get => GlobalOptions.Instance.SingleDiceRoller;
        set
        {
            GlobalOptions.Instance.SingleDiceRoller = value;
            OnPropertyChanged();
        }
    }

    public bool DatesIncludeTime
    {
        get => GlobalOptions.Instance.DatesIncludeTime;
        set
        {
            GlobalOptions.Instance.DatesIncludeTime = value;
            OnPropertyChanged();
        }
    }

    public bool PrintToFileFirst
    {
        get => GlobalOptions.Instance.PrintToFileFirst;
        set
        {
            GlobalOptions.Instance.PrintToFileFirst = value;
            OnPropertyChanged();
        }
    }

    public string PdfAppPath
    {
        get => GlobalOptions.Instance.PdfAppPath;
        set
        {
            GlobalOptions.Instance.PdfAppPath = value;
            OnPropertyChanged();
        }
    }

    public string CloudApiBaseUrl
    {
        get => GlobalOptions.Instance.CloudApiBaseUrl;
        set
        {
            GlobalOptions.Instance.CloudApiBaseUrl = value;
            OnPropertyChanged();
        }
    }

    public bool SuppressCloudUnreachableWarning
    {
        get => GlobalOptions.Instance.SuppressCloudUnreachableWarning;
        set
        {
            GlobalOptions.Instance.SuppressCloudUnreachableWarning = value;
            OnPropertyChanged();
        }
    }

    public ListItem? SelectedPdfArgumentStyle
    {
        get => PdfArgumentStyles.FirstOrDefault(x => x.Value == GlobalOptions.Instance.PdfArgumentStyle);
        set
        {
            if (value?.Value == null)
                return;
            GlobalOptions.Instance.PdfArgumentStyle = value.Value;
            OnPropertyChanged();
        }
    }

    public bool CanVerifyLanguage => SelectedLanguage?.Value != "en-us";

    public OptionsDialogViewModel()
    {
        BuildMethods.Add(new ListItem { Name = App.LanguageCatalog.GetString("String_BP"), Value = "BP" });
        BuildMethods.Add(new ListItem { Name = App.LanguageCatalog.GetString("String_Karma"), Value = "Karma" });

        EssenceDecimals.Add(new ListItem { Name = "2", Value = "2" });
        EssenceDecimals.Add(new ListItem { Name = "4", Value = "4" });

        LimbCounts.Add(new ListItem { Name = "6 (2 Arme, 2 Beine, Torso, Schädel)", Value = "all" });
        LimbCounts.Add(new ListItem { Name = "5 (ohne Schädel)", Value = "skull" });
        LimbCounts.Add(new ListItem { Name = "5 (ohne Torso)", Value = "torso" });

        PdfArgumentStyles.Add(new ListItem { Name = "Adobe/Foxit", Value = "Adobe/Foxit" });
        PdfArgumentStyles.Add(new ListItem { Name = "SumatraPDF", Value = "SumatraPDF" });

        LoadSettingsProfiles();
        LoadLanguages();
        LoadSheets();
        LoadSourcebooks();

        SyncSelectionsFromOptions();
        SyncSourcebookSelection();
    }
}
