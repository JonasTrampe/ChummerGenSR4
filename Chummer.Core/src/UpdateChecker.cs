#nullable enable
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Chummer.Core;

/// <summary>Ported from frmUpdate.cs's CheckForUpdate/ParseReleaseVersion: checks GitHub's
/// Releases API for a newer version than the one currently running. Legacy's own already-adapted
/// version (see commit "Remove dead WCF Omae feature and legacy update checker (Phase 0)") already
/// dropped the old per-file self-replacing updater in favor of just opening the release page in
/// the browser - that behavior carries over unchanged here, just re-hosted for Avalonia/HttpClient
/// instead of WinForms/WebClient.</summary>
public static class UpdateChecker
{
    public const string GitHubLatestReleaseApiUrl =
        "https://api.github.com/repos/JonasTrampe/ChummerGenSR4/releases/latest";

    public sealed record UpdateCheckResult(string LatestVersion, string ReleaseNotes, string ReleaseUrl);

    /// <summary>Parses a version string that may have a leading "v" and/or a pre-release or
    /// build-metadata suffix (e.g. "v0.1.501-beta+abc123") down to the plain Major.Minor.Build
    /// form used to compare a release tag against the running version.</summary>
    public static Version? ParseReleaseVersion(string strVersion)
    {
        string strTrimmed = strVersion.TrimStart('v', 'V');
        int intSuffixIndex = strTrimmed.IndexOfAny(new[] { '-', '+' });
        if (intSuffixIndex >= 0)
            strTrimmed = strTrimmed[..intSuffixIndex];

        try
        {
            Version objParsed = new(strTrimmed);
            return new Version(objParsed.Major, objParsed.Minor, Math.Max(objParsed.Build, 0));
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException)
        {
            return null;
        }
    }

    /// <summary>Parses a GitHub Releases API response and compares its tag against <paramref
    /// name="strCurrentVersion"/>. Returns null if the response is malformed, unparsable, or not
    /// newer than the current version - matching frmUpdate.cs's "no new updates"/"cannot connect"
    /// cases, which both just quietly close without showing a dialog in silent mode.</summary>
    public static UpdateCheckResult? ParseReleaseJson(string strJson, string strCurrentVersion)
    {
        JsonDocument objDocument;
        try
        {
            objDocument = JsonDocument.Parse(strJson);
        }
        catch (JsonException)
        {
            return null;
        }

        using (objDocument)
        {
            JsonElement objRoot = objDocument.RootElement;
            if (!objRoot.TryGetProperty("tag_name", out JsonElement objTagName)
                || objTagName.GetString() is not { Length: > 0 } strTagName)
                return null;

            Version? objLatest = ParseReleaseVersion(strTagName);
            Version? objCurrent = ParseReleaseVersion(strCurrentVersion);
            if (objLatest == null || objCurrent == null || objLatest <= objCurrent)
                return null;

            string strReleaseUrl = objRoot.TryGetProperty("html_url", out JsonElement objHtmlUrl)
                ? objHtmlUrl.GetString() ?? string.Empty
                : string.Empty;
            if (string.IsNullOrEmpty(strReleaseUrl))
                strReleaseUrl = "https://github.com/JonasTrampe/ChummerGenSR4/releases/latest";

            string strNotes = objRoot.TryGetProperty("body", out JsonElement objBody)
                ? objBody.GetString() ?? string.Empty
                : string.Empty;

            return new UpdateCheckResult(strTagName, strNotes, strReleaseUrl);
        }
    }

    /// <summary>Fetches and parses the latest release. Returns null on any network/parse failure
    /// or if already up to date - callers in silent mode should treat null as "nothing to show".</summary>
    public static async Task<UpdateCheckResult?> CheckForUpdateAsync(HttpClient objClient, string strCurrentVersion)
    {
        try
        {
            using var objRequest = new HttpRequestMessage(HttpMethod.Get, GitHubLatestReleaseApiUrl);
            // GitHub's API rejects requests with no User-Agent header.
            objRequest.Headers.UserAgent.ParseAdd("ChummerGenSR4-UpdateCheck");
            using HttpResponseMessage objResponse = await objClient.SendAsync(objRequest).ConfigureAwait(false);
            if (!objResponse.IsSuccessStatusCode)
                return null;

            string strJson = await objResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
            return ParseReleaseJson(strJson, strCurrentVersion);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }
}
