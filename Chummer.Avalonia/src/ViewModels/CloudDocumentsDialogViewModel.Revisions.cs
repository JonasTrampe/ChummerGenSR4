using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RunnersPoint.Api;

namespace Chummer.NewUI.ViewModels;

public sealed partial class CloudDocumentsDialogViewModel
{
    public Task<List<RunnersPointRevision>> ListRevisionsAsync(string strDocumentId, bool blnShared)
    {
        return blnShared
            ? _apiClient.ListSharedRevisionsAsync(strDocumentId)
            : _apiClient.ListRevisionsAsync(strDocumentId);
    }

    public Task<Tuple<byte[], string>> DownloadRevisionAsync(string strDocumentId, string strRevisionId, bool blnShared)
    {
        return blnShared
            ? _apiClient.DownloadSharedDocumentRevisionAsync(strDocumentId, strRevisionId)
            : _apiClient.DownloadRevisionAsync(strDocumentId, strRevisionId);
    }

    public Task<Tuple<RunnersPointDocument, string>> GetDocumentAsync(string strDocumentId)
    {
        return _apiClient.GetDocumentAsync(strDocumentId);
    }

    public async Task<Tuple<RunnersPointDocument, string>> GetDocumentForRevisionDialogAsync(string strDocumentId, bool blnShared)
    {
        if (blnShared)
        {
            Tuple<RunnersPointSharedDocument, string> objShared = await _apiClient.GetSharedDocumentAsync(strDocumentId);
            return new Tuple<RunnersPointDocument, string>(objShared.Item1, objShared.Item2);
        }

        return await _apiClient.GetDocumentAsync(strDocumentId);
    }

    public async Task PurgeRevisionAsync(string strDocumentId, string strRevisionId, bool blnShared)
    {
        string strIfMatch = (await GetDocumentForRevisionDialogAsync(strDocumentId, blnShared)).Item2;
        if (blnShared)
            await _apiClient.PurgeSharedRevisionAsync(strDocumentId, strRevisionId, strIfMatch);
        else
            await _apiClient.PurgeRevisionAsync(strDocumentId, strRevisionId, strIfMatch);
    }

    public async Task PurgeDocumentAsync(string strDocumentId, bool blnShared)
    {
        string strIfMatch = (await GetDocumentForRevisionDialogAsync(strDocumentId, blnShared)).Item2;
        if (blnShared)
            await _apiClient.PurgeSharedDocumentAsync(strDocumentId, strIfMatch);
        else
            await _apiClient.PurgeDocumentAsync(strDocumentId, strIfMatch);
    }

    public async Task UpdateActiveCharacterMetadataAsync(string strDisplayName, string strDescription, string strImageUrl)
    {
        if (_activeCharacter == null)
            return;

        string strTrimmedImageUrl = strImageUrl.Trim();
        if (!string.IsNullOrWhiteSpace(strTrimmedImageUrl) && !strTrimmedImageUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(T("Message_CloudMetadata_ImageUrlMustBeHttps"));

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
}
