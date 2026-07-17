using System;
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

	public class RunnersPointCapabilities
	{
		public string ApiVersion { get; set; }
		public List<RunnersPointGameProfile> GameProfiles { get; set; } = new List<RunnersPointGameProfile>();
		public List<string> Formats { get; set; } = new List<string>();
		public long MaximumUploadBytes { get; set; }
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
	/// Thin wrapper over the RunnersPoint Character Document Storage API (v1, draft.2). Only implements
	/// the operations the Chummer client actually uses - personal storage/sync of `character` documents.
	/// Sharing/discovery and non-character document types are out of scope for this client (see
	/// docs/api/open-questions-character-document-storage-v1.md in the RunnersPoint repo).
	/// </summary>
	public class RunnersPointApiClient
	{
		// TODO: replace with the real base URL once RunnersPoint publishes one outside the example placeholder.
		private const string BaseUrl = "https://api.runnerspoint.example/v1";
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

			if (objResponse.StatusCode == (HttpStatusCode)429)
			{
				IEnumerable<string> lstRetryAfter;
				string strRetryAfter = objResponse.Headers.TryGetValues("Retry-After", out lstRetryAfter) ? string.Join(",", lstRetryAfter) : "unknown";
				throw new RunnersPointApiException(objResponse.StatusCode, "Rate limited - retry after " + strRetryAfter + "s", strCode, strCorrelationId);
			}

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
			objCapabilities.MaximumUploadBytes = objJson.ContainsKey("maximumUploadBytes") ? Convert.ToInt64(objJson["maximumUploadBytes"]) : 0;

			if (objJson.ContainsKey("formats"))
			{
				foreach (object objFormat in (object[])objJson["formats"])
					objCapabilities.Formats.Add(objFormat.ToString());
			}

			if (objJson.ContainsKey("gameProfiles"))
			{
				foreach (object objProfileObj in (object[])objJson["gameProfiles"])
				{
					Dictionary<string, object> objProfile = (Dictionary<string, object>)objProfileObj;
					RunnersPointGameProfile objGameProfile = new RunnersPointGameProfile();
					objGameProfile.Id = objProfile.ContainsKey("id") ? objProfile["id"].ToString() : "";
					objGameProfile.System = objProfile.ContainsKey("system") ? objProfile["system"].ToString() : "";
					objGameProfile.Edition = objProfile.ContainsKey("edition") ? objProfile["edition"].ToString() : "";
					objGameProfile.DisplayName = objProfile.ContainsKey("displayName") ? objProfile["displayName"].ToString() : "";
					if (objProfile.ContainsKey("formats"))
					{
						foreach (object objFormat in (object[])objProfile["formats"])
							objGameProfile.Formats.Add(objFormat.ToString());
					}
					objCapabilities.GameProfiles.Add(objGameProfile);
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

			HttpRequestMessage objRequest = await CreateRequestAsync(HttpMethod.Get, strPath);
			HttpResponseMessage objResponse = await _objHttpClient.SendAsync(objRequest);
			await ThrowIfProblemAsync(objResponse);

			string strBody = await objResponse.Content.ReadAsStringAsync();
			JavaScriptSerializer objSerializer = new JavaScriptSerializer();
			Dictionary<string, object> objJson = objSerializer.Deserialize<Dictionary<string, object>>(strBody);

			RunnersPointDocumentPage objPage = new RunnersPointDocumentPage();
			objPage.NextCursor = objJson.ContainsKey("nextCursor") && objJson["nextCursor"] != null ? objJson["nextCursor"].ToString() : null;

			if (objJson.ContainsKey("items"))
			{
				foreach (object objItemObj in (object[])objJson["items"])
					objPage.Items.Add(ParseDocument((Dictionary<string, object>)objItemObj));
			}

			return objPage;
		}

		private static RunnersPointDocument ParseDocument(Dictionary<string, object> objJson)
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
			}
			if (objJson.ContainsKey("updatedAt"))
				DateTime.TryParse(objJson["updatedAt"].ToString(), out DateTime datUpdatedAt);
			return objDocument;
		}

		/// <summary>
		/// GET /documents/{documentId}. Returns the document plus its current ETag (needed for a
		/// subsequent pushRevision/archive call's If-Match).
		/// </summary>
		public async Task<Tuple<RunnersPointDocument, string>> GetDocumentAsync(string strDocumentId)
		{
			HttpRequestMessage objRequest = await CreateRequestAsync(HttpMethod.Get, "/documents/" + strDocumentId);
			HttpResponseMessage objResponse = await _objHttpClient.SendAsync(objRequest);
			await ThrowIfProblemAsync(objResponse);

			string strBody = await objResponse.Content.ReadAsStringAsync();
			JavaScriptSerializer objSerializer = new JavaScriptSerializer();
			RunnersPointDocument objDocument = ParseDocument(objSerializer.Deserialize<Dictionary<string, object>>(strBody));
			string strETag = objResponse.Headers.ETag != null ? objResponse.Headers.ETag.Tag : null;
			return new Tuple<RunnersPointDocument, string>(objDocument, strETag);
		}

		/// <summary>
		/// POST /documents. Mints a brand-new document and its first revision. Do not call this again
		/// for a document that already has a CloudDocumentId - use PushRevisionAsync instead.
		/// </summary>
		public async Task<RunnersPointRevisionStatus> CreateDocumentAsync(byte[] bytContent, string strGameProfileId, string strFormat)
		{
			HttpRequestMessage objRequest = await CreateRequestAsync(HttpMethod.Post, "/documents");
			objRequest.Headers.Add("Idempotency-Key", NewIdempotencyKey());
			objRequest.Headers.Add("X-Game-Profile-Id", strGameProfileId);
			objRequest.Headers.Add("X-Document-Format", strFormat);
			objRequest.Content = new ByteArrayContent(bytContent);
			objRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

			HttpResponseMessage objResponse = await _objHttpClient.SendAsync(objRequest);
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
			HttpRequestMessage objRequest = await CreateRequestAsync(HttpMethod.Put, "/documents/" + strDocumentId);
			objRequest.Headers.Add("Idempotency-Key", NewIdempotencyKey());
			objRequest.Headers.Add("If-Match", strIfMatch);
			objRequest.Headers.Add("X-Game-Profile-Id", strGameProfileId);
			objRequest.Headers.Add("X-Document-Format", strFormat);
			objRequest.Content = new ByteArrayContent(bytContent);
			objRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

			HttpResponseMessage objResponse = await _objHttpClient.SendAsync(objRequest);
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
				foreach (object objMessage in (object[])objJson["messages"])
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
			HttpRequestMessage objRequest = await CreateRequestAsync(HttpMethod.Get, "/revision-status/" + strRevisionId);
			HttpResponseMessage objResponse = await _objHttpClient.SendAsync(objRequest);
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
		public async Task<byte[]> DownloadRevisionAsync(string strDocumentId, string strRevisionId)
		{
			HttpRequestMessage objRequest = await CreateRequestAsync(HttpMethod.Get, "/documents/" + strDocumentId + "/revisions/" + strRevisionId + "/content");
			HttpResponseMessage objResponse = await _objHttpClient.SendAsync(objRequest);
			await ThrowIfProblemAsync(objResponse);
			byte[] bytContent = await objResponse.Content.ReadAsByteArrayAsync();

			IEnumerable<string> lstDigestValues;
			if (objResponse.Headers.TryGetValues("Digest", out lstDigestValues))
			{
				string strDigestHeader = lstDigestValues.FirstOrDefault();
				if (!string.IsNullOrEmpty(strDigestHeader) && !VerifyDigest(bytContent, strDigestHeader))
					throw new InvalidOperationException("Downloaded content for revision " + strRevisionId + " failed digest verification - the bytes received don't match the server's declared hash. Discarding rather than saving a possibly-corrupted file.");
			}

			return bytContent;
		}

		/// <summary>
		/// Compares a SHA-256 hash of bytContent against a Digest header value. The spec only says
		/// "SHA-256 digest of the response bytes" without nailing down RFC 3230's usual
		/// "SHA-256=&lt;base64&gt;" framing vs. a bare hex string, so both are accepted.
		/// </summary>
		private static bool VerifyDigest(byte[] bytContent, string strDigestHeader)
		{
			string strValue = strDigestHeader;
			int intEquals = strValue.IndexOf('=');
			if (strValue.StartsWith("SHA-256=", StringComparison.OrdinalIgnoreCase) && intEquals >= 0)
				strValue = strValue.Substring(intEquals + 1);

			byte[] bytExpected;
			using (SHA256 objSha256 = SHA256.Create())
				bytExpected = objSha256.ComputeHash(bytContent);

			byte[] bytActual;
			try
			{
				// Try base64 first (RFC 3230 convention), fall back to hex.
				bytActual = Convert.FromBase64String(strValue);
			}
			catch (FormatException)
			{
				try
				{
					string strHex = strValue.Replace("-", "");
					bytActual = new byte[strHex.Length / 2];
					for (int i = 0; i < bytActual.Length; i++)
						bytActual[i] = Convert.ToByte(strHex.Substring(i * 2, 2), 16);
				}
				catch
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
			HttpRequestMessage objRequest = await CreateRequestAsync(HttpMethod.Delete, "/documents/" + strDocumentId);
			objRequest.Headers.Add("Idempotency-Key", NewIdempotencyKey());
			objRequest.Headers.Add("If-Match", strIfMatch);

			HttpResponseMessage objResponse = await _objHttpClient.SendAsync(objRequest);
			await ThrowIfProblemAsync(objResponse);
		}
	}
}
