# RunnersPoint.Api

Standalone .NET client for the RunnersPoint document-storage API. It contains the public wire
contracts, `IRunnersPointApiClient`, `RunnersPointApiClient`, and the cross-platform
`RunnersPointAuth` token provider; it has no dependency on Chummer or Avalonia.

```csharp
using RunnersPoint.Api;

var auth = new RunnersPointAuth();
auth.SetApiToken("rp_...");
var api = new RunnersPointApiClient(auth, "https://runners-point.link/api/v1");
var folders = await api.ListFoldersAsync();
```

Hosts may instead implement `IRunnersPointAuth` and pass their own token storage/provider to the
client. The default `RunnersPointAuth` uses DPAPI on Windows and `secret-tool`/libsecret on Linux.

## Document links

The API supports directed, typed links between documents. A link always has an owner document,
target document, and generic lower-case `relationType` (for example `companion`). It can optionally
have a `targetRevisionId`: omit it to follow the target document's current revision, or supply it to
pin the link to a historical snapshot.

```csharp
var link = await api.CreateDocumentLinkAsync(runnerId, companionId, "companion");

var snapshot = await api.CreateDocumentLinkAsync(runnerId, companionId, "companion",
    strTargetRevisionId: companionRevisionId);
```

Server contract:

- `GET /documents/{ownerDocumentId}/links?pageSize={n}&cursor={cursor}` lists outgoing links.
- `POST /documents/{ownerDocumentId}/links` creates a link with `targetDocumentId`,
  `relationType`, optional `targetRevisionId`, and optional `displayName`.
- `DELETE /document-links/{linkId}` deletes the dedicated link resource.
- Link graphs must be acyclic. The server rejects a new link that creates a cycle.
- Deleting a target document is rejected while incoming links exist.
- Sharing a document can include its linked documents. Those grants are recorded as inherited
  grants and are removed when the source share or link is removed; direct grants remain intact.
