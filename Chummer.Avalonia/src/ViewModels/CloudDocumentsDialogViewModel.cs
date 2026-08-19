using System.Collections.Generic;
using System.Collections.ObjectModel;
using Chummer.Core;
using RunnersPoint.Api;
using RunnersPointAuth = RunnersPoint.Api.RunnersPointAuth;

namespace Chummer.NewUI.ViewModels;

// Split by concern into partial-class files: .Auth.cs (login/logout/connection state),
// .Folders.cs (folder tree CRUD), .Documents.cs (listing/filing), .Push.cs (push/download/
// archive), .Revisions.cs (revision history + character metadata). This file keeps the
// constructor, bindable state/labels, and the shared T()/ReportError/RaiseSelectionFlagsChanged
// helpers used across the other parts.
public sealed partial class CloudDocumentsDialogViewModel : ViewModelBase
{
    public const int AllDocumentsFolderId = -1;
    public const int UnfiledFolderId = -2;

    private readonly RunnersPointAuth _auth = new();
    private readonly IRunnersPointApiClient _apiClient;
    private readonly CharacterFileService _characterFileService = new();
    private readonly List<RunnersPointFolder> _folders = new();
    private readonly List<CloudDocumentEntryViewModel> _documents = new();
    private readonly CharacterDocument? _activeCharacter;
    private readonly string? _activeCharacterPath;

    private FolderNodeViewModel? _selectedFolder;
    private CloudDocumentEntryViewModel? _selectedDocument;
    private string _apiToken = string.Empty;
    private string _connectionState = string.Empty;
    private string _status = string.Empty;
    private bool _sharedMode;
    private bool _useApiToken = true;
    private string _gameProfileId = string.Empty;
    private string _gameProfileFormat = string.Empty;

    public ObservableCollection<FolderNodeViewModel> Folders { get; } = new();
    public ObservableCollection<CloudDocumentEntryViewModel> VisibleDocuments { get; } = new();
    public string TitleText => T("Title_CloudDocuments");
    public string MyDocumentsText => T("Radio_Cloud_MyDocuments");
    public string SharedWithMeText => T("Radio_Cloud_SharedWithMe");
    public string UseApiTokenText => T("Radio_Cloud_AuthApiToken");
    public string UseOAuthText => T("Radio_Cloud_AuthOAuth");
    public string ApiTokenLabelText => T("Label_Cloud_ApiToken");
    public string UseTokenButtonText => T("Button_Cloud_UseApiToken");
    public string LoginButtonText => T("Button_Cloud_Login");
    public string NewFolderButtonText => T("Button_Cloud_NewFolder");
    public string RenameFolderButtonText => T("Button_Cloud_RenameFolder");
    public string DeleteButtonText => T("String_Delete");
    public string RefreshButtonText => T("Button_Cloud_Refresh");
    public string PushCurrentButtonText => T("Button_Cloud_PushCurrent");
    public string PushSharedButtonText => T("Button_Cloud_PushShared");
    public string DownloadButtonText => T("Button_Cloud_Download");
    public string RevisionsButtonText => T("Button_Cloud_Revisions");
    public string EditMetadataButtonText => T("Button_Cloud_EditMetadata");
    public string FileInFolderButtonText => T("Button_Cloud_FileInFolder");
    public string UnfileButtonText => T("Button_Cloud_Unfile");
    public string LogoutButtonText => T("Button_Cloud_Logout");
    public string NameHeaderText => T("Label_Name");
    public string StateHeaderText => T("String_Cloud_RevisionState");
    public string UpdatedHeaderText => T("String_Cloud_Updated");
    public string ShareHeaderText => T("String_Cloud_Share");
    public string TokenPresetState => _auth.HasStoredLogin()
        ? T("String_Cloud_TokenPreset_Stored")
        : T("String_Cloud_TokenPreset_NotStored");

    public FolderNodeViewModel? SelectedFolder
    {
        get => _selectedFolder;
        set
        {
            if (!SetField(ref _selectedFolder, value))
                return;
            RefreshVisibleDocuments();
            OnPropertyChanged(nameof(CanManageSelectedFolder));
            OnPropertyChanged(nameof(CanFileDocument));
        }
    }

    public CloudDocumentEntryViewModel? SelectedDocument
    {
        get => _selectedDocument;
        set
        {
            if (!SetField(ref _selectedDocument, value))
                return;
            RaiseSelectionFlagsChanged();
        }
    }

    public string ApiToken
    {
        get => _apiToken;
        set => SetField(ref _apiToken, value);
    }

    public string ConnectionState
    {
        get => _connectionState;
        private set => SetField(ref _connectionState, value);
    }

    public string Status
    {
        get => _status;
        private set => SetField(ref _status, value);
    }

    public bool SharedMode
    {
        get => _sharedMode;
        set
        {
            if (!SetField(ref _sharedMode, value))
                return;
            OnPropertyChanged(nameof(MyDocumentsMode));
            OnPropertyChanged(nameof(SharedWithMeMode));
            RaiseSelectionFlagsChanged();
        }
    }

    public bool MyDocumentsMode
    {
        get => !SharedMode;
        set
        {
            if (value)
                SharedMode = false;
        }
    }

    public bool SharedWithMeMode
    {
        get => SharedMode;
        set
        {
            if (value)
                SharedMode = true;
        }
    }

    public bool UseApiToken
    {
        get => _useApiToken;
        set
        {
            if (!SetField(ref _useApiToken, value))
                return;
            OnPropertyChanged(nameof(UseOAuth));
        }
    }

    public bool UseOAuth
    {
        get => !UseApiToken;
        set
        {
            if (value)
                UseApiToken = false;
        }
    }

    public bool IsLoggedIn => _auth.HasStoredLogin();
    public bool IsApiTokenLogin() => _auth.IsApiTokenLogin();
    public CharacterDocument? ActiveCharacter => _activeCharacter;
    public bool HasActiveCharacter => _activeCharacter != null;
    public bool HasActiveCharacterPath => !string.IsNullOrWhiteSpace(_activeCharacterPath);
    public bool CanManageSelectedFolder => SelectedFolder != null && SelectedFolder.Id >= 0;
    public bool CanFileDocument => SelectedDocument != null && SelectedFolder != null && SelectedFolder.Id >= 0;
    public bool CanUnfileDocument => SelectedDocument != null;
    public bool CanDownloadDocument => SelectedDocument != null;
    public bool CanPushCurrentCharacter => _activeCharacter != null;
    public bool CanEditMetadata => _activeCharacter != null;
    public bool CanPushSharedDocument => SelectedDocument?.CanPushShared == true && _activeCharacter != null;
    public bool CanArchiveDocument => SelectedDocument != null && !SharedMode;
    public string ArchiveButtonText => SelectedDocument?.IsArchived == true ? T("Button_Cloud_Unarchive") : T("Button_Cloud_Archive");

    public CloudDocumentsDialogViewModel(CharacterDocument? objActiveCharacter = null, string? strActiveCharacterPath = null)
    {
        _apiClient = new RunnersPointApiClient(_auth);
        _activeCharacter = objActiveCharacter;
        _activeCharacterPath = strActiveCharacterPath;
    }

    public void ReportError(string strMessage)
    {
        Status = string.IsNullOrWhiteSpace(strMessage) ? string.Format(T("String_Cloud_Error"), "Cloud operation failed.") : strMessage;
    }

    private void RaiseSelectionFlagsChanged()
    {
        OnPropertyChanged(nameof(CanUnfileDocument));
        OnPropertyChanged(nameof(CanDownloadDocument));
        OnPropertyChanged(nameof(CanFileDocument));
        OnPropertyChanged(nameof(CanPushSharedDocument));
        OnPropertyChanged(nameof(CanArchiveDocument));
        OnPropertyChanged(nameof(ArchiveButtonText));
    }

    private static string T(string strKey)
    {
        return App.LanguageCatalog.GetString(strKey);
    }
}
