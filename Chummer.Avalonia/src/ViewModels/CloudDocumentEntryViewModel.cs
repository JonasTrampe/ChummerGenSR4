using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

public sealed class CloudDocumentEntryViewModel : ViewModelBase
{
    public static readonly string[] InFlightStates = { "quarantined", "processing" };
    public static readonly string[] DownloadableStates = { "accepted" };

    public RunnersPointDocument Document { get; }
    public bool IsShared { get; }

    public string Id => Document.Id;
    public string DisplayName => string.IsNullOrWhiteSpace(Document.DisplayName) ? Document.Id : Document.DisplayName;
    public string ValidationState => Document.ValidationState;
    public string UpdatedAt => Document.UpdatedAt == default ? "–" : Document.UpdatedAt.ToString("g");
    public int? FolderId => IsShared && Document is RunnersPointSharedDocument objShared ? objShared.RecipientFolderId : Document.FolderId;
    public bool IsArchived => Document.ValidationState == "archived";
    public bool CanPushShared => Document is RunnersPointSharedDocument objSharedDocument
        && objSharedDocument.Permission == "write"
        && objSharedDocument.ShareStatus == "active";
    public bool CanPurge => !IsShared || Document is RunnersPointSharedDocument objSharedPurge
        && objSharedPurge.Permission == "purge"
        && objSharedPurge.ShareStatus == "active";
    public string ShareInfo
    {
        get
        {
            if (Document is not RunnersPointSharedDocument objShared)
                return string.Empty;

            string strShare = objShared.Permission + " (" + objShared.ShareStatus;
            if (objShared.ExpiresAt.HasValue)
                strShare += ", expires " + objShared.ExpiresAt.Value.ToLocalTime().ToString("d");
            return strShare + ")";
        }
    }

    public CloudDocumentEntryViewModel(RunnersPointDocument objDocument, bool blnIsShared)
    {
        Document = objDocument;
        IsShared = blnIsShared;
    }
}
