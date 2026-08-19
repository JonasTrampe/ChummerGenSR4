using System;
using System.IO;
using System.Threading.Tasks;
using RunnersPoint.Api;

namespace Chummer.NewUI.ViewModels;

public sealed partial class CloudDocumentsDialogViewModel
{
    public async Task PushCurrentCharacterAsync()
    {
        if (_activeCharacter == null)
            return;

        Status = T("String_Cloud_Pushing");
        byte[] bytContent = SerializeActiveCharacter();
        RunnersPointRevisionStatus objStatus;

        if (string.IsNullOrEmpty(_activeCharacter.CloudDocumentId))
        {
            objStatus = await _apiClient.CreateDocumentAsync(bytContent, _gameProfileId, _gameProfileFormat);
            _activeCharacter.CloudDocumentId = objStatus.DocumentId;
            _activeCharacter.CloudLastKnownRevisionId = objStatus.RevisionId;
            SaveActiveCharacterIfPossible();
        }
        else
        {
            Tuple<RunnersPointDocument, string> objCurrent = await _apiClient.GetDocumentAsync(_activeCharacter.CloudDocumentId);
            if (CloudDocumentEntryViewModel.InFlightStates.Contains(objCurrent.Item1.ValidationState))
                throw new InvalidOperationException(T("String_Cloud_PushInFlight"));

            objStatus = await _apiClient.PushRevisionAsync(_activeCharacter.CloudDocumentId, bytContent, objCurrent.Item2,
                _gameProfileId, _gameProfileFormat);
            _activeCharacter.CloudLastKnownRevisionId = objStatus.RevisionId;
            SaveActiveCharacterIfPossible();
        }

        Status = BuildAcceptedStatus(objStatus);
        await RefreshAsync();
    }

    public void SaveActiveCharacterSnapshot()
    {
        SaveActiveCharacterIfPossible();
    }

    public async Task<RunnersPointRevisionStatus> ForcePushCurrentCharacterAsync()
    {
        if (_activeCharacter == null || string.IsNullOrEmpty(_activeCharacter.CloudDocumentId))
            throw new InvalidOperationException(T("String_Cloud_NotSavedLocally"));

        Status = T("String_Cloud_Pushing");
        byte[] bytContent = SerializeActiveCharacter();
        Tuple<RunnersPointDocument, string> objCurrent = await _apiClient.GetDocumentAsync(_activeCharacter.CloudDocumentId);
        if (CloudDocumentEntryViewModel.InFlightStates.Contains(objCurrent.Item1.ValidationState))
            throw new InvalidOperationException(T("String_Cloud_PushInFlight"));

        RunnersPointRevisionStatus objStatus = await _apiClient.PushRevisionAsync(
            _activeCharacter.CloudDocumentId, bytContent, objCurrent.Item2, _gameProfileId, _gameProfileFormat);
        _activeCharacter.CloudLastKnownRevisionId = objStatus.RevisionId;
        SaveActiveCharacterIfPossible();
        Status = BuildAcceptedStatus(objStatus);
        await RefreshAsync();
        return objStatus;
    }

    public async Task PushSelectedSharedDocumentAsync()
    {
        if (_activeCharacter == null || SelectedDocument?.Document is not RunnersPointSharedDocument objDocument)
            return;

        Status = T("String_Cloud_Pushing");
        byte[] bytContent = SerializeActiveCharacter();
        Tuple<RunnersPointSharedDocument, string> objCurrent = await _apiClient.GetSharedDocumentAsync(objDocument.Id);
        if (CloudDocumentEntryViewModel.InFlightStates.Contains(objCurrent.Item1.ValidationState))
            throw new InvalidOperationException(T("String_Cloud_PushInFlight"));

        RunnersPointRevisionStatus objStatus = await _apiClient.PushSharedDocumentRevisionAsync(
            objDocument.Id, bytContent, objCurrent.Item2, _gameProfileId, _gameProfileFormat);

        Status = BuildAcceptedStatus(objStatus);
        await RefreshAsync();
    }

    public async Task<Tuple<byte[], string>> DownloadSelectedDocumentAsync()
    {
        if (SelectedDocument == null)
            throw new InvalidOperationException(string.Format(T("String_Cloud_Error"), "No document selected."));

        bool blnShared = SelectedDocument.IsShared;
        RunnersPointDocument objCurrentDocument = blnShared
            ? (await _apiClient.GetSharedDocumentAsync(SelectedDocument.Id)).Item1
            : (await _apiClient.GetDocumentAsync(SelectedDocument.Id)).Item1;

        if (!CloudDocumentEntryViewModel.DownloadableStates.Contains(objCurrentDocument.ValidationState))
            throw new InvalidOperationException(string.Format(T("String_Cloud_NotDownloadable"), objCurrentDocument.ValidationState));

        Status = T("String_Cloud_Downloading");
        Tuple<byte[], string> objDownload = blnShared
            ? await _apiClient.DownloadSharedDocumentRevisionAsync(objCurrentDocument.Id, objCurrentDocument.CurrentRevision)
            : await _apiClient.DownloadRevisionAsync(objCurrentDocument.Id, objCurrentDocument.CurrentRevision);
        Status = T("String_Cloud_Ready");
        return objDownload;
    }

    public async Task ArchiveOrUnarchiveSelectedDocumentAsync()
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

    private static string BuildAcceptedStatus(RunnersPointRevisionStatus objStatus)
    {
        return objStatus.Messages.Count == 0
            ? T("String_Cloud_PushAccepted")
            : T("String_Cloud_PushAccepted") + " " + string.Join(" ", objStatus.Messages);
    }
}
