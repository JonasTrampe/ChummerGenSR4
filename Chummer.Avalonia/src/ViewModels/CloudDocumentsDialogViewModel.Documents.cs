using System;
using System.Linq;
using System.Threading.Tasks;
using RunnersPoint.Api;

namespace Chummer.NewUI.ViewModels;

public sealed partial class CloudDocumentsDialogViewModel
{
    public async Task RefreshAsync()
    {
        if (!IsLoggedIn)
        {
            Status = T("String_Cloud_NotLoggedIn");
            return;
        }

        Status = T("String_Cloud_Refreshing");
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
        Status = T("String_Cloud_Ready");
        RaiseSelectionFlagsChanged();
    }

    public async Task FileSelectedDocumentAsync()
    {
        if (!CanFileDocument || SelectedFolder == null || SelectedDocument == null)
            return;

        if (SelectedDocument.IsShared)
            await _apiClient.SetSharedDocumentFolderAsync(SelectedDocument.Id, SelectedFolder.Id);
        else
            await _apiClient.SetDocumentFolderAsync(SelectedDocument.Id, SelectedFolder.Id);
        await RefreshAsync();
    }

    public async Task UnfileSelectedDocumentAsync()
    {
        if (SelectedDocument == null)
            return;

        if (SelectedDocument.IsShared)
            await _apiClient.SetSharedDocumentFolderAsync(SelectedDocument.Id, null);
        else
            await _apiClient.SetDocumentFolderAsync(SelectedDocument.Id, null);
        await RefreshAsync();
    }

    public async Task MoveDocumentToFolderAsync(CloudDocumentEntryViewModel objDocument, int? intFolderId)
    {
        if (objDocument.IsShared)
            await _apiClient.SetSharedDocumentFolderAsync(objDocument.Id, intFolderId);
        else
            await _apiClient.SetDocumentFolderAsync(objDocument.Id, intFolderId);
        await RefreshAsync();
    }

    private async Task EnsureGameProfileAsync()
    {
        if (!string.IsNullOrEmpty(_gameProfileId))
            return;

        RunnersPointCapabilities objCapabilities = await _apiClient.GetCapabilitiesAsync();
        RunnersPointGameProfile? objProfile = objCapabilities.GameProfiles.FirstOrDefault(
            p => p.System.IndexOf("Shadowrun", StringComparison.OrdinalIgnoreCase) >= 0 && p.Edition.Contains("4"));
        if (objProfile == null)
            throw new InvalidOperationException(T("String_Cloud_NoGameProfile"));

        string? strFormat = objProfile.Formats.FirstOrDefault(f => f == "application/xml") ?? objProfile.Formats.FirstOrDefault();
        if (string.IsNullOrEmpty(strFormat))
            throw new InvalidOperationException(T("String_Cloud_UnsupportedDocumentType"));

        _gameProfileId = objProfile.Id;
        _gameProfileFormat = strFormat;
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
}
