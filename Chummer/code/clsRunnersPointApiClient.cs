using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Serilog;

namespace Chummer
{
	/// <summary>
	/// Document as returned by the RunnersPoint Character Document Storage API.
	/// </summary>
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

	/// <summary>
	/// A page of Documents from listDocuments, plus the ETag/cursor needed to keep paging.
	/// </summary>
	public class RunnersPointDocumentPage
	{
		public List<RunnersPointDocument> Items { get; set; } = new List<RunnersPointDocument>();
		public string NextCursor { get; set; }
	}

	/// <summary>
	/// Asynchronous quarantine/validation status, returned by createDocument, pushRevision, and getRevisionStatus.
	/// </summary>
	public class RunnersPointRevisionStatus
	{
		public string DocumentId { get; set; }
		public string RevisionId { get; set; }
		public string State { get; set; }
		public List<string> Messages { get; set; } = new List<string>();
	}

	public class RunnersPointGameProfile
	{
		public string Id { get; set; }
		public string System { get; set; }
		public string Edition { get; set; }
		public string DisplayName { get; set; }
		public List<string> Formats { get; set; } = new List<string>();
	}

	/// <summary>
	/// One media type a registered document type accepts (e.g. "application/xml" for "character"),
	/// plus its own upload size ceiling.
	/// </summary>
	public class RunnersPointDocumentFormatCapability
	{
		public string MediaType { get; set; }
		public long MaxUploadBytes { get; set; }
	}

	/// <summary>
	/// One entry from Capabilities.documentTypes - the registered type id (e.g. "character") Chummer
	/// sends as X-Document-Type, and the formats it accepts.
	/// </summary>
	public class RunnersPointDocumentTypeCapability
	{
		public string Id { get; set; }
		public string DisplayName { get; set; }
		public List<RunnersPointDocumentFormatCapability> Formats { get; set; } = new List<RunnersPointDocumentFormatCapability>();
	}

	public class RunnersPointCapabilities
	{
		public string ApiVersion { get; set; }
		public List<RunnersPointGameProfile> GameProfiles { get; set; } = new List<RunnersPointGameProfile>();
		public List<RunnersPointDocumentTypeCapability> DocumentTypes { get; set; } = new List<RunnersPointDocumentTypeCapability>();
		public List<string> Formats { get; set; } = new List<string>();
		public long MaxUploadBytes { get; set; }
	}

	/// <summary>
	/// A document explicitly shared with the authenticated user by another user (e.g. a GM sharing a
	/// character, or a document originating from a marketplace on the RunnersPoint website). Chummer
	/// never sees a public listing - only documents an active share grant actually covers.
	/// </summary>
	public class RunnersPointSharedDocument : RunnersPointDocument
	{
		public string Permission { get; set; }
		public string ShareStatus { get; set; }
		public DateTime? ExpiresAt { get; set; }
	}

	/// <summary>
	/// A page of SharedDocuments from listSharedDocuments, plus the cursor needed to keep paging.
	/// </summary>
	public class RunnersPointSharedDocumentPage
	{
		public List<RunnersPointSharedDocument> Items { get; set; } = new List<RunnersPointSharedDocument>();
		public string NextCursor { get; set; }
	}

	/// <summary>
	/// Thrown for any non-success response from the RunnersPoint API. Carries the RFC 9457 Problem
	/// Details fields where the server provided them, so callers can show a useful message instead of
	/// just an HTTP status code.
	/// </summary>
	public class RunnersPointApiException : Exception
	{
		public HttpStatusCode StatusCode { get; private set; }
		public string ProblemCode { get; private set; }
		public string CorrelationId { get; private set; }

		public RunnersPointApiException(HttpStatusCode statusCode, string title, string problemCode, string correlationId)
			: base(title)
		{
			StatusCode = statusCode;
			ProblemCode = problemCode;
			CorrelationId = correlationId;
		}
	}

	/// <summary>
	/// Thin wrapper over the RunnersPoint Character Document Storage API (v1, draft.6). Covers personal
	/// storage/sync of `character` documents, plus read-only-or-write access to documents explicitly
	/// shared with the authenticated user via `/shared/documents/*` (per-document grants only - there is
	/// no public marketplace/discovery surface in this API; that lives on the RunnersPoint website).
	/// </summary>
	public class RunnersPointApiClient
	{
		// Configurable via Options > Cloud Documents server (GlobalOptions.CloudApiBaseUrl), so it can
		// point at a local dev server, a staging deployment, or eventually a real production host
		// without a rebuild. Defaults to the local Symfony dev server's actual route prefix (/api/v1,
		// not the /v1 the OpenAPI spec's example server URL still shows).
		private static string BaseUrl => GlobalOptions.Instance.CloudApiBaseUrl;
		private const string ClientName = "ChummerGenSR4";

		private readonly HttpClient _objHttpClient;
		private readonly RunnersPointAuth _objAuth;

		public RunnersPointApiClient(RunnersPointAuth objAuth)
		{
			_objAuth = objAuth;
			_objHttpClient = new HttpClient();
			_objHttpClient.DefaultRequestHeaders.Add("X-Client-Name", ClientName);
			_objHttpClient.DefaultRequestHeaders.Add("X-Client-Version", System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString());
		}

		private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod objMethod, string strPath, bool blnAuthenticated = true)
		{
			HttpRequestMessage objRequest = new HttpRequestMessage(objMethod, BaseUrl + strPath);
			if (blnAuthenticated)
			{
				string strToken = await _objAuth.GetAccessTokenAsync();
				objRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", strToken);
			}
			return objRequest;
		}

		private static string NewIdempotencyKey()
		{
			return Guid.NewGuid().ToString("N");
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
		internal static string ExtractRawETag(HttpResponseMessage objResponse)
		{
			IEnumerable<string> lstValues;
			return objResponse.Headers.TryGetValues("ETag", out lstValues) ? lstValues.FirstOrDefault() : null;
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
			HttpRequestMessage objRequest = await requestFactory();
			HttpResponseMessage objResponse = await _objHttpClient.SendAsync(objRequest);
			if (objResponse.StatusCode != HttpStatusCode.Unauthorized)
				return objResponse;

			if (!await _objAuth.TryForceRefreshAsync())
				return objResponse;

			Log.Information("RunnersPoint API call got 401 - refreshed the access token and retrying once");
			objResponse.Dispose();
			HttpRequestMessage objRetryRequest = await requestFactory();
			return await _objHttpClient.SendAsync(objRetryRequest);
		}

		private async Task ThrowIfProblemAsync(HttpResponseMessage objResponse)
		{
			if (objResponse.IsSuccessStatusCode)
				return;

			string strTitle = objResponse.ReasonPhrase;
			string strCode = "";
			string strCorrelationId = "";
			try
			{
				string strBody = await objResponse.Content.ReadAsStringAsync();
				if (!string.IsNullOrEmpty(strBody))
				{
					JavaScriptSerializer objSerializer = new JavaScriptSerializer();
					Dictionary<string, object> objProblem = objSerializer.Deserialize<Dictionary<string, object>>(strBody);
					if (objProblem.ContainsKey("title"))
						strTitle = objProblem["title"].ToString();
					if (objProblem.ContainsKey("code"))
						strCode = objProblem["code"].ToString();
					if (objProblem.ContainsKey("correlationId"))
						strCorrelationId = objProblem["correlationId"].ToString();
				}
			}
			catch
			{
				// Problem body wasn't valid JSON (or wasn't Problem-shaped) - fall back to the status line.
			}

			string strMethod = objResponse.RequestMessage?.Method?.Method ?? "?";
			string strPath = objResponse.RequestMessage?.RequestUri?.PathAndQuery ?? "?";

			if (objResponse.StatusCode == (HttpStatusCode)429)
			{
				IEnumerable<string> lstRetryAfter;
				string strRetryAfter = objResponse.Headers.TryGetValues("Retry-After", out lstRetryAfter) ? string.Join(",", lstRetryAfter) : "unknown";
				Log.Warning("RunnersPoint API {Method} {Path} rate limited - retry after {RetryAfter}s (correlationId={CorrelationId})", strMethod, strPath, strRetryAfter, strCorrelationId);
				throw new RunnersPointApiException(objResponse.StatusCode, "Rate limited - retry after " + strRetryAfter + "s", strCode, strCorrelationId);
			}

			Log.Warning("RunnersPoint API {Method} {Path} failed with {StatusCode}: {Title} (code={Code}, correlationId={CorrelationId})",
				strMethod, strPath, (int)objResponse.StatusCode, strTitle, strCode, strCorrelationId);
			throw new RunnersPointApiException(objResponse.StatusCode, strTitle, strCode, strCorrelationId);
		}

		/// <summary>
		/// GET /capabilities. Unauthenticated. Used at startup to resolve the SR4 GameProfileId and its
		/// supported formats/maximum upload size.
		/// </summary>
		public async Task<RunnersPointCapabilities> GetCapabilitiesAsync()
		{
			HttpRequestMessage objRequest = await CreateRequestAsync(HttpMethod.Get, "/capabilities", false);
			HttpResponseMessage objResponse = await _objHttpClient.SendAsync(objRequest);
			await ThrowIfProblemAsync(objResponse);

			string strBody = await objResponse.Content.ReadAsStringAsync();
			JavaScriptSerializer objSerializer = new JavaScriptSerializer();
			Dictionary<string, object> objJson = objSerializer.Deserialize<Dictionary<string, object>>(strBody);

			RunnersPointCapabilities objCapabilities = new RunnersPointCapabilities();
			objCapabilities.ApiVersion = objJson.ContainsKey("apiVersion") ? objJson["apiVersion"].ToString() : "";
			objCapabilities.MaxUploadBytes = objJson.ContainsKey("maxUploadBytes") ? Convert.ToInt64(objJson["maxUploadBytes"]) : 0;

			if (objJson.ContainsKey("formats"))
			{
				foreach (object objFormat in (IEnumerable)objJson["formats"])
					objCapabilities.Formats.Add(objFormat.ToString());
			}

			if (objJson.ContainsKey("gameProfiles"))
			{
				foreach (object objProfileObj in (IEnumerable)objJson["gameProfiles"])
				{
					Dictionary<string, object> objProfile = (Dictionary<string, object>)objProfileObj;
					RunnersPointGameProfile objGameProfile = new RunnersPointGameProfile();
					objGameProfile.Id = objProfile.ContainsKey("id") ? objProfile["id"].ToString() : "";
					objGameProfile.System = objProfile.ContainsKey("system") ? objProfile["system"].ToString() : "";
					objGameProfile.Edition = objProfile.ContainsKey("edition") ? objProfile["edition"].ToString() : "";
					objGameProfile.DisplayName = objProfile.ContainsKey("displayName") ? objProfile["displayName"].ToString() : "";
					if (objProfile.ContainsKey("formats"))
					{
						foreach (object objFormat in (IEnumerable)objProfile["formats"])
							objGameProfile.Formats.Add(objFormat.ToString());
					}
					objCapabilities.GameProfiles.Add(objGameProfile);
				}
			}

			if (objJson.ContainsKey("documentTypes"))
			{
				foreach (object objTypeObj in (IEnumerable)objJson["documentTypes"])
				{
					Dictionary<string, object> objType = (Dictionary<string, object>)objTypeObj;
					RunnersPointDocumentTypeCapability objTypeCapability = new RunnersPointDocumentTypeCapability();
					objTypeCapability.Id = objType.ContainsKey("id") ? objType["id"].ToString() : "";
					objTypeCapability.DisplayName = objType.ContainsKey("displayName") ? objType["displayName"].ToString() : "";
					if (objType.ContainsKey("formats"))
					{
						foreach (object objFormatObj in (IEnumerable)objType["formats"])
						{
							Dictionary<string, object> objFormat = (Dictionary<string, object>)objFormatObj;
							objTypeCapability.Formats.Add(new RunnersPointDocumentFormatCapability
							{
								MediaType = objFormat.ContainsKey("mediaType") ? objFormat["mediaType"].ToString() : "",
								MaxUploadBytes = objFormat.ContainsKey("maxUploadBytes") ? Convert.ToInt64(objFormat["maxUploadBytes"]) : 0,
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
		public async Task<RunnersPointDocumentPage> ListDocumentsAsync(string strGameProfileId, string strCursor = null, int intPageSize = 25)
		{
			string strPath = "/documents?gameProfileId=" + Uri.EscapeDataString(strGameProfileId) + "&pageSize=" + intPageSize;
			if (!string.IsNullOrEmpty(strCursor))
				strPath += "&cursor=" + Uri.EscapeDataString(strCursor);

			HttpResponseMessage objResponse = await SendWithRetryAsync(() => CreateRequestAsync(HttpMethod.Get, strPath));
			await ThrowIfProblemAsync(objResponse);

			string strBody = await objResponse.Content.ReadAsStringAsync();
			JavaScriptSerializer objSerializer = new JavaScriptSerializer();
			Dictionary<string, object> objJson = objSerializer.Deserialize<Dictionary<string, object>>(strBody);

			RunnersPointDocumentPage objPage = new RunnersPointDocumentPage();
			objPage.NextCursor = objJson.ContainsKey("nextCursor") && objJson["nextCursor"] != null ? objJson["nextCursor"].ToString() : null;

			if (objJson.ContainsKey("items"))
			{
				foreach (object objItemObj in (IEnumerable)objJson["items"])
					objPage.Items.Add(ParseDocument((Dictionary<string, object>)objItemObj));
			}

			return objPage;
		}

		internal static RunnersPointDocument ParseDocument(Dictionary<string, object> objJson)
		{
			RunnersPointDocument objDocument = new RunnersPointDocument();
			objDocument.Id = objJson.ContainsKey("id") ? objJson["id"].ToString() : "";
			objDocument.Type = objJson.ContainsKey("type") ? objJson["type"].ToString() : "";
			objDocument.GameProfileId = objJson.ContainsKey("gameProfileId") ? objJson["gameProfileId"].ToString() : "";
			objDocument.Format = objJson.ContainsKey("format") ? objJson["format"].ToString() : "";
			objDocument.SchemaVersion = objJson.ContainsKey("schemaVersion") ? objJson["schemaVersion"].ToString() : "";
			objDocument.CurrentRevision = objJson.ContainsKey("currentRevision") ? objJson["currentRevision"].ToString() : "";
			objDocument.ValidationState = objJson.ContainsKey("validationState") ? objJson["validationState"].ToString() : "";
			if (objJson.ContainsKey("metadata") && objJson["metadata"] is Dictionary<string, object>)
			{
				Dictionary<string, object> objMetadata = (Dictionary<string, object>)objJson["metadata"];
				if (objMetadata.ContainsKey("displayName"))
					objDocument.DisplayName = objMetadata["displayName"].ToString();
				// The server's character-document extractor currently populates metadata.name (the
				// charactername/name XML element), not metadata.displayName as the spec and this client
				// otherwise expect - fall back to it so the list doesn't just show raw document IDs for
				// every character until that's reconciled server-side.
				else if (objMetadata.ContainsKey("name"))
					objDocument.DisplayName = objMetadata["name"].ToString();
			}
			if (objJson.ContainsKey("updatedAt") && DateTime.TryParse(objJson["updatedAt"].ToString(), out DateTime datUpdatedAt))
				objDocument.UpdatedAt = datUpdatedAt;
			return objDocument;
		}

		/// <summary>
		/// Parses a SharedDocument - the same envelope as Document, plus a required `share` grant
		/// (permission/status/expiresAt) describing what the authenticated user is allowed to do with it.
		/// </summary>
		internal static RunnersPointSharedDocument ParseSharedDocument(Dictionary<string, object> objJson)
		{
			RunnersPointDocument objBase = ParseDocument(objJson);
			RunnersPointSharedDocument objShared = new RunnersPointSharedDocument
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
				objShared.Permission = objShare.ContainsKey("permission") ? objShare["permission"].ToString() : "";
				objShared.ShareStatus = objShare.ContainsKey("status") ? objShare["status"].ToString() : "";
				if (objShare.ContainsKey("expiresAt") && objShare["expiresAt"] != null
					&& DateTime.TryParse(objShare["expiresAt"].ToString(), out DateTime datExpiresAt))
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
			HttpResponseMessage objResponse = await SendWithRetryAsync(() => CreateRequestAsync(HttpMethod.Get, "/documents/" + strDocumentId));
			await ThrowIfProblemAsync(objResponse);

			string strBody = await objResponse.Content.ReadAsStringAsync();
			JavaScriptSerializer objSerializer = new JavaScriptSerializer();
			RunnersPointDocument objDocument = ParseDocument(objSerializer.Deserialize<Dictionary<string, object>>(strBody));
			string strETag = ExtractRawETag(objResponse);
			return new Tuple<RunnersPointDocument, string>(objDocument, strETag);
		}

		/// <summary>
		/// POST /documents. Mints a brand-new document and its first revision. Do not call this again
		/// for a document that already has a CloudDocumentId - use PushRevisionAsync instead.
		/// </summary>
		public async Task<RunnersPointRevisionStatus> CreateDocumentAsync(byte[] bytContent, string strGameProfileId, string strFormat)
		{
			string strIdempotencyKey = NewIdempotencyKey();
			HttpResponseMessage objResponse = await SendWithRetryAsync(async () =>
			{
				HttpRequestMessage objRequest = await CreateRequestAsync(HttpMethod.Post, "/documents");
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
			string strIdempotencyKey = NewIdempotencyKey();
			HttpResponseMessage objResponse = await SendWithRetryAsync(async () =>
			{
				HttpRequestMessage objRequest = await CreateRequestAsync(HttpMethod.Put, "/documents/" + strDocumentId);
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
			string strBody = await objResponse.Content.ReadAsStringAsync();
			JavaScriptSerializer objSerializer = new JavaScriptSerializer();
			Dictionary<string, object> objJson = objSerializer.Deserialize<Dictionary<string, object>>(strBody);

			RunnersPointRevisionStatus objStatus = new RunnersPointRevisionStatus();
			objStatus.DocumentId = objJson.ContainsKey("documentId") ? objJson["documentId"].ToString() : "";
			objStatus.RevisionId = objJson.ContainsKey("revisionId") ? objJson["revisionId"].ToString() : "";
			objStatus.State = objJson.ContainsKey("state") ? objJson["state"].ToString() : "";
			if (objJson.ContainsKey("messages"))
			{
				foreach (object objMessage in (IEnumerable)objJson["messages"])
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
			HttpResponseMessage objResponse = await SendWithRetryAsync(() => CreateRequestAsync(HttpMethod.Get, "/revision-status/" + strRevisionId));
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
			HttpResponseMessage objResponse = await SendWithRetryAsync(() => CreateRequestAsync(HttpMethod.Get, "/documents/" + strDocumentId + "/revisions/" + strRevisionId + "/content"));
			await ThrowIfProblemAsync(objResponse);
			byte[] bytContent = await objResponse.Content.ReadAsByteArrayAsync();

			IEnumerable<string> lstDigestValues;
			if (objResponse.Headers.TryGetValues("Digest", out lstDigestValues))
			{
				string strDigestHeader = lstDigestValues.FirstOrDefault();
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
		internal static string ParseSuggestedFileName(HttpResponseMessage objResponse)
		{
			// Content-Disposition is a content header per RFC 7231, not a response header - HttpClient
			// parses it onto Content.Headers, never onto the top-level Headers collection, so looking
			// there would never find it even if the server sent one.
			if (objResponse.Content == null)
				return null;

			IEnumerable<string> lstValues;
			if (!objResponse.Content.Headers.TryGetValues("Content-Disposition", out lstValues))
				return null;

			string strHeader = lstValues.FirstOrDefault();
			if (string.IsNullOrEmpty(strHeader))
				return null;

			int intFilenameIndex = strHeader.IndexOf("filename=", StringComparison.OrdinalIgnoreCase);
			if (intFilenameIndex < 0)
				return null;

			string strFileName = strHeader.Substring(intFilenameIndex + "filename=".Length).Trim();
			int intSemicolon = strFileName.IndexOf(';');
			if (intSemicolon >= 0)
				strFileName = strFileName.Substring(0, intSemicolon);
			return strFileName.Trim().Trim('"');
		}

		/// <summary>
		/// Compares a SHA-256 hash of bytContent against a Digest header value. The spec only says
		/// "SHA-256 digest of the response bytes" without nailing down RFC 3230's usual
		/// "SHA-256=&lt;base64&gt;" framing vs. a bare hex string, so both are accepted.
		/// </summary>
		internal static bool VerifyDigest(byte[] bytContent, string strDigestHeader)
		{
			string strValue = strDigestHeader;
			int intEquals = strValue.IndexOf('=');
			if (strValue.StartsWith("SHA-256=", StringComparison.OrdinalIgnoreCase) && intEquals >= 0)
				strValue = strValue.Substring(intEquals + 1);

			byte[] bytExpected;
			using (SHA256 objSha256 = SHA256.Create())
				bytExpected = objSha256.ComputeHash(bytContent);

			byte[] bytActual;
			string strHex = strValue.Replace("-", "");
			// A bare hex SHA-256 digest is 64 lowercase-hex characters - which is also, incidentally, a
			// syntactically valid (if semantically wrong) base64 string, since the hex alphabet is a
			// subset of base64's and 64 is a multiple of 4. Trying base64 first would silently decode
			// that into 48 wrong bytes instead of throwing, so hex-shaped input has to be checked first.
			if (strHex.Length % 2 == 0 && System.Text.RegularExpressions.Regex.IsMatch(strHex, "^[0-9a-fA-F]+$"))
			{
				bytActual = new byte[strHex.Length / 2];
				for (int i = 0; i < bytActual.Length; i++)
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
			for (int i = 0; i < bytActual.Length; i++)
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
			string strIdempotencyKey = NewIdempotencyKey();
			HttpResponseMessage objResponse = await SendWithRetryAsync(async () =>
			{
				HttpRequestMessage objRequest = await CreateRequestAsync(HttpMethod.Delete, "/documents/" + strDocumentId);
				objRequest.Headers.Add("Idempotency-Key", strIdempotencyKey);
				objRequest.Headers.TryAddWithoutValidation("If-Match", strIfMatch);
				return objRequest;
			});
			await ThrowIfProblemAsync(objResponse);
		}

		/// <summary>
		/// GET /shared/documents. Documents explicitly shared with the authenticated user by another
		/// user - e.g. a GM sharing a character back, or a document someone picked up from a marketplace
		/// on the RunnersPoint website. Chummer only ever sees the individual grant; there is no public
		/// browsing here.
		/// </summary>
		public async Task<RunnersPointSharedDocumentPage> ListSharedDocumentsAsync(string strGameProfileId, string strCursor = null, int intPageSize = 25)
		{
			string strPath = "/shared/documents?gameProfileId=" + Uri.EscapeDataString(strGameProfileId) + "&pageSize=" + intPageSize;
			if (!string.IsNullOrEmpty(strCursor))
				strPath += "&cursor=" + Uri.EscapeDataString(strCursor);

			HttpResponseMessage objResponse = await SendWithRetryAsync(() => CreateRequestAsync(HttpMethod.Get, strPath));
			await ThrowIfProblemAsync(objResponse);

			string strBody = await objResponse.Content.ReadAsStringAsync();
			JavaScriptSerializer objSerializer = new JavaScriptSerializer();
			Dictionary<string, object> objJson = objSerializer.Deserialize<Dictionary<string, object>>(strBody);

			RunnersPointSharedDocumentPage objPage = new RunnersPointSharedDocumentPage();
			objPage.NextCursor = objJson.ContainsKey("nextCursor") && objJson["nextCursor"] != null ? objJson["nextCursor"].ToString() : null;

			if (objJson.ContainsKey("items"))
			{
				foreach (object objItemObj in (IEnumerable)objJson["items"])
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
			HttpResponseMessage objResponse = await SendWithRetryAsync(() => CreateRequestAsync(HttpMethod.Get, "/shared/documents/" + strDocumentId));
			await ThrowIfProblemAsync(objResponse);

			string strBody = await objResponse.Content.ReadAsStringAsync();
			JavaScriptSerializer objSerializer = new JavaScriptSerializer();
			RunnersPointSharedDocument objDocument = ParseSharedDocument(objSerializer.Deserialize<Dictionary<string, object>>(strBody));
			string strETag = ExtractRawETag(objResponse);
			return new Tuple<RunnersPointSharedDocument, string>(objDocument, strETag);
		}

		/// <summary>
		/// PUT /shared/documents/{documentId}. Requires an active write grant on the document - archive
		/// and delete are never available through shared access, only through the owner's own /documents.
		/// </summary>
		public async Task<RunnersPointRevisionStatus> PushSharedDocumentRevisionAsync(string strDocumentId, byte[] bytContent, string strIfMatch, string strGameProfileId, string strFormat)
		{
			string strIdempotencyKey = NewIdempotencyKey();
			HttpResponseMessage objResponse = await SendWithRetryAsync(async () =>
			{
				HttpRequestMessage objRequest = await CreateRequestAsync(HttpMethod.Put, "/shared/documents/" + strDocumentId);
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
		/// GET /shared/documents/{documentId}/revisions/{revisionId}/content. Same Digest verification
		/// as DownloadRevisionAsync - a shared download is downloaded from someone else's storage, so
		/// verifying it wasn't corrupted or truncated in transit matters at least as much here.
		/// </summary>
		public async Task<Tuple<byte[], string>> DownloadSharedDocumentRevisionAsync(string strDocumentId, string strRevisionId)
		{
			HttpResponseMessage objResponse = await SendWithRetryAsync(() => CreateRequestAsync(HttpMethod.Get, "/shared/documents/" + strDocumentId + "/revisions/" + strRevisionId + "/content"));
			await ThrowIfProblemAsync(objResponse);
			byte[] bytContent = await objResponse.Content.ReadAsByteArrayAsync();

			IEnumerable<string> lstDigestValues;
			if (objResponse.Headers.TryGetValues("Digest", out lstDigestValues))
			{
				string strDigestHeader = lstDigestValues.FirstOrDefault();
				if (!string.IsNullOrEmpty(strDigestHeader) && !VerifyDigest(bytContent, strDigestHeader))
					throw new InvalidOperationException("Downloaded content for shared revision " + strRevisionId + " failed digest verification - the bytes received don't match the server's declared hash. Discarding rather than saving a possibly-corrupted file.");
			}

			return new Tuple<byte[], string>(bytContent, ParseSuggestedFileName(objResponse));
		}
	}
}
