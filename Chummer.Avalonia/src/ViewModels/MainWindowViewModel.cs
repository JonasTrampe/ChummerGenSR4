using System;
using System.Collections.ObjectModel;
using Chummer.Core;
using Chummer.NewUI.Controls;

namespace Chummer.NewUI.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private OpenCharacterTab? _selectedOpenCharacter;
    private string _strKarmaStatus = "Karma: —";
    private string _strEssenceStatus = "Essenz: —";
    private string _strNuyenStatus = "Nuyen: —";
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
            WindowTitle = "Chummer - [" + App.LanguageCatalog.GetString("Title_CareerMode") + " (Default Settings)]";
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
            KarmaStatus = "Karma: —";
            EssenceStatus = "Essenz: —";
            NuyenStatus = "Nuyen: —";
            return;
        }

        KarmaStatus = string.Equals(character.BuildMethod, "BP", StringComparison.OrdinalIgnoreCase)
            ? "BP: " + character.Bp + " / Karma: " + character.Karma
            : "Karma: " + character.Karma;
        EssenceStatus = "Essenz: " + character.Condition.Essence;
        NuyenStatus = "Nuyen: " + character.Nuyen + "¥";
        WindowTitle = "Chummer - " + character.Name;
    }

    private void RebuildRecentCharacters()
    {
        RecentCharacters.Clear();

        foreach (string strFilePath in GlobalOptions.Instance.ReadStickyMruList())
            RecentCharacters.Add(new RecentCharacterEntryViewModel(strFilePath, true));

        foreach (string strFilePath in GlobalOptions.Instance.ReadMruList())
            RecentCharacters.Add(new RecentCharacterEntryViewModel(strFilePath, false));

        OnPropertyChanged(nameof(HasRecentCharacters));
        OnPropertyChanged(nameof(HasNoRecentCharacters));
    }
}
