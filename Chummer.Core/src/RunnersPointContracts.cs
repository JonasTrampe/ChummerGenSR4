using System;
using System.Collections.Generic;
using System.Net;

namespace Chummer.Core
{
    public class RunnersPointDocument
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public string GameProfileId { get; set; }
        public string Format { get; set; }
        public string SchemaVersion { get; set; }
        public string CurrentRevision { get; set; }
        public string ValidationState { get; set; }
        public string DisplayName { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class RunnersPointDocumentPage
    {
        public List<RunnersPointDocument> Items { get; set; } = new();
        public string NextCursor { get; set; }
    }

    public class RunnersPointRevisionStatus
    {
        public string DocumentId { get; set; }
        public string RevisionId { get; set; }
        public string State { get; set; }
        public List<string> Messages { get; set; } = new();
    }

    public class RunnersPointGameProfile
    {
        public string Id { get; set; }
        public string System { get; set; }
        public string Edition { get; set; }
        public string DisplayName { get; set; }
        public List<string> Formats { get; set; } = new();
    }

    public class RunnersPointDocumentFormatCapability
    {
        public string MediaType { get; set; }
        public long MaxUploadBytes { get; set; }
    }

    public class RunnersPointDocumentTypeCapability
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
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
        public string Permission { get; set; }
        public string ShareStatus { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }

    public class RunnersPointSharedDocumentPage
    {
        public List<RunnersPointSharedDocument> Items { get; set; } = new();
        public string NextCursor { get; set; }
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