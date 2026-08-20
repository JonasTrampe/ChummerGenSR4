using System;
using System.Collections.ObjectModel;
using System.Text;
using Chummer.Core;
using Chummer.NewUI.Controls;

namespace Chummer.NewUI.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private OpenCharacterTab? _selectedOpenCharacter;
    private string _strKarmaStatus = App.LanguageCatalog.GetString("UI_KarmaStatusDefault");
    private string _strCreationBudgetTooltip = string.Empty;
    private string _strEssenceStatus = App.LanguageCatalog.GetString("UI_EssenceStatusDefault");
    private string _strNuyenStatus = App.LanguageCatalog.GetString("UI_NuyenStatusDefault");
    private string _strErrorMessage = string.Empty;
    private string _strWindowTitle = "Chummer";

    public ObservableCollection<OpenCharacterTab> OpenCharacters { get; } = new();
    public ObservableCollection<RecentCharacterEntryViewModel> RecentCharacters { get; } = new();

    public OpenCharacterTab? SelectedOpenCharacter
    {
        get => _selectedOpenCharacter;
        set
        {
            CharacterDocument? previousCharacter = _selectedOpenCharacter?.Character;
            if (!SetField(ref _selectedOpenCharacter, value))
                return;

            if (previousCharacter != null)
                previousCharacter.Changed -= OnActiveCharacterChanged;
            if (value?.Character != null)
                value.Character.Changed += OnActiveCharacterChanged;

            ActivateCharacter(value?.Character);
        }
    }

    public string KarmaStatus
    {
        get => _strKarmaStatus;
        private set => SetField(ref _strKarmaStatus, value);
    }

    /// <summary>Creation-only point breakdown shown by the status-bar budget tracker.</summary>
    public string CreationBudgetTooltip
    {
        get => _strCreationBudgetTooltip;
        private set => SetField(ref _strCreationBudgetTooltip, value);
    }

    public string EssenceStatus
    {
        get => _strEssenceStatus;
        private set => SetField(ref _strEssenceStatus, value);
    }

    public string NuyenStatus
    {
        get => _strNuyenStatus;
        private set => SetField(ref _strNuyenStatus, value);
    }

    public string ErrorMessage
    {
        get => _strErrorMessage;
        private set
        {
            if (!SetField(ref _strErrorMessage, value))
                return;

            OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public string WindowTitle
    {
        get => _strWindowTitle;
        private set => SetField(ref _strWindowTitle, value);
    }

    public bool HasRecentCharacters => RecentCharacters.Count > 0;
    public bool HasNoRecentCharacters => RecentCharacters.Count == 0;

    public MainWindowViewModel()
    {
        try
        {
            WindowTitle = "Chummer - [" + App.LanguageCatalog.GetString("Title_CareerMode") + " ("
                + App.LanguageCatalog.GetString("UI_DefaultSettingsName") + ")]";
        }
        catch
        {
            WindowTitle = "Chummer";
        }

        RebuildRecentCharacters();
        GlobalOptions.Instance.MruChanged += RebuildRecentCharacters;
    }

    public void AddOpenCharacter(CharacterDocument character, string? sourcePath = null)
    {
        var tab = new OpenCharacterTab(character, sourcePath);
        tab.BackupFailed += message => ReportError("Could not create the pre-career backup: " + message);
        OpenCharacters.Add(tab);
        SelectedOpenCharacter = tab;
        ClearError();

        if (!string.IsNullOrWhiteSpace(sourcePath))
            GlobalOptions.Instance.AddToMruList(sourcePath);
    }

    public void CloseCharacter(OpenCharacterTab tab)
    {
        int index = OpenCharacters.IndexOf(tab);
        bool closingActiveCharacter = ReferenceEquals(SelectedOpenCharacter, tab);
        OpenCharacters.Remove(tab);
        if (!closingActiveCharacter)
            return;

        SelectedOpenCharacter = OpenCharacters.Count == 0
            ? null
            : OpenCharacters[Math.Min(index, OpenCharacters.Count - 1)];
    }

    public void ReportError(string message)
    {
        ErrorMessage = message;
    }

    public void ClearError()
    {
        ErrorMessage = string.Empty;
    }

    public void RememberSavedPath(string filePath)
    {
        if (!string.IsNullOrWhiteSpace(filePath))
            GlobalOptions.Instance.AddToMruList(filePath);
    }

    public void MarkSaved(OpenCharacterTab tab, string? filePath)
    {
        tab.SetSavedPath(filePath);
        RememberSavedPath(filePath ?? string.Empty);
    }

    public void RemoveRecentCharacter(string filePath, bool isSticky)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return;

        if (isSticky)
            GlobalOptions.Instance.RemoveFromStickyMruList(filePath);
        else
            GlobalOptions.Instance.RemoveFromMruList(filePath);
    }

    private void OnActiveCharacterChanged()
    {
        ActivateCharacter(SelectedOpenCharacter?.Character);
    }

    private void ActivateCharacter(CharacterDocument? character)
    {
        if (character is null)
        {
            WindowTitle = "Chummer";
            KarmaStatus = App.LanguageCatalog.GetString("UI_KarmaStatusDefault");
            CreationBudgetTooltip = string.Empty;
            EssenceStatus = App.LanguageCatalog.GetString("UI_EssenceStatusDefault");
            NuyenStatus = App.LanguageCatalog.GetString("UI_NuyenStatusDefault");
            return;
        }

        if (!character.Created)
        {
            CharacterCreationBudgetData budget = character.CreationBudget;
            string strPoolName = string.Equals(character.BuildMethod, "BP", StringComparison.OrdinalIgnoreCase)
                ? App.LanguageCatalog.GetString("String_BP")
                : App.LanguageCatalog.GetString("String_Karma");
            KarmaStatus = strPoolName + ": " + budget.Remaining + " / " + budget.Starting
                + " (" + budget.Spent + " used)";
            CreationBudgetTooltip = FormatCreationBudgetTooltip(strPoolName, budget);
        }
        else
        {
            CreationBudgetTooltip = string.Empty;
            KarmaStatus = string.Equals(character.BuildMethod, "BP", StringComparison.OrdinalIgnoreCase)
            ? App.LanguageCatalog.GetString("String_BP") + ": " + character.Bp + " / "
                + App.LanguageCatalog.GetString("String_Karma") + ": " + character.Karma
            : App.LanguageCatalog.GetString("String_Karma") + ": " + character.Karma;
        }
        EssenceStatus = App.LanguageCatalog.GetString("UI_EssenceStatusPrefix") + character.Condition.Essence;
        NuyenStatus = App.LanguageCatalog.GetString("UI_NuyenStatusPrefix") + character.Nuyen + "¥";
        WindowTitle = "Chummer - " + character.Name;
    }

    private static string FormatCreationBudgetTooltip(string strPoolName, CharacterCreationBudgetData budget)
    {
        var builder = new StringBuilder();
        builder.Append(strPoolName).Append(": ").Append(budget.Remaining).Append(" remaining of ")
            .Append(budget.Starting).Append("\nSpent: ").Append(budget.Spent);
        foreach (CharacterCreationBudgetCategoryData category in budget.Categories)
            builder.Append('\n').Append(category.Name).Append(": ").Append(category.Cost >= 0 ? "+" : string.Empty)
                .Append(category.Cost);
        return builder.ToString();
    }

    private void RebuildRecentCharacters()
    {
        RecentCharacters.Clear();

        foreach (string strFilePath in GlobalOptions.Instance.ReadStickyMruList())
            RecentCharacters.Add(new RecentCharacterEntryViewModel(strFilePath, true));

        foreach (string strFilePath in GlobalOptions.Instance.ReadMruList())
            RecentCharacters.Add(new RecentCharacterEntryViewModel(strFilePath, false));

        // Avalonia's MenuItem can't mix ItemsSource with a declared static child MenuItem in
        // XAML (that silently breaks the binding and leaves the submenu empty regardless of
        // what's in this collection) - the "no recent files" placeholder has to live in the
        // bound collection itself instead.
        if (RecentCharacters.Count == 0)
            RecentCharacters.Add(RecentCharacterEntryViewModel.CreatePlaceholder(App.LanguageCatalog.GetString("UI_NoRecentFiles")));

        OnPropertyChanged(nameof(HasRecentCharacters));
        OnPropertyChanged(nameof(HasNoRecentCharacters));
    }
}
