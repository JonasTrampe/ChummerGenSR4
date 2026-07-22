using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Chummer.Core;
using Chummer.NewUI.Api;
using RunnersPointAuth = Chummer.NewUI.Api.RunnersPointAuth;

namespace Chummer.NewUI.ViewModels;

public sealed class CloudDocumentsDialogViewModel : ViewModelBase
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
    private string _connectionState = "Not connected";
    private string _status = string.Empty;
    private bool _sharedMode;
    private bool _useApiToken = true;
    private string _gameProfileId = string.Empty;
    private string _gameProfileFormat = string.Empty;

    public ObservableCollection<FolderNodeViewModel> Folders { get; } = new();
    public ObservableCollection<CloudDocumentEntryViewModel> VisibleDocuments { get; } = new();

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
    public bool HasActiveCharacter => _activeCharacter != null;
    public bool CanManageSelectedFolder => SelectedFolder != null && SelectedFolder.Id >= 0;
    public bool CanFileDocument => SelectedDocument != null && SelectedFolder != null && SelectedFolder.Id >= 0;
    public bool CanUnfileDocument => SelectedDocument != null;
    public bool CanDownloadDocument => SelectedDocument != null;
    public bool CanPushCurrentCharacter => _activeCharacter != null;
    public bool CanEditMetadata => _activeCharacter != null;
    public bool CanPushSharedDocument => SelectedDocument?.CanPushShared == true && _activeCharacter != null;
    public bool CanArchiveDocument => SelectedDocument != null && !SharedMode;
    public string ArchiveButtonText => SelectedDocument?.IsArchived == true ? "Unarchive Selected" : "Archive Selected";

    public CloudDocumentsDialogViewModel(CharacterDocument? objActiveCharacter = null, string? strActiveCharacterPath = null)
    {
        _apiClient = new RunnersPointApiClient(_auth);
        _activeCharacter = objActiveCharacter;
        _activeCharacterPath = strActiveCharacterPath;
    }

    public void ReportError(string strMessage)
    {
        Status = string.IsNullOrWhiteSpace(strMessage) ? "Cloud operation failed." : strMessage;
    }

    public async System.Threading.Tasks.Task InitializeAsync()
    {
        if (_auth.HasStoredLogin() && !_auth.IsApiTokenLogin())
            UseApiToken = false;

        if (_auth.HasStoredLogin() && _auth.IsApiTokenLogin())
            ApiToken = "................................";

        UpdateConnectionState();
        if (IsLoggedIn)
            await RefreshAsync();
        else
            Status = "Not logged in";
    }

    public async System.Threading.Tasks.Task LoginWithOAuthAsync()
    {
        Status = "Logging in...";
        await _auth.LoginAsync();
        UpdateConnectionState();
        await RefreshAsync();
    }

    public async System.Threading.Tasks.Task UseApiTokenAsync()
    {
        if (ApiToken == "................................")
            return;

        _auth.SetApiToken(ApiToken);
        UpdateConnectionState();
        ApiToken = "................................";
        await RefreshAsync();
    }

    public void Logout()
    {
        _auth.Logout();
        _documents.Clear();
        VisibleDocuments.Clear();
        Folders.Clear();
        UpdateConnectionState();
        ApiToken = string.Empty;
        Status = "Not logged in";
    }

    public async System.Threading.Tasks.Task RefreshAsync()
    {
        if (!IsLoggedIn)
        {
            Status = "Not logged in";
            return;
        }

        Status = "Refreshing...";
        await EnsureGameProfileAsync();

        _folders.Clear();
        _folders.AddRange(await _apiClient.ListFoldersAsync());
        RebuildFolderTree();

        _documents.Clear();
        string? strCursor = null;
        if (SharedMode)
        {
            do
            {
                RunnersPointSharedDocumentPage objPage = await _apiClient.ListSharedDocumentsAsync(_gameProfileId, strCursor ?? string.Empty);
                foreach (RunnersPointSharedDocument objDocument in objPage.Items)
                    _documents.Add(new CloudDocumentEntryViewModel(objDocument, true));
                strCursor = objPage.NextCursor;
            } while (!string.IsNullOrEmpty(strCursor));
        }
        else
        {
            do
            {
                RunnersPointDocumentPage objPage = await _apiClient.ListDocumentsAsync(_gameProfileId, strCursor ?? string.Empty);
                foreach (RunnersPointDocument objDocument in objPage.Items)
                    _documents.Add(new CloudDocumentEntryViewModel(objDocument, false));
                strCursor = objPage.NextCursor;
            } while (!string.IsNullOrEmpty(strCursor));
        }

        RefreshVisibleDocuments();
        Status = "Ready";
        RaiseSelectionFlagsChanged();
    }

    public async System.Threading.Tasks.Task CreateFolderAsync(string strName)
    {
        await _apiClient.CreateFolderAsync(strName.Trim(), GetSelectedFolderId(false));
        await RefreshAsync();
    }

    public async System.Threading.Tasks.Task RenameSelectedFolderAsync(string strName)
    {
        if (!CanManageSelectedFolder || SelectedFolder == null)
            return;

        await _apiClient.UpdateFolderAsync(SelectedFolder.Id, strName.Trim());
        await RefreshAsync();
    }

    public async System.Threading.Tasks.Task DeleteSelectedFolderAsync()
    {
        if (!CanManageSelectedFolder || SelectedFolder == null)
            return;

        await _apiClient.DeleteFolderAsync(SelectedFolder.Id);
        await RefreshAsync();
    }

    public async System.Threading.Tasks.Task FileSelectedDocumentAsync()
    {
        if (!CanFileDocument || SelectedFolder == null || SelectedDocument == null)
            return;

        if (SelectedDocument.IsShared)
            await _apiClient.SetSharedDocumentFolderAsync(SelectedDocument.Id, SelectedFolder.Id);
        else
            await _apiClient.SetDocumentFolderAsync(SelectedDocument.Id, SelectedFolder.Id);
        await RefreshAsync();
    }

    public async System.Threading.Tasks.Task UnfileSelectedDocumentAsync()
    {
        if (SelectedDocument == null)
            return;

        if (SelectedDocument.IsShared)
            await _apiClient.SetSharedDocumentFolderAsync(SelectedDocument.Id, null);
        else
            await _apiClient.SetDocumentFolderAsync(SelectedDocument.Id, null);
        await RefreshAsync();
    }

    public async System.Threading.Tasks.Task MoveDocumentToFolderAsync(CloudDocumentEntryViewModel objDocument, int? intFolderId)
    {
        if (objDocument.IsShared)
            await _apiClient.SetSharedDocumentFolderAsync(objDocument.Id, intFolderId);
        else
            await _apiClient.SetDocumentFolderAsync(objDocument.Id, intFolderId);
        await RefreshAsync();
    }

    public async System.Threading.Tasks.Task PushCurrentCharacterAsync()
    {
        if (_activeCharacter == null)
            return;

        Status = "Pushing...";
        byte[] bytContent = SerializeActiveCharacter();
        RunnersPointRevisionStatus objStatus;

        if (string.IsNullOrEmpty(_activeCharacter.CloudDocumentId))
        {
            objStatus = await _apiClient.CreateDocumentAsync(bytContent, _gameProfileId, _gameProfileFormat);
            _activeCharacter.CloudDocumentId = objStatus.DocumentId;
            SaveActiveCharacterIfPossible();
        }
        else
        {
            Tuple<RunnersPointDocument, string> objCurrent = await _apiClient.GetDocumentAsync(_activeCharacter.CloudDocumentId);
            if (CloudDocumentEntryViewModel.InFlightStates.Contains(objCurrent.Item1.ValidationState))
                throw new InvalidOperationException("The cloud document is still being processed and cannot be updated yet.");

            objStatus = await _apiClient.PushRevisionAsync(_activeCharacter.CloudDocumentId, bytContent, objCurrent.Item2,
                _gameProfileId, _gameProfileFormat);
        }

        Status = BuildAcceptedStatus(objStatus);
        await RefreshAsync();
    }

    public async System.Threading.Tasks.Task PushSelectedSharedDocumentAsync()
    {
        if (_activeCharacter == null || SelectedDocument?.Document is not RunnersPointSharedDocument objDocument)
            return;

        Status = "Pushing...";
        byte[] bytContent = SerializeActiveCharacter();
        Tuple<RunnersPointSharedDocument, string> objCurrent = await _apiClient.GetSharedDocumentAsync(objDocument.Id);
        if (CloudDocumentEntryViewModel.InFlightStates.Contains(objCurrent.Item1.ValidationState))
            throw new InvalidOperationException("The shared document is still being processed and cannot be updated yet.");

        RunnersPointRevisionStatus objStatus = await _apiClient.PushSharedDocumentRevisionAsync(
            objDocument.Id, bytContent, objCurrent.Item2, _gameProfileId, _gameProfileFormat);

        Status = BuildAcceptedStatus(objStatus);
        await RefreshAsync();
    }

    public async System.Threading.Tasks.Task<Tuple<byte[], string>> DownloadSelectedDocumentAsync()
    {
        if (SelectedDocument == null)
            throw new InvalidOperationException("No document selected.");

        bool blnShared = SelectedDocument.IsShared;
        RunnersPointDocument objCurrentDocument = blnShared
            ? (await _apiClient.GetSharedDocumentAsync(SelectedDocument.Id)).Item1
            : (await _apiClient.GetDocumentAsync(SelectedDocument.Id)).Item1;

        if (!CloudDocumentEntryViewModel.DownloadableStates.Contains(objCurrentDocument.ValidationState))
            throw new InvalidOperationException("The selected document cannot be downloaded while its state is '" + objCurrentDocument.ValidationState + "'.");

        Status = "Downloading...";
        Tuple<byte[], string> objDownload = blnShared
            ? await _apiClient.DownloadSharedDocumentRevisionAsync(objCurrentDocument.Id, objCurrentDocument.CurrentRevision)
            : await _apiClient.DownloadRevisionAsync(objCurrentDocument.Id, objCurrentDocument.CurrentRevision);
        Status = "Ready";
        return objDownload;
    }

    public async System.Threading.Tasks.Task ArchiveOrUnarchiveSelectedDocumentAsync()
    {
        if (SelectedDocument == null || SharedMode)
            return;

        Tuple<RunnersPointDocument, string> objCurrent = await _apiClient.GetDocumentAsync(SelectedDocument.Id);
        if (SelectedDocument.IsArchived)
            await _apiClient.UnarchiveDocumentAsync(SelectedDocument.Id, objCurrent.Item2);
        else
            await _apiClient.ArchiveDocumentAsync(SelectedDocument.Id, objCurrent.Item2);

        await RefreshAsync();
    }

    public System.Threading.Tasks.Task<List<RunnersPointRevision>> ListRevisionsAsync(string strDocumentId, bool blnShared)
    {
        return blnShared
            ? _apiClient.ListSharedRevisionsAsync(strDocumentId)
            : _apiClient.ListRevisionsAsync(strDocumentId);
    }

    public System.Threading.Tasks.Task<Tuple<byte[], string>> DownloadRevisionAsync(string strDocumentId, string strRevisionId, bool blnShared)
    {
        return blnShared
            ? _apiClient.DownloadSharedDocumentRevisionAsync(strDocumentId, strRevisionId)
            : _apiClient.DownloadRevisionAsync(strDocumentId, strRevisionId);
    }

    public System.Threading.Tasks.Task<Tuple<RunnersPointDocument, string>> GetDocumentAsync(string strDocumentId)
    {
        return _apiClient.GetDocumentAsync(strDocumentId);
    }

    public async System.Threading.Tasks.Task<Tuple<RunnersPointDocument, string>> GetDocumentForRevisionDialogAsync(string strDocumentId, bool blnShared)
    {
        if (blnShared)
        {
            Tuple<RunnersPointSharedDocument, string> objShared = await _apiClient.GetSharedDocumentAsync(strDocumentId);
            return new Tuple<RunnersPointDocument, string>(objShared.Item1, objShared.Item2);
        }

        return await _apiClient.GetDocumentAsync(strDocumentId);
    }

    public async System.Threading.Tasks.Task PurgeRevisionAsync(string strDocumentId, string strRevisionId, bool blnShared)
    {
        string strIfMatch = (await GetDocumentForRevisionDialogAsync(strDocumentId, blnShared)).Item2;
        if (blnShared)
            await _apiClient.PurgeSharedRevisionAsync(strDocumentId, strRevisionId, strIfMatch);
        else
            await _apiClient.PurgeRevisionAsync(strDocumentId, strRevisionId, strIfMatch);
    }

    public async System.Threading.Tasks.Task PurgeDocumentAsync(string strDocumentId, bool blnShared)
    {
        string strIfMatch = (await GetDocumentForRevisionDialogAsync(strDocumentId, blnShared)).Item2;
        if (blnShared)
            await _apiClient.PurgeSharedDocumentAsync(strDocumentId, strIfMatch);
        else
            await _apiClient.PurgeDocumentAsync(strDocumentId, strIfMatch);
    }

    public async System.Threading.Tasks.Task UpdateActiveCharacterMetadataAsync(string strDisplayName, string strDescription, string strImageUrl)
    {
        if (_activeCharacter == null)
            return;

        string strTrimmedImageUrl = strImageUrl.Trim();
        if (!string.IsNullOrWhiteSpace(strTrimmedImageUrl) && !strTrimmedImageUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Image URL must start with https://");

        _activeCharacter.CloudMetadataDisplayName = strDisplayName.Trim();
        _activeCharacter.CloudMetadataDescription = strDescription.Trim();
        _activeCharacter.CloudMetadataImageUrl = strTrimmedImageUrl;
        SaveActiveCharacterIfPossible();

        if (!string.IsNullOrEmpty(_activeCharacter.CloudDocumentId))
        {
            Tuple<RunnersPointDocument, string> objCurrent = await _apiClient.GetDocumentAsync(_activeCharacter.CloudDocumentId);
            await _apiClient.UpdateDocumentMetadataAsync(_activeCharacter.CloudDocumentId, objCurrent.Item2,
                _activeCharacter.CloudMetadataDisplayName, _activeCharacter.CloudMetadataDescription,
                _activeCharacter.CloudMetadataImageUrl);
        }

        await RefreshAsync();
    }

    public string GetActiveCharacterDisplayName() => _activeCharacter?.CloudMetadataDisplayName ?? string.Empty;
    public string GetActiveCharacterDescription() => _activeCharacter?.CloudMetadataDescription ?? string.Empty;
    public string GetActiveCharacterImageUrl() => _activeCharacter?.CloudMetadataImageUrl ?? string.Empty;

    private async System.Threading.Tasks.Task EnsureGameProfileAsync()
    {
        if (!string.IsNullOrEmpty(_gameProfileId))
            return;

        RunnersPointCapabilities objCapabilities = await _apiClient.GetCapabilitiesAsync();
        RunnersPointGameProfile? objProfile = objCapabilities.GameProfiles.FirstOrDefault(
            p => p.System.IndexOf("Shadowrun", StringComparison.OrdinalIgnoreCase) >= 0 && p.Edition.Contains("4"));
        if (objProfile == null)
            throw new InvalidOperationException("No Shadowrun 4 game profile available.");

        string? strFormat = objProfile.Formats.FirstOrDefault(f => f == "application/xml") ?? objProfile.Formats.FirstOrDefault();
        if (string.IsNullOrEmpty(strFormat))
            throw new InvalidOperationException("No supported format available.");

        _gameProfileId = objProfile.Id;
        _gameProfileFormat = strFormat;
    }

    private void RebuildFolderTree()
    {
        int intSelectedId = SelectedFolder?.Id ?? AllDocumentsFolderId;
        Folders.Clear();

        FolderNodeViewModel objAll = new(AllDocumentsFolderId, "All Documents") { IsExpanded = true };
        FolderNodeViewModel objUnfiled = new(UnfiledFolderId, "Unfiled");
        Folders.Add(objAll);
        Folders.Add(objUnfiled);

        Dictionary<int, FolderNodeViewModel> dicNodes = new();
        foreach (RunnersPointFolder objFolder in _folders.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            dicNodes[objFolder.Id] = new FolderNodeViewModel(objFolder.Id, objFolder.Name);

        foreach (RunnersPointFolder objFolder in _folders.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            FolderNodeViewModel objNode = dicNodes[objFolder.Id];
            objNode.IsExpanded = true;
            if (objFolder.ParentFolderId.HasValue && dicNodes.ContainsKey(objFolder.ParentFolderId.Value))
                dicNodes[objFolder.ParentFolderId.Value].Children.Add(objNode);
            else
                Folders.Add(objNode);
        }

        SelectedFolder = FindFolderNode(intSelectedId) ?? objAll;
    }

    private FolderNodeViewModel? FindFolderNode(int intId)
    {
        foreach (FolderNodeViewModel objNode in Folders)
        {
            FolderNodeViewModel? objMatch = FindFolderNodeRecursive(objNode, intId);
            if (objMatch != null)
                return objMatch;
        }

        return null;
    }

    private static FolderNodeViewModel? FindFolderNodeRecursive(FolderNodeViewModel objNode, int intId)
    {
        if (objNode.Id == intId)
            return objNode;

        foreach (FolderNodeViewModel objChild in objNode.Children)
        {
            FolderNodeViewModel? objMatch = FindFolderNodeRecursive(objChild, intId);
            if (objMatch != null)
                return objMatch;
        }

        return null;
    }

    private void RefreshVisibleDocuments()
    {
        VisibleDocuments.Clear();
        int intSelectedFolderId = SelectedFolder?.Id ?? AllDocumentsFolderId;
        foreach (CloudDocumentEntryViewModel objDocument in _documents)
        {
            if (intSelectedFolderId == UnfiledFolderId)
            {
                if (objDocument.FolderId.HasValue)
                    continue;
            }
            else if (intSelectedFolderId != AllDocumentsFolderId && objDocument.FolderId != intSelectedFolderId)
                continue;

            VisibleDocuments.Add(objDocument);
        }

        if (SelectedDocument != null && !VisibleDocuments.Contains(SelectedDocument))
            SelectedDocument = null;
    }

    private int? GetSelectedFolderId(bool blnRequireActualFolder)
    {
        if (SelectedFolder == null)
            return null;
        if (blnRequireActualFolder && SelectedFolder.Id < 0)
            return null;
        return SelectedFolder.Id >= 0 ? SelectedFolder.Id : null;
    }

    private void UpdateConnectionState()
    {
        ConnectionState = !_auth.HasStoredLogin()
            ? "Not connected"
            : _auth.IsApiTokenLogin() ? "Connected via API token" : "Connected via OAuth";
        OnPropertyChanged(nameof(IsLoggedIn));
    }

    private byte[] SerializeActiveCharacter()
    {
        if (_activeCharacter == null)
            return Array.Empty<byte>();

        using MemoryStream objStream = new();
        _characterFileService.Save(_activeCharacter, objStream, _activeCharacter.DisplayName);
        return objStream.ToArray();
    }

    private void SaveActiveCharacterIfPossible()
    {
        if (_activeCharacter == null || string.IsNullOrWhiteSpace(_activeCharacterPath))
            return;

        using FileStream objStream = File.Create(_activeCharacterPath);
        _characterFileService.Save(_activeCharacter, objStream, Path.GetFileName(_activeCharacterPath));
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

    private static string BuildAcceptedStatus(RunnersPointRevisionStatus objStatus)
    {
        return objStatus.Messages.Count == 0
            ? "Push accepted"
            : "Push accepted " + string.Join(" ", objStatus.Messages);
    }
}
