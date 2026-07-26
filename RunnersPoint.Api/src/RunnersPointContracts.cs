#nullable enable

using System;
using System.Collections.Generic;
using System.Net;

namespace RunnersPoint.Api
{
    public class RunnersPointDocument
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string GameProfileId { get; set; } = string.Empty;
        public string Format { get; set; } = string.Empty;
        public string SchemaVersion { get; set; } = string.Empty;
        public string CurrentRevision { get; set; } = string.Empty;
        public string ValidationState { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int? FolderId { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class RunnersPointDocumentPage
    {
        public List<RunnersPointDocument> Items { get; set; } = new();
        public string? NextCursor { get; set; }
    }

    /// <summary>
    /// A directed relationship between two RunnersPoint documents. <see cref="TargetDocumentId"/>
    /// always identifies the live target document; when <see cref="TargetRevisionId"/> is set, the
    /// relationship deliberately pins that target to the specified historical revision.
    /// </summary>
    public class RunnersPointDocumentLink
    {
        public string Id { get; set; } = string.Empty;
        public string OwnerDocumentId { get; set; } = string.Empty;
        public string TargetDocumentId { get; set; } = string.Empty;
        public string? TargetRevisionId { get; set; }
        public string RelationType { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class RunnersPointDocumentLinkPage
    {
        public List<RunnersPointDocumentLink> Items { get; set; } = new();
        public string? NextCursor { get; set; }
    }

    public class RunnersPointRevisionStatus
    {
        public string DocumentId { get; set; } = string.Empty;
        public string RevisionId { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public List<string> Messages { get; set; } = new();
    }

    public class RunnersPointGameProfile
    {
        public string Id { get; set; } = string.Empty;
        public string System { get; set; } = string.Empty;
        public string Edition { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public List<string> Formats { get; set; } = new();
    }

    public class RunnersPointDocumentFormatCapability
    {
        public string MediaType { get; set; } = string.Empty;
        public long MaxUploadBytes { get; set; }
    }

    public class RunnersPointDocumentTypeCapability
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public List<RunnersPointDocumentFormatCapability> Formats { get; set; } = new();
    }

    public class RunnersPointCapabilities
    {
        public string ApiVersion { get; set; } = "";
        public List<RunnersPointGameProfile> GameProfiles { get; set; } = [];
        public List<RunnersPointDocumentTypeCapability> DocumentTypes { get; set; } = [];
        public List<string> Formats { get; set; } = [];
        public long MaxUploadBytes { get; set; }
    }

    public class RunnersPointSharedDocument : RunnersPointDocument
    {
        public int? RecipientFolderId { get; set; }
        public string Permission { get; set; } = string.Empty;
        public string ShareStatus { get; set; } = string.Empty;
        public DateTime? ExpiresAt { get; set; }
    }

    public class RunnersPointFolder
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int? ParentFolderId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class RunnersPointSharedDocumentPage
    {
        public List<RunnersPointSharedDocument> Items { get; set; } = new();
        public string? NextCursor { get; set; }
    }

    public class RunnersPointRevision
    {
        public string Id { get; set; } = "";
        public string DocumentId { get; set; } = "";
        public string Hash { get; set; } = "";
        public long SizeBytes { get; set; }
        public string ValidationState { get; set; } = "";
        public List<string> ValidationMessages { get; set; } = [];
        public DateTime CreatedAt { get; set; }
    }

    public class RunnersPointApiException : Exception
    {
        public RunnersPointApiException(HttpStatusCode statusCode, string title, string problemCode, string correlationId) :
            base(title)
        {
            StatusCode = statusCode;
            ProblemCode = problemCode;
            CorrelationId = correlationId;
        }

        public HttpStatusCode StatusCode { get; private set; }
        public string ProblemCode { get; private set; }
        public string CorrelationId { get; private set; }
    }
}
