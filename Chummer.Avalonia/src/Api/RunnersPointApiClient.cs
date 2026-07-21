using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Chummer.Core;
using Serilog;

namespace Chummer.NewUI.Api
{
	/// <summary>
	/// Thin wrapper over the RunnersPoint Character Document Storage API (v1, draft.6). Covers personal
	/// storage/sync of `character` documents, plus read-only-or-write access to documents explicitly
	/// shared with the authenticated user via `/shared/documents/*` (per-document grants only - there is
	/// no public marketplace/discovery surface in this API; that lives on the RunnersPoint website).
	/// </summary>
	public class RunnersPointApiClient : IRunnersPointApiClient
	{
		// Configurable via Options > Cloud Documents server (GlobalOptions.CloudApiBaseUrl), so it can
		// point at a local dev server, a staging deployment, or eventually a real production host
		// without a rebuild. Defaults to the local Symfony dev server's actual route prefix (/api/v1,
		// not the /v1 the OpenAPI spec's example server URL still shows).
		private readonly string _strBaseUrl;
		private const string ClientName = "ChummerGenSR4";
		// System.Net.Http.HttpMethod.Patch isn't available on the .NET Framework 4.8 build of
		// System.Net.Http (it was added well after that assembly shipped) - construct it explicitly.
		private static readonly HttpMethod PatchMethod = new HttpMethod("PATCH");

		private readonly HttpClient _objHttpClient;
		private readonly IRunnersPointAuth _objAuth;

		public RunnersPointApiClient(IRunnersPointAuth objAuth)
			: this(objAuth, GlobalOptions.Instance.CloudApiBaseUrl)
		{
		}

		/// <summary>
		/// Create a client using a host-supplied API endpoint. This keeps the transport reusable by
		/// the Avalonia host without coupling it to the legacy GlobalOptions singleton.
		/// </summary>
		public RunnersPointApiClient(IRunnersPointAuth objAuth, string? strBaseUrl)
		{
			_objAuth = objAuth;
			_strBaseUrl = (strBaseUrl ?? "").TrimEnd('/');
			_objHttpClient = new HttpClient();
			_objHttpClient.DefaultRequestHeaders.Add("X-Client-Name", ClientName);
			_objHttpClient.DefaultRequestHeaders.Add("X-Client-Version", System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString());
		}

		public RunnersPointApiClient(IRunnersPointAuth objAuth, RunnersPointApiOptions? objOptions)
			: this(objAuth, objOptions?.BaseUrl)
		{
		}

		private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod objMethod, string strPath, bool blnAuthenticated = true)
		{
			var objRequest = new HttpRequestMessage(objMethod, _strBaseUrl + strPath);
			if (blnAuthenticated)
			{
				var strToken = await _objAuth.GetAccessTokenAsync();
				objRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", strToken);
			}
			return objRequest;
		}

		private static string NewIdempotencyKey()
		{
			return Guid.NewGuid().ToString("N");
		}

		/// <summary>
		/// Gets a required textual API value. JSON object values are deliberately treated as untrusted:
		/// a present JSON null is equivalent to a missing value for the wire contracts in Chummer.Core.
		/// </summary>
		private static string GetString(Dictionary<string, object> objJson, string strName)
		{
			return objJson.TryGetValue(strName, out var objValue) ? objValue?.ToString() ?? string.Empty : string.Empty;
		}

		private static string? GetOptionalString(Dictionary<string, object> objJson, string strName)
		{
			return objJson.TryGetValue(strName, out var objValue) ? objValue?.ToString() : null;
		}

		/// <summary>
		/// Reads the raw ETag header value, bypassing HttpResponseMessage.Headers.ETag's strongly-typed
		/// EntityTagHeaderValue parsing. The server sends ETag as a bare opaque token (e.g. a revision
		/// UUID) without the DQUOTEs RFC 7232 requires around an entity-tag; .NET's typed parser silently
		/// refuses anything unquoted and leaves Headers.ETag null instead of throwing, which meant every
		/// If-Match sent back (pushRevision, archive) was empty and got rejected. Reading the header as
		/// plain text and echoing it back exactly as received round-trips correctly against this server,
		/// even though it isn't RFC-conformant.
		/// </summary>
		public static string? ExtractRawETag(HttpResponseMessage objResponse)
		{
			return objResponse.Headers.TryGetValues("ETag", out var lstValues) ? lstValues.FirstOrDefault() : null;
		}

		/// <summary>
		/// Sends an authenticated request built by requestFactory. If the server responds 401, forces a
		/// token refresh and retries once with a freshly-built request (a request/its content can't be
		/// resent as-is once sent, hence rebuilding rather than reusing the same HttpRequestMessage). If
		/// there's nothing to refresh (a pasted apiToken) or the refresh itself fails, the original 401
		/// response is returned as-is for ThrowIfProblemAsync to turn into the usual auth-expired path.
		/// requestFactory must be safe to call twice - callers building a POST/PUT with a stable
		/// Idempotency-Key should compute that key once, outside the factory, and only rebuild the
		/// request/content inside it.
		/// </summary>
		private async Task<HttpResponseMessage> SendWithRetryAsync(Func<Task<HttpRequestMessage>> requestFactory)
		{
			var objRequest = await requestFactory();
			var objResponse = await _objHttpClient.SendAsync(objRequest);
			if (objResponse.StatusCode != HttpStatusCode.Unauthorized)
				return objResponse;

			if (!await _objAuth.TryForceRefreshAsync())
				return objResponse;

			Log.Information("RunnersPoint API call got 401 - refreshed the access token and retrying once");
			objResponse.Dispose();
			var objRetryRequest = await requestFactory();
			return await _objHttpClient.SendAsync(objRetryRequest);
		}

		private async Task ThrowIfProblemAsync(HttpResponseMessage objResponse)
		{
			if (objResponse.IsSuccessStatusCode)
				return;

			var strTitle = objResponse.ReasonPhrase;
			var strCode = "";
			var strCorrelationId = "";
			try
			{
				var strBody = await objResponse.Content.ReadAsStringAsync();
				if (!string.IsNullOrEmpty(strBody))
				{
		
					var objProblem = JsonSerializer.Deserialize<Dictionary<string, object>>(strBody);
					if (objProblem == null)
						throw new ArgumentNullException(nameof(objProblem));
					
					if (objProblem.TryGetValue("title", out var title))
						strTitle = title.ToString();
					if (objProblem.TryGetValue("code", out var code))
						strCode = code.ToString();
					if (objProblem.TryGetValue("correlationId", out var correlationId))
						strCorrelationId = correlationId.ToString();
				}
			}
			catch
			{
				// Todo: Add logging!
				// Problem body wasn't valid JSON (or wasn't Problem-shaped) - fall back to the status line.
			}

			var strMethod = objResponse.RequestMessage?.Method?.Method ?? "?";
			var strPath = objResponse.RequestMessage?.RequestUri?.PathAndQuery ?? "?";

			if (objResponse.StatusCode == (HttpStatusCode)429)
			{
				var strRetryAfter = objResponse.Headers.TryGetValues("Retry-After", out var lstRetryAfter) ? string.Join(",", lstRetryAfter) : "unknown";
				Log.Warning("RunnersPoint API {Method} {Path} rate limited - retry after {RetryAfter}s (correlationId={CorrelationId})", strMethod, strPath, strRetryAfter, strCorrelationId);
				throw new RunnersPointApiException(objResponse.StatusCode, "Rate limited - retry after " + strRetryAfter + "s", strCode!, strCorrelationId!);
			}

			Log.Warning("RunnersPoint API {Method} {Path} failed with {StatusCode}: {Title} (code={Code}, correlationId={CorrelationId})",
				strMethod, strPath, (int)objResponse.StatusCode, strTitle, strCode, strCorrelationId);
			throw new RunnersPointApiException(objResponse.StatusCode, strTitle!, strCode!, strCorrelationId!);
		}

		/// <summary>
		/// GET /capabilities. Unauthenticated. Used at startup to resolve the SR4 GameProfileId and its
		/// supported formats/maximum upload size.
		/// </summary>
		public async Task<RunnersPointCapabilities> GetCapabilitiesAsync()
		{
			var objRequest = await CreateRequestAsync(HttpMethod.Get, "/capabilities", false);
			var objResponse = await _objHttpClient.SendAsync(objRequest);
			await ThrowIfProblemAsync(objResponse);

			var strBody = await objResponse.Content.ReadAsStringAsync();

			var objJson = JsonSerializer.Deserialize<Dictionary<string, object>>(strBody);

			if (objJson == null)
				return new RunnersPointCapabilities();
			
			var objCapabilities = new RunnersPointCapabilities();
			objCapabilities.ApiVersion = GetString(objJson, "apiVersion");
			objCapabilities.MaxUploadBytes = objJson.TryGetValue("maxUploadBytes", out var value1) ? Convert.ToInt64(value1) : 0;

			if (objJson.TryGetValue("formats", out var value2))
			{
				foreach (var objFormat in (IEnumerable)value2)
					objCapabilities.Formats.Add(objFormat?.ToString() ?? string.Empty);
			}

			if (objJson.TryGetValue("gameProfiles", out var value3))
			{
				foreach (var objProfileObj in (IEnumerable)value3)
				{
					var objProfile = (Dictionary<string, object>)objProfileObj;
					var objGameProfile = new RunnersPointGameProfile();
					objGameProfile.Id = GetString(objProfile, "id");
					objGameProfile.System = GetString(objProfile, "system");
					objGameProfile.Edition = GetString(objProfile, "edition");
					objGameProfile.DisplayName = GetString(objProfile, "displayName");
					if (objProfile.TryGetValue("formats", out var value8))
					{
						foreach (var objFormat in (IEnumerable)value8)
							objGameProfile.Formats.Add(objFormat?.ToString() ?? string.Empty);
					}
					objCapabilities.GameProfiles.Add(objGameProfile);
				}
			}

			if (objJson.TryGetValue("documentTypes", out var value9))
			{
				foreach (var objTypeObj in (IEnumerable)value9)
				{
					var objType = (Dictionary<string, object>)objTypeObj;
					var objTypeCapability = new RunnersPointDocumentTypeCapability();
					objTypeCapability.Id = GetString(objType, "id");
					objTypeCapability.DisplayName = GetString(objType, "displayName");
					if (objType.TryGetValue("formats", out var value6))
					{
						foreach (var objFormatObj in (IEnumerable)value6)
						{
							var objFormat = (Dictionary<string, object>)objFormatObj;
							objTypeCapability.Formats.Add(new RunnersPointDocumentFormatCapability
							{
								MediaType = GetString(objFormat, "mediaType"),
								MaxUploadBytes = objFormat.TryGetValue("maxUploadBytes", out var value8) ? Convert.ToInt64(value8) : 0,
							});
						}
					}
					objCapabilities.DocumentTypes.Add(objTypeCapability);
				}
			}

			return objCapabilities;
		}

		/// <summary>
		/// GET /documents. Documents owned by the authenticated user, optionally filtered.
		/// </summary>
		public async Task<RunnersPointDocumentPage> ListDocumentsAsync(string strGameProfileId, string? strCursor = null, int intPageSize = 25)
		{
			var strPath = "/documents?gameProfileId=" + Uri.EscapeDataString(strGameProfileId) + "&pageSize=" + intPageSize;
			if (!string.IsNullOrEmpty(strCursor))
				strPath += "&cursor=" + Uri.EscapeDataString(strCursor);

			var objResponse = await SendWithRetryAsync(() => CreateRequestAsync(HttpMethod.Get, strPath));
			await ThrowIfProblemAsync(objResponse);

			var strBody = await objResponse.Content.ReadAsStringAsync();

			var objJson = JsonSerializer.Deserialize<Dictionary<string, object>>(strBody);
			if (objJson == null)
				return new RunnersPointDocumentPage();
			
			var objPage = new RunnersPointDocumentPage
			{
				NextCursor = GetOptionalString(objJson, "nextCursor")
			};

			if (objJson.TryGetValue("items", out var items))
			{
				foreach (var objItemObj in (IEnumerable)items)
					objPage.Items.Add(ParseDocument((Dictionary<string, object>)objItemObj));
			}

			return objPage;
		}

		public static RunnersPointDocument ParseDocument(Dictionary<string, object>? objJson)
		{
			if (objJson == null)
			{
				throw new InvalidOperationException("Got an empty object");
			}
			
			var objDocument = new RunnersPointDocument();
			objDocument.Id = GetString(objJson, "id");
			objDocument.Type = GetString(objJson, "type");
			objDocument.GameProfileId = GetString(objJson, "gameProfileId");
			objDocument.Format = GetString(objJson, "format");
			objDocument.SchemaVersion = GetString(objJson, "schemaVersion");
			objDocument.CurrentRevision = GetString(objJson, "currentRevision");
			objDocument.ValidationState = GetString(objJson, "validationState");
			if (objJson.ContainsKey("metadata") && objJson["metadata"] is Dictionary<string, object>)
			{
				var objMetadata = (Dictionary<string, object>)objJson["metadata"];
				if (objMetadata.TryGetValue("displayName", out var value7))
					objDocument.DisplayName = value7?.ToString() ?? string.Empty;
				// The server's character-document extractor currently populates metadata.name (the
				// charactername/name XML element), not metadata.displayName as the spec and this client
				// otherwise expect - fall back to it so the list doesn't just show raw document IDs for
				// every character until that's reconciled server-side.
				else if (objMetadata.TryGetValue("name", out var value8))
					objDocument.DisplayName = value8?.ToString() ?? string.Empty;
			}
			if (objJson.ContainsKey("updatedAt") && DateTime.TryParse(objJson["updatedAt"].ToString(), out var datUpdatedAt))
				objDocument.UpdatedAt = datUpdatedAt;
			return objDocument;
		}

		/// <summary>
		/// Parses a SharedDocument - the same envelope as Document, plus a required `share` grant
		/// (permission/status/expiresAt) describing what the authenticated user is allowed to do with it.
		/// </summary>
		public static RunnersPointSharedDocument ParseSharedDocument(Dictionary<string, object>? objJson)
		{
			var objBase = ParseDocument(objJson);
			var objShared = new RunnersPointSharedDocument
			{
				Id = objBase.Id,
				Type = objBase.Type,
				GameProfileId = objBase.GameProfileId,
				Format = objBase.Format,
				SchemaVersion = objBase.SchemaVersion,
				CurrentRevision = objBase.CurrentRevision,
				ValidationState = objBase.ValidationState,
				DisplayName = objBase.DisplayName,
				UpdatedAt = objBase.UpdatedAt
			};

			if (objJson.ContainsKey("share") && objJson["share"] is Dictionary<string, object> objShare)
			{
				objShared.Permission = GetString(objShare, "permission");
				objShared.ShareStatus = GetString(objShare, "status");
				if (objShare.ContainsKey("expiresAt") && objShare["expiresAt"] != null
					&& DateTime.TryParse(objShare["expiresAt"].ToString(), out var datExpiresAt))
					objShared.ExpiresAt = datExpiresAt;
			}

			return objShared;
		}

		/// <summary>
		/// GET /documents/{documentId}. Returns the document plus its current ETag (needed for a
		/// subsequent pushRevision/archive call's If-Match).
		/// </summary>
		public async Task<Tuple<RunnersPointDocument, string>> GetDocumentAsync(string strDocumentId)
		{
			var objResponse = await SendWithRetryAsync(() => CreateRequestAsync(HttpMethod.Get, "/documents/" + strDocumentId));
			await ThrowIfProblemAsync(objResponse);

			var strBody = await objResponse.Content.ReadAsStringAsync();

			var objDocument = ParseDocument(JsonSerializer.Deserialize<Dictionary<string, object>>(strBody));
			var strETag = ExtractRawETag(objResponse);
			return new Tuple<RunnersPointDocument, string>(objDocument, strETag);
		}

		/// <summary>
		/// POST /documents. Mints a brand-new document and its first revision. Do not call this again
		/// for a document that already has a CloudDocumentId - use PushRevisionAsync instead.
		/// </summary>
		public async Task<RunnersPointRevisionStatus> CreateDocumentAsync(byte[] bytContent, string strGameProfileId, string strFormat)
		{
			var strIdempotencyKey = NewIdempotencyKey();
			var objResponse = await SendWithRetryAsync(async () =>
			{
				var objRequest = await CreateRequestAsync(HttpMethod.Post, "/documents");
				objRequest.Headers.Add("Idempotency-Key", strIdempotencyKey);
				objRequest.Headers.Add("X-Document-Type", "character");
				objRequest.Headers.Add("X-Game-Profile-Id", strGameProfileId);
				objRequest.Headers.Add("X-Document-Format", strFormat);
				objRequest.Content = new ByteArrayContent(bytContent);
				objRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
				return objRequest;
			});
			await ThrowIfProblemAsync(objResponse);
			return await ParseRevisionStatusAsync(objResponse);
		}

		/// <summary>
		/// PUT /documents/{documentId}. Pushes new content for a document that already exists. The
		/// server hash-checks against existing revisions; identical content returns the existing
		/// revision rather than creating a duplicate. Requires the document's current ETag - a stale
		/// strIfMatch results in a RunnersPointApiException with StatusCode 412, meaning the caller
		/// should GetDocumentAsync for the latest ETag/content before retrying.
		/// </summary>
		public async Task<RunnersPointRevisionStatus> PushRevisionAsync(string strDocumentId, byte[] bytContent, string strIfMatch, string strGameProfileId, string strFormat)
		{
			var strIdempotencyKey = NewIdempotencyKey();
			var objResponse = await SendWithRetryAsync(async () =>
			{
				var objRequest = await CreateRequestAsync(HttpMethod.Put, "/documents/" + strDocumentId);
				objRequest.Headers.Add("Idempotency-Key", strIdempotencyKey);
				objRequest.Headers.TryAddWithoutValidation("If-Match", strIfMatch);
				objRequest.Headers.Add("X-Game-Profile-Id", strGameProfileId);
				objRequest.Headers.Add("X-Document-Format", strFormat);
				objRequest.Content = new ByteArrayContent(bytContent);
				objRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
				return objRequest;
			});
			await ThrowIfProblemAsync(objResponse);
			return await ParseRevisionStatusAsync(objResponse);
		}

		private async Task<RunnersPointRevisionStatus> ParseRevisionStatusAsync(HttpResponseMessage objResponse)
		{
			var strBody = await objResponse.Content.ReadAsStringAsync();

			var objJson = JsonSerializer.Deserialize<Dictionary<string, object>>(strBody);

			var objStatus = new RunnersPointRevisionStatus();
			objStatus.DocumentId = objJson.TryGetValue("documentId", out var value) ? value.ToString() : "";
			objStatus.RevisionId = objJson.TryGetValue("revisionId", out var value1) ? value1.ToString() : "";
			objStatus.State = objJson.TryGetValue("state", out var value2) ? value2.ToString() : "";
			if (objJson.TryGetValue("messages", out var value3))
			{
				foreach (var objMessage in (IEnumerable)value3)
					objStatus.Messages.Add(objMessage.ToString());
			}
			return objStatus;
		}

		/// <summary>
		/// GET /revision-status/{revisionId}. Called on-demand (e.g. when the user opens the Cloud
		/// Documents panel) - Chummer does not poll this in the background.
		/// </summary>
		public async Task<RunnersPointRevisionStatus> GetRevisionStatusAsync(string strRevisionId)
		{
			var objResponse = await SendWithRetryAsync(() => CreateRequestAsync(HttpMethod.Get, "/revision-status/" + strRevisionId));
			await ThrowIfProblemAsync(objResponse);
			return await ParseRevisionStatusAsync(objResponse);
		}

		/// <summary>
		/// GET /documents/{documentId}/revisions/{revisionId}/content. Downloads the accepted bytes
		/// for a specific revision (used to pull a document down to a new local .chum file). Verifies
		/// the response's Digest header (SHA-256 of the bytes) against what was actually received,
		/// since silently trusting an unverified download for a "store my character" feature is asking
		/// for a corrupted/truncated file to go unnoticed.
		/// </summary>
		public async Task<Tuple<byte[], string>> DownloadRevisionAsync(string strDocumentId, string strRevisionId)
		{
			var objResponse = await SendWithRetryAsync(() => CreateRequestAsync(HttpMethod.Get, "/documents/" + strDocumentId + "/revisions/" + strRevisionId + "/content"));
			await ThrowIfProblemAsync(objResponse);
			var bytContent = await objResponse.Content.ReadAsByteArrayAsync();

			IEnumerable<string> lstDigestValues;
			if (objResponse.Headers.TryGetValues("Digest", out lstDigestValues))
			{
				var strDigestHeader = lstDigestValues.FirstOrDefault();
				if (!string.IsNullOrEmpty(strDigestHeader) && !VerifyDigest(bytContent, strDigestHeader))
					throw new InvalidOperationException("Downloaded content for revision " + strRevisionId + " failed digest verification - the bytes received don't match the server's declared hash. Discarding rather than saving a possibly-corrupted file.");
			}

			return new Tuple<byte[], string>(bytContent, ParseSuggestedFileName(objResponse));
		}

		/// <summary>
		/// Extracts a suggested filename from a Content-Disposition response header, if the server sent
		/// one - not all deployments do (it's an optional hint per the spec), so callers should fall back
		/// to something derived from the document's own metadata when this returns null.
		/// </summary>
		public static string? ParseSuggestedFileName(HttpResponseMessage objResponse)
		{
			// Content-Disposition is a content header per RFC 7231, not a response header - HttpClient
			// parses it onto Content.Headers, never onto the top-level Headers collection, so looking
			// there would never find it even if the server sent one.
			if (objResponse.Content == null)
				return null;

			IEnumerable<string> lstValues;
			if (!objResponse.Content.Headers.TryGetValues("Content-Disposition", out lstValues))
				return null;

			var strHeader = lstValues.FirstOrDefault();
			if (string.IsNullOrEmpty(strHeader))
				return null;

			var intFilenameIndex = strHeader.IndexOf("filename=", StringComparison.OrdinalIgnoreCase);
			if (intFilenameIndex < 0)
				return null;

			var strFileName = strHeader.Substring(intFilenameIndex + "filename=".Length).Trim();
			var intSemicolon = strFileName.IndexOf(';');
			if (intSemicolon >= 0)
				strFileName = strFileName.Substring(0, intSemicolon);
			return strFileName.Trim().Trim('"');
		}

		/// <summary>
		/// Compares a SHA-256 hash of bytContent against a Digest header value. The spec only says
		/// "SHA-256 digest of the response bytes" without nailing down RFC 3230's usual
		/// "SHA-256=&lt;base64&gt;" framing vs. a bare hex string, so both are accepted.
		/// </summary>
		public static bool VerifyDigest(byte[] bytContent, string strDigestHeader)
		{
			var strValue = strDigestHeader;
			var intEquals = strValue.IndexOf('=');
			if (strValue.StartsWith("SHA-256=", StringComparison.OrdinalIgnoreCase) && intEquals >= 0)
				strValue = strValue.Substring(intEquals + 1);

			byte[] bytExpected;
			using (var objSha256 = SHA256.Create())
				bytExpected = objSha256.ComputeHash(bytContent);

			byte[] bytActual;
			var strHex = strValue.Replace("-", "");
			// A bare hex SHA-256 digest is 64 lowercase-hex characters - which is also, incidentally, a
			// syntactically valid (if semantically wrong) base64 string, since the hex alphabet is a
			// subset of base64's and 64 is a multiple of 4. Trying base64 first would silently decode
			// that into 48 wrong bytes instead of throwing, so hex-shaped input has to be checked first.
			if (strHex.Length % 2 == 0 && System.Text.RegularExpressions.Regex.IsMatch(strHex, "^[0-9a-fA-F]+$"))
			{
				bytActual = new byte[strHex.Length / 2];
				for (var i = 0; i < bytActual.Length; i++)
					bytActual[i] = Convert.ToByte(strHex.Substring(i * 2, 2), 16);
			}
			else
			{
				try
				{
					bytActual = Convert.FromBase64String(strValue);
				}
				catch (FormatException)
				{
					// Header present but not in a format we understand - don't fail the download over
					// a format mismatch we can't parse, but don't pretend we verified it either.
					return true;
				}
			}

			if (bytActual.Length != bytExpected.Length)
				return false;
			for (var i = 0; i < bytActual.Length; i++)
			{
				if (bytActual[i] != bytExpected[i])
					return false;
			}
			return true;
		}

		/// <summary>
		/// DELETE /documents/{documentId}. Archives the document per the server's retention policy.
		/// Requires the document's current ETag, same staleness handling as PushRevisionAsync.
		/// </summary>
		public async Task ArchiveDocumentAsync(string strDocumentId, string strIfMatch)
		{
			var strIdempotencyKey = NewIdempotencyKey();
			var objResponse = await SendWithRetryAsync(async () =>
			{
				var objRequest = await CreateRequestAsync(HttpMethod.Delete, "/documents/" + strDocumentId);
				objRequest.Headers.Add("Idempotency-Key", strIdempotencyKey);
				objRequest.Headers.TryAddWithoutValidation("If-Match", strIfMatch);
				return objRequest;
			});
			await ThrowIfProblemAsync(objResponse);
		}

		/// <summary>
		/// POST /documents/{documentId}/unarchive. Owner-only, like archive - restores validationState
		/// to mirror the current revision's own state (not unconditionally "accepted", since a document
		/// can be archived before its current revision ever reached that state). currentRevision and the
		/// document's ETag are unaffected.
		/// </summary>
		public async Task<RunnersPointDocument> UnarchiveDocumentAsync(string strDocumentId, string strIfMatch)
		{
			var strIdempotencyKey = NewIdempotencyKey();
			var objResponse = await SendWithRetryAsync(async () =>
			{
				var objRequest = await CreateRequestAsync(HttpMethod.Post, "/documents/" + strDocumentId + "/unarchive");
				objRequest.Headers.Add("Idempotency-Key", strIdempotencyKey);
				objRequest.Headers.TryAddWithoutValidation("If-Match", strIfMatch);
				return objRequest;
			});
			await ThrowIfProblemAsync(objResponse);
			var strBody = await objResponse.Content.ReadAsStringAsync();
			return ParseDocument(JsonSerializer.Deserialize<Dictionary<string, object>>(strBody));
		}

		/// <summary>
		/// Builds a JSON Merge Patch (RFC 7396) body over Document.metadata's three keys. Chummer's
		/// metadata editor always edits displayName/description/imageUrl together, so this always sends
		/// all three - an empty/null argument clears that field server-side (JSON null), a non-empty one
		/// sets it. Content-Type must be application/merge-patch+json per the spec.
		/// </summary>
		public static byte[] BuildMetadataPatchBody(string strDisplayName, string strDescription, string strImageUrl)
		{
			var objPatch = new Dictionary<string, object?>
			{
				{ "displayName", string.IsNullOrEmpty(strDisplayName) ? null : strDisplayName },
				{ "description", string.IsNullOrEmpty(strDescription) ? null : strDescription },
				{ "imageUrl", string.IsNullOrEmpty(strImageUrl) ? null : strImageUrl },
			};
			return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(objPatch));
		}

		/// <summary>
		/// PATCH /documents/{documentId}. Metadata-only edit - never creates a new Revision, so
		/// currentRevision/ETag are unaffected by this call (the response's ETag header still reflects
		/// the current revision, unchanged).
		/// </summary>
		public async Task<RunnersPointDocument> UpdateDocumentMetadataAsync(string strDocumentId, string strIfMatch, string strDisplayName, string strDescription, string strImageUrl)
		{
			var strIdempotencyKey = NewIdempotencyKey();
			var bytContent = BuildMetadataPatchBody(strDisplayName, strDescription, strImageUrl);
			var objResponse = await SendWithRetryAsync(async () =>
			{
				var objRequest = await CreateRequestAsync(PatchMethod, "/documents/" + strDocumentId);
				objRequest.Headers.Add("Idempotency-Key", strIdempotencyKey);
				objRequest.Headers.TryAddWithoutValidation("If-Match", strIfMatch);
				objRequest.Content = new ByteArrayContent(bytContent);
				objRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/merge-patch+json");
				return objRequest;
			});
			await ThrowIfProblemAsync(objResponse);
			var strBody = await objResponse.Content.ReadAsStringAsync();
			return ParseDocument(JsonSerializer.Deserialize<Dictionary<string, object>>(strBody));
		}

		/// <summary>
		/// GET /shared/documents. Documents explicitly shared with the authenticated user by another
		/// user - e.g. a GM sharing a character back, or a document someone picked up from a marketplace
		/// on the RunnersPoint website. Chummer only ever sees the individual grant; there is no public
		/// browsing here.
		/// </summary>
		public async Task<RunnersPointSharedDocumentPage> ListSharedDocumentsAsync(string strGameProfileId, string? strCursor = null, int intPageSize = 25)
		{
			var strPath = "/shared/documents?gameProfileId=" + Uri.EscapeDataString(strGameProfileId) + "&pageSize=" + intPageSize;
			if (!string.IsNullOrEmpty(strCursor))
				strPath += "&cursor=" + Uri.EscapeDataString(strCursor);

			var objResponse = await SendWithRetryAsync(() => CreateRequestAsync(HttpMethod.Get, strPath));
			await ThrowIfProblemAsync(objResponse);

			var strBody = await objResponse.Content.ReadAsStringAsync();

			var objJson = JsonSerializer.Deserialize<Dictionary<string, object>>(strBody);
			if (objJson == null)
				return null;
			
			var objPage = new RunnersPointSharedDocumentPage();
			objPage.NextCursor = objJson.TryGetValue("nextCursor", out var nextCursor) ? nextCursor.ToString() : null;

			if (objJson.TryGetValue("items", out var value))
			{
				foreach (var objItemObj in (IEnumerable)value)
					objPage.Items.Add(ParseSharedDocument((Dictionary<string, object>)objItemObj));
			}

			return objPage;
		}

		/// <summary>
		/// GET /shared/documents/{documentId}. Same ETag contract as GetDocumentAsync - needed before
		/// calling PushSharedDocumentRevisionAsync.
		/// </summary>
		public async Task<Tuple<RunnersPointSharedDocument, string>> GetSharedDocumentAsync(string strDocumentId)
		{
			var objResponse = await SendWithRetryAsync(() => CreateRequestAsync(HttpMethod.Get, "/shared/documents/" + strDocumentId));
			await ThrowIfProblemAsync(objResponse);

			var strBody = await objResponse.Content.ReadAsStringAsync();

			var objDocument = ParseSharedDocument(JsonSerializer.Deserialize<Dictionary<string, object>>(strBody));
			var strETag = ExtractRawETag(objResponse);
			return new Tuple<RunnersPointSharedDocument, string>(objDocument, strETag);
		}

		/// <summary>
		/// PUT /shared/documents/{documentId}. Requires an active write grant on the document - archive
		/// and delete are never available through shared access, only through the owner's own /documents.
		/// </summary>
		public async Task<RunnersPointRevisionStatus> PushSharedDocumentRevisionAsync(string strDocumentId, byte[] bytContent, string strIfMatch, string strGameProfileId, string strFormat)
		{
			var strIdempotencyKey = NewIdempotencyKey();
			var objResponse = await SendWithRetryAsync(async () =>
			{
				var objRequest = await CreateRequestAsync(HttpMethod.Put, "/shared/documents/" + strDocumentId);
				objRequest.Headers.Add("Idempotency-Key", strIdempotencyKey);
				objRequest.Headers.TryAddWithoutValidation("If-Match", strIfMatch);
				objRequest.Headers.Add("X-Game-Profile-Id", strGameProfileId);
				objRequest.Headers.Add("X-Document-Format", strFormat);
				objRequest.Content = new ByteArrayContent(bytContent);
				objRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
				return objRequest;
			});
			await ThrowIfProblemAsync(objResponse);
			return await ParseRevisionStatusAsync(objResponse);
		}

		/// <summary>
		/// PATCH /shared/documents/{documentId}. Metadata-only edit via shared access - requires an
		/// active grant with "update" permission specifically. A "write" grant alone is not sufficient:
		/// per the spec, write (content) and update (metadata) are independent capabilities that don't
		/// imply each other, so a collaborator needing both holds two separate grants. Archive/unarchive/
		/// delete remain unavailable through shared access regardless.
		/// </summary>
		public async Task<RunnersPointSharedDocument> UpdateSharedDocumentMetadataAsync(string strDocumentId, string strIfMatch, string strDisplayName, string strDescription, string strImageUrl)
		{
			var strIdempotencyKey = NewIdempotencyKey();
			var bytContent = BuildMetadataPatchBody(strDisplayName, strDescription, strImageUrl);
			var objResponse = await SendWithRetryAsync(async () =>
			{
				var objRequest = await CreateRequestAsync(PatchMethod, "/shared/documents/" + strDocumentId);
				objRequest.Headers.Add("Idempotency-Key", strIdempotencyKey);
				objRequest.Headers.TryAddWithoutValidation("If-Match", strIfMatch);
				objRequest.Content = new ByteArrayContent(bytContent);
				objRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/merge-patch+json");
				return objRequest;
			});
			await ThrowIfProblemAsync(objResponse);
			var strBody = await objResponse.Content.ReadAsStringAsync();

			return ParseSharedDocument(JsonSerializer.Deserialize<Dictionary<string, object>>(strBody));
		}

		/// <summary>
		/// GET /shared/documents/{documentId}/revisions/{revisionId}/content. Same Digest verification
		/// as DownloadRevisionAsync - a shared download is downloaded from someone else's storage, so
		/// verifying it wasn't corrupted or truncated in transit matters at least as much here.
		/// </summary>
		public async Task<Tuple<byte[], string>> DownloadSharedDocumentRevisionAsync(string strDocumentId, string strRevisionId)
		{
			var objResponse = await SendWithRetryAsync(() => CreateRequestAsync(HttpMethod.Get, "/shared/documents/" + strDocumentId + "/revisions/" + strRevisionId + "/content"));
			await ThrowIfProblemAsync(objResponse);
			var bytContent = await objResponse.Content.ReadAsByteArrayAsync();

			if (objResponse.Headers.TryGetValues("Digest", out var lstDigestValues))
			{
				var strDigestHeader = lstDigestValues.FirstOrDefault();
				if (!string.IsNullOrEmpty(strDigestHeader) && !VerifyDigest(bytContent, strDigestHeader))
					throw new InvalidOperationException("Downloaded content for shared revision " + strRevisionId + " failed digest verification - the bytes received don't match the server's declared hash. Discarding rather than saving a possibly-corrupted file.");
			}

			return new Tuple<byte[], string>(bytContent, ParseSuggestedFileName(objResponse));
		}

		internal static RunnersPointRevision ParseRevision(Dictionary<string, object> objJson)
		{
			var objRevision = new RunnersPointRevision();
			objRevision.Id = objJson.TryGetValue("id", out var value) ? value.ToString() : "";
			objRevision.DocumentId = objJson.TryGetValue("documentId", out var value1) ? value1.ToString() : "";
			objRevision.Hash = objJson.TryGetValue("hash", out var value2) ? value2.ToString() : "";
			objRevision.SizeBytes = objJson.TryGetValue("sizeBytes", out var value3) ? Convert.ToInt64(value3) : 0;
			objRevision.ValidationState = objJson.TryGetValue("validationState", out var value4) ? value4.ToString() : "";
			if (objJson.TryGetValue("validationMessages", out var value5))
			{
				foreach (var objMessage in (IEnumerable)value5)
					objRevision.ValidationMessages.Add(objMessage.ToString());
			}
			if (objJson.ContainsKey("createdAt") && DateTime.TryParse(objJson["createdAt"].ToString(), out var datCreatedAt))
				objRevision.CreatedAt = datCreatedAt;
			return objRevision;
		}

		private async Task<List<RunnersPointRevision>> FetchRevisionsAsync(string strPath)
		{
			var objResponse = await SendWithRetryAsync(() => CreateRequestAsync(HttpMethod.Get, strPath));
			await ThrowIfProblemAsync(objResponse);

			var strBody = await objResponse.Content.ReadAsStringAsync();

			var objJson = JsonSerializer.Deserialize<Dictionary<string, object>>(strBody);
			if (objJson == null)
			{
				throw new InvalidOperationException("Failed to deserialize revision list");
			}
			
			var lstRevisions = new List<RunnersPointRevision>();
			if (objJson.TryGetValue("items", out var value))
			{
				foreach (var objItemObj in (IEnumerable)value)
					lstRevisions.Add(ParseRevision((Dictionary<string, object>)objItemObj));
			}
			return lstRevisions;
		}

		/// <summary>
		/// GET /documents/{documentId}/revisions. Immutable revision metadata, newest first.
		/// </summary>
		public Task<List<RunnersPointRevision>> ListRevisionsAsync(string strDocumentId)
		{
			return FetchRevisionsAsync("/documents/" + strDocumentId + "/revisions");
		}

		/// <summary>
		/// GET /shared/documents/{documentId}/revisions. Same shape, scoped to whatever the active share
		/// grant permits.
		/// </summary>
		public Task<List<RunnersPointRevision>> ListSharedRevisionsAsync(string strDocumentId)
		{
			return FetchRevisionsAsync("/shared/documents/" + strDocumentId + "/revisions");
		}

		private async Task PurgeAsync(string strPath, string strIfMatch)
		{
			var strIdempotencyKey = NewIdempotencyKey();
			var objResponse = await SendWithRetryAsync(async () =>
			{
				var objRequest = await CreateRequestAsync(HttpMethod.Delete, strPath);
				objRequest.Headers.Add("Idempotency-Key", strIdempotencyKey);
				objRequest.Headers.TryAddWithoutValidation("If-Match", strIfMatch);
				return objRequest;
			});
			await ThrowIfProblemAsync(objResponse);
		}

		/// <summary>
		/// DELETE /documents/{documentId}/purge. Irreversibly deletes an archived document, its
		/// revisions, and their storage objects. Requires the document to have been archived for at
		/// least 7 days (`purge_not_eligible` otherwise) and requires authentication no older than 15
		/// minutes (`recent_authentication_required`) - a stale login may need to be refreshed/re-entered
		/// even though the token itself is still otherwise valid.
		/// </summary>
		public Task PurgeDocumentAsync(string strDocumentId, string strIfMatch)
		{
			return PurgeAsync("/documents/" + strDocumentId + "/purge", strIfMatch);
		}

		/// <summary>
		/// DELETE /shared/documents/{documentId}/purge. Same operation as PurgeDocumentAsync, for a
		/// grantee holding an active `purge` grant instead of the owner - unlike archive/unarchive, purge
		/// is not owner-only. Same 7-day-archived and 15-minute-recent-auth preconditions.
		/// </summary>
		public Task PurgeSharedDocumentAsync(string strDocumentId, string strIfMatch)
		{
			return PurgeAsync("/shared/documents/" + strDocumentId + "/purge", strIfMatch);
		}

		/// <summary>
		/// DELETE /documents/{documentId}/revisions/{revisionId}. Irreversibly deletes a single revision
		/// and its storage object - no archived-first precondition or cooldown, but still requires
		/// authentication no older than 15 minutes. If the purged revision was `currentRevision` and
		/// others remain, the document rolls back to the most recently created remaining revision; if it
		/// was the only revision, the document becomes `archived` with a null `currentRevision`. Returns
		/// 204 with no body - callers should re-fetch the document afterward to see the updated state.
		/// </summary>
		public Task PurgeRevisionAsync(string strDocumentId, string strRevisionId, string strIfMatch)
		{
			return PurgeAsync("/documents/" + strDocumentId + "/revisions/" + strRevisionId, strIfMatch);
		}

		/// <summary>
		/// DELETE /shared/documents/{documentId}/revisions/{revisionId}. Same operation as
		/// PurgeRevisionAsync, for a grantee holding an active `purge` grant. No archived-first
		/// precondition; same 15-minute recent-auth requirement.
		/// </summary>
		public Task PurgeSharedRevisionAsync(string strDocumentId, string strRevisionId, string strIfMatch)
		{
			return PurgeAsync("/shared/documents/" + strDocumentId + "/revisions/" + strRevisionId, strIfMatch);
		}

#if DEBUG
		/// <summary>
		/// Debug-build-only diagnostic: dumps raw request/response detail for a document lookup that the
		/// normal typed methods discard (the exact ETag header text, full response headers, raw metadata
		/// JSON). Exists because bugs like the unquoted-ETag one are invisible through the typed API and
		/// only show up by looking at the wire format directly.
		/// </summary>
		public async Task<string> GetDebugDumpAsync(string strDocumentId)
		{
			var objDump = new StringBuilder();
			objDump.AppendLine("Base URL: " + _strBaseUrl);
			objDump.AppendLine("Client: " + ClientName + " " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version);

			var objRequest = await CreateRequestAsync(HttpMethod.Get, "/documents/" + strDocumentId);
			var objResponse = await _objHttpClient.SendAsync(objRequest);

			objDump.AppendLine();
			objDump.AppendLine("GET /documents/" + strDocumentId);
			objDump.AppendLine("Status: " + (int)objResponse.StatusCode + " " + objResponse.ReasonPhrase);
			objDump.AppendLine("Response headers:");
			foreach (var objHeader in objResponse.Headers)
				objDump.AppendLine("  " + objHeader.Key + ": " + string.Join(", ", objHeader.Value));
			objDump.AppendLine("Headers.ETag (typed): " + (objResponse.Headers.ETag?.Tag ?? "(null - see ExtractRawETag)"));
			objDump.AppendLine("ExtractRawETag(): " + (ExtractRawETag(objResponse) ?? "(null)"));

			var strBody = await objResponse.Content.ReadAsStringAsync();
			objDump.AppendLine("Body: " + strBody);

			return objDump.ToString();
		}
#endif
	}
}
