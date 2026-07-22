using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Xml;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

public sealed class OptionsDialogViewModel : ViewModelBase
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

    public void Save()
    {
        CurrentOptions.Books.Clear();
        foreach (OptionsBookItemViewModel objBook in Sourcebooks.Where(x => x.IsSelected))
            CurrentOptions.Books.Add(objBook.Code);
        if (!CurrentOptions.Books.Contains("SR4"))
            CurrentOptions.Books.Add("SR4");

        if (SelectedLanguage?.Value != null)
            GlobalOptions.Instance.Language = SelectedLanguage.Value;
        if (SelectedSheet?.Value != null)
            GlobalOptions.Instance.DefaultCharacterSheet = SelectedSheet.Value;

        CurrentOptions.Save();

        SettingsRegistryKey objRegistry = SettingsStore.CurrentUser.CreateSubKey("Software\\Chummer");
        objRegistry.SetValue("autoupdate", AutomaticUpdate.ToString());
        objRegistry.SetValue("localisedupdatesonly", LocalisedUpdatesOnly.ToString());
        objRegistry.SetValue("language", GlobalOptions.Instance.Language);
        objRegistry.SetValue("startupfullscreen", StartupFullscreen.ToString());
        objRegistry.SetValue("singlediceroller", SingleDiceRoller.ToString());
        objRegistry.SetValue("defaultsheet", GlobalOptions.Instance.DefaultCharacterSheet);
        objRegistry.SetValue("pdfargumentstyle", GlobalOptions.Instance.PdfArgumentStyle);
        objRegistry.SetValue("datesincludetime", DatesIncludeTime.ToString());
        objRegistry.SetValue("printtofilefirst", PrintToFileFirst.ToString());
        objRegistry.SetValue("pdfapppath", PdfAppPath);
        objRegistry.SetValue("cloudapibaseurl", CloudApiBaseUrl);
        objRegistry.SetValue("suppresscloudunreachablewarning", SuppressCloudUnreachableWarning.ToString());
    }

    public void RestoreBpDefaults()
    {
        CurrentOptions.BpAttribute = 10;
        CurrentOptions.BpAttributeMax = 15;
        CurrentOptions.BpContact = 1;
        CurrentOptions.BpMartialArt = 5;
        CurrentOptions.BpMartialArtManeuver = 2;
        CurrentOptions.BpSkillGroup = 10;
        CurrentOptions.BpActiveSkill = 4;
        CurrentOptions.BpActiveSkillSpecialization = 2;
        CurrentOptions.BpKnowledgeSkill = 2;
        CurrentOptions.BpSpell = 3;
        CurrentOptions.BpFocus = 1;
        CurrentOptions.BpSpirit = 1;
        CurrentOptions.BpComplexForm = 1;
        CurrentOptions.BpComplexFormOption = 1;
        OnPropertyChanged(nameof(CurrentOptions));
    }

    public void RestoreKarmaDefaults()
    {
        CurrentOptions.KarmaAttribute = 5;
        CurrentOptions.KarmaQuality = 2;
        CurrentOptions.KarmaSpecialization = 2;
        CurrentOptions.KarmaNewKnowledgeSkill = 2;
        CurrentOptions.KarmaNewActiveSkill = 4;
        CurrentOptions.KarmaNewSkillGroup = 10;
        CurrentOptions.KarmaImproveKnowledgeSkill = 1;
        CurrentOptions.KarmaImproveActiveSkill = 2;
        CurrentOptions.KarmaImproveSkillGroup = 5;
        CurrentOptions.KarmaSpell = 5;
        CurrentOptions.KarmaNewComplexForm = 2;
        CurrentOptions.KarmaImproveComplexForm = 1;
        CurrentOptions.KarmaComplexFormOption = 2;
        CurrentOptions.KarmaComplexFormSkillsoft = 1;
        CurrentOptions.KarmaNuyenPer = 2500;
        CurrentOptions.KarmaContact = 2;
        CurrentOptions.KarmaCarryover = 5;
        CurrentOptions.KarmaSpirit = 2;
        CurrentOptions.KarmaManeuver = 4;
        CurrentOptions.KarmaInitiation = 3;
        CurrentOptions.KarmaMetamagic = 15;
        CurrentOptions.KarmaJoinGroup = 5;
        CurrentOptions.KarmaLeaveGroup = 1;
        CurrentOptions.KarmaAnchoringFocus = 6;
        CurrentOptions.KarmaBanishingFocus = 3;
        CurrentOptions.KarmaBindingFocus = 3;
        CurrentOptions.KarmaCenteringFocus = 6;
        CurrentOptions.KarmaCounterspellingFocus = 3;
        CurrentOptions.KarmaDiviningFocus = 6;
        CurrentOptions.KarmaDowsingFocus = 6;
        CurrentOptions.KarmaInfusionFocus = 3;
        CurrentOptions.KarmaMaskingFocus = 6;
        CurrentOptions.KarmaPowerFocus = 6;
        CurrentOptions.KarmaShieldingFocus = 6;
        CurrentOptions.KarmaSpellcastingFocus = 4;
        CurrentOptions.KarmaSummoningFocus = 4;
        CurrentOptions.KarmaSustainingFocus = 2;
        CurrentOptions.KarmaSymbolicLinkFocus = 1;
        CurrentOptions.KarmaWeaponFocus = 3;
        OnPropertyChanged(nameof(CurrentOptions));
    }

    private void LoadSettingsProfiles()
    {
        SettingsProfiles.Clear();
        string strDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings");
        if (!Directory.Exists(strDirectory))
            return;

        foreach (string strFile in Directory.GetFiles(strDirectory, "*.xml"))
        {
            try
            {
                XmlDocument objDoc = new XmlDocument();
                objDoc.Load(strFile);
                SettingsProfiles.Add(new ListItem
                {
                    Value = Path.GetFileName(strFile),
                    Name = objDoc.SelectSingleNode("/settings/name")?.InnerText ?? Path.GetFileNameWithoutExtension(strFile)
                });
            }
            catch
            {
            }
        }

        SelectedSettingsProfile = SettingsProfiles.FirstOrDefault(x => x.Value == "default.xml") ?? SettingsProfiles.FirstOrDefault();
    }

    private void LoadLanguages()
    {
        Languages.Clear();
        string strPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "lang");
        foreach (string strFile in Directory.GetFiles(strPath, "*.xml"))
        {
            string strCode = Path.GetFileNameWithoutExtension(strFile);
            if (strCode.EndsWith("_data", StringComparison.OrdinalIgnoreCase) ||
                strCode.StartsWith("results_", StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                XmlDocument objXmlDocument = new XmlDocument();
                objXmlDocument.Load(strFile);
                string? strName = objXmlDocument.SelectSingleNode("/chummer/name")?.InnerText;
                if (string.IsNullOrWhiteSpace(strName))
                    continue;

                Languages.Add(new ListItem
                {
                    Value = strCode,
                    Name = strName
                });
            }
            catch
            {
            }
        }

        Sort(Languages);
        SelectedLanguage = Languages.FirstOrDefault(x => x.Value == GlobalOptions.Instance.Language) ??
                           Languages.FirstOrDefault(x => x.Value == "en-us") ??
                           Languages.FirstOrDefault();
    }

    private void LoadSheets()
    {
        Sheets.Clear();
        string strPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "sheets");
        if (!Directory.Exists(strPath))
            return;

        foreach (string strFile in Directory.GetFiles(strPath, "*.xsl"))
        {
            if (strFile.EndsWith(".xslt", StringComparison.OrdinalIgnoreCase))
                continue;
            string strName = Path.GetFileNameWithoutExtension(strFile);
            Sheets.Add(new ListItem { Value = strName, Name = strName });
        }

        SelectedSheet = Sheets.FirstOrDefault(x => x.Value == GlobalOptions.Instance.DefaultCharacterSheet) ?? Sheets.FirstOrDefault();
    }

    private void LoadSourcebooks()
    {
        Sourcebooks.Clear();
        XmlDocument objDocument = XmlManager.Instance.Load("books.xml");
        XmlNodeList? objBookNodes = objDocument.SelectNodes("/chummer/books/book");
        if (objBookNodes == null)
            return;

        foreach (XmlNode objBookNode in objBookNodes)
        {
            string strCode = objBookNode["code"]?.InnerText ?? string.Empty;
            if (string.IsNullOrWhiteSpace(strCode))
                continue;
            string strName = objBookNode["translate"]?.InnerText
                             ?? objBookNode["name"]?.InnerText
                             ?? strCode;
            Sourcebooks.Add(new OptionsBookItemViewModel
            {
                Code = strCode,
                DisplayName = strName,
                IsRequired = strCode == "SR4",
                IsSelected = CurrentOptions.BookEnabled(strCode) || strCode == "SR4"
            });
        }

        foreach (OptionsBookItemViewModel objBook in Sourcebooks.OrderBy(x => x.DisplayName).ToList())
        {
            Sourcebooks.Remove(objBook);
            Sourcebooks.Add(objBook);
        }
    }

    private void SyncSelectionsFromOptions()
    {
        SelectedBuildMethod = BuildMethods.FirstOrDefault(x => x.Value == CurrentOptions.BuildMethod) ?? BuildMethods.FirstOrDefault();
        SelectedEssenceDecimals = EssenceDecimals.FirstOrDefault(x => x.Value == CurrentOptions.EssenceDecimals.ToString()) ?? EssenceDecimals.FirstOrDefault();
        string strLimbKey = CurrentOptions.LimbCount == 6 ? "all" : CurrentOptions.ExcludeLimbSlot == "skull" ? "torso" : "skull";
        SelectedLimbCount = LimbCounts.FirstOrDefault(x => x.Value == strLimbKey) ?? LimbCounts.FirstOrDefault();
        OnPropertyChanged(nameof(CurrentOptions));
    }

    private void SyncSourcebookSelection()
    {
        HashSet<string> setBooks = CurrentOptions.Books.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (OptionsBookItemViewModel objBook in Sourcebooks)
            objBook.IsSelected = objBook.IsRequired || setBooks.Contains(objBook.Code);
    }

    private static void Sort(ObservableCollection<ListItem> lstItems)
    {
        List<ListItem> lstSorted = lstItems.OrderBy(x => x.Name).ToList();
        lstItems.Clear();
        foreach (ListItem objItem in lstSorted)
            lstItems.Add(objItem);
    }
}
