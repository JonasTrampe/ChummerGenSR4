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
