using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Chummer.Core
{
    /// <summary>
    ///     Cross-platform contract for RunnersPoint document storage and sharing operations.
    /// </summary>
    public interface IRunnersPointApiClient
    {
        Task<RunnersPointCapabilities> GetCapabilitiesAsync();

        Task<RunnersPointDocumentPage> ListDocumentsAsync(string strGameProfileId, string strCursor = "",
            int intPageSize = 25);

        Task<Tuple<RunnersPointDocument, string>> GetDocumentAsync(string strDocumentId);
        Task<RunnersPointRevisionStatus> CreateDocumentAsync(byte[] bytContent, string strGameProfileId, string strFormat);

        Task<RunnersPointRevisionStatus> PushRevisionAsync(string strDocumentId, byte[] bytContent, string strIfMatch,
            string strGameProfileId, string strFormat);

        Task<RunnersPointRevisionStatus> GetRevisionStatusAsync(string strRevisionId);
        Task<Tuple<byte[], string>> DownloadRevisionAsync(string strDocumentId, string strRevisionId);
        Task ArchiveDocumentAsync(string strDocumentId, string strIfMatch);
        Task<RunnersPointDocument> UnarchiveDocumentAsync(string strDocumentId, string strIfMatch);

        Task<RunnersPointDocument> UpdateDocumentMetadataAsync(string strDocumentId, string strIfMatch,
            string strDisplayName, string strDescription, string strImageUrl);

        Task<RunnersPointSharedDocumentPage> ListSharedDocumentsAsync(string strGameProfileId, string strCursor = "",
            int intPageSize = 25);

        Task<Tuple<RunnersPointSharedDocument, string>> GetSharedDocumentAsync(string strDocumentId);

        Task<RunnersPointRevisionStatus> PushSharedDocumentRevisionAsync(string strDocumentId, byte[] bytContent,
            string strIfMatch, string strGameProfileId, string strFormat);

        Task<RunnersPointSharedDocument> UpdateSharedDocumentMetadataAsync(string strDocumentId, string strIfMatch,
            string strDisplayName, string strDescription, string strImageUrl);

        Task<Tuple<byte[], string>> DownloadSharedDocumentRevisionAsync(string strDocumentId, string strRevisionId);
        Task<List<RunnersPointRevision>> ListRevisionsAsync(string strDocumentId);
        Task<List<RunnersPointRevision>> ListSharedRevisionsAsync(string strDocumentId);
        Task PurgeDocumentAsync(string strDocumentId, string strIfMatch);
        Task PurgeSharedDocumentAsync(string strDocumentId, string strIfMatch);
        Task PurgeRevisionAsync(string strDocumentId, string strRevisionId, string strIfMatch);
        Task PurgeSharedRevisionAsync(string strDocumentId, string strRevisionId, string strIfMatch);
        Task<string> GetDebugDumpAsync(string strDocumentId);
    }
}
