using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using Xunit;

namespace Chummer.Tests
{
	public class RunnersPointApiClientTests
	{
		private static string Sha256Base64(byte[] bytContent)
		{
			using (SHA256 objSha256 = SHA256.Create())
				return Convert.ToBase64String(objSha256.ComputeHash(bytContent));
		}

		private static string Sha256Hex(byte[] bytContent)
		{
			using (SHA256 objSha256 = SHA256.Create())
				return BitConverter.ToString(objSha256.ComputeHash(bytContent)).Replace("-", "").ToLowerInvariant();
		}

		[Fact]
		public void VerifyDigest_AcceptsRfc3230Base64Prefixed()
		{
			byte[] bytContent = Encoding.UTF8.GetBytes("hello world");
			string strHeader = "SHA-256=" + Sha256Base64(bytContent);

			Assert.True(RunnersPointApiClient.VerifyDigest(bytContent, strHeader));
		}

		[Fact]
		public void VerifyDigest_AcceptsBareHex()
		{
			byte[] bytContent = Encoding.UTF8.GetBytes("hello world");
			string strHeader = Sha256Hex(bytContent);

			Assert.True(RunnersPointApiClient.VerifyDigest(bytContent, strHeader));
		}

		[Fact]
		public void VerifyDigest_RejectsMismatchedHash()
		{
			byte[] bytContent = Encoding.UTF8.GetBytes("hello world");
			byte[] bytOther = Encoding.UTF8.GetBytes("something else entirely");
			string strHeader = "SHA-256=" + Sha256Base64(bytOther);

			Assert.False(RunnersPointApiClient.VerifyDigest(bytContent, strHeader));
		}

		[Fact]
		public void VerifyDigest_FailsOpenOnUnrecognizedFormat()
		{
			// Documented behavior: a Digest header present but in a format we can't parse doesn't fail
			// the download over a format mismatch we can't interpret, but doesn't pretend to have
			// verified it either - VerifyDigest returns true rather than false in this case.
			byte[] bytContent = Encoding.UTF8.GetBytes("hello world");
			string strHeader = "not a real digest header !!";

			Assert.True(RunnersPointApiClient.VerifyDigest(bytContent, strHeader));
		}

		private static HttpResponseMessage ResponseWithContentDisposition(string strValue)
		{
			HttpResponseMessage objResponse = new HttpResponseMessage
			{
				Content = new ByteArrayContent(Array.Empty<byte>()),
			};
			if (strValue != null)
				objResponse.Content.Headers.TryAddWithoutValidation("Content-Disposition", strValue);
			return objResponse;
		}

		[Fact]
		public void ParseSuggestedFileName_ExtractsQuotedFilename()
		{
			HttpResponseMessage objResponse = ResponseWithContentDisposition("attachment; filename=\"my-character.chum\"");

			Assert.Equal("my-character.chum", RunnersPointApiClient.ParseSuggestedFileName(objResponse));
		}

		[Fact]
		public void ParseSuggestedFileName_ExtractsUnquotedFilename()
		{
			HttpResponseMessage objResponse = ResponseWithContentDisposition("attachment; filename=my-character.chum");

			Assert.Equal("my-character.chum", RunnersPointApiClient.ParseSuggestedFileName(objResponse));
		}

		[Fact]
		public void ParseSuggestedFileName_ReturnsNullWhenHeaderMissing()
		{
			HttpResponseMessage objResponse = ResponseWithContentDisposition(null);

			Assert.Null(RunnersPointApiClient.ParseSuggestedFileName(objResponse));
		}

		[Fact]
		public void ParseSuggestedFileName_ReturnsNullWhenNoFilenameParameter()
		{
			HttpResponseMessage objResponse = ResponseWithContentDisposition("attachment");

			Assert.Null(RunnersPointApiClient.ParseSuggestedFileName(objResponse));
		}

		[Fact]
		public void ParseDocument_MapsAllFieldsIncludingUpdatedAt()
		{
			Dictionary<string, object> objJson = new Dictionary<string, object>
			{
				{ "id", "doc-1" },
				{ "type", "character" },
				{ "gameProfileId", "profile-1" },
				{ "format", "application/xml" },
				{ "schemaVersion", "1" },
				{ "currentRevision", "rev-1" },
				{ "validationState", "accepted" },
				{ "metadata", new Dictionary<string, object> { { "displayName", "Kestrel" } } },
				{ "updatedAt", "2026-07-17T12:00:00+00:00" },
			};

			RunnersPointDocument objDocument = RunnersPointApiClient.ParseDocument(objJson);

			Assert.Equal("doc-1", objDocument.Id);
			Assert.Equal("character", objDocument.Type);
			Assert.Equal("profile-1", objDocument.GameProfileId);
			Assert.Equal("application/xml", objDocument.Format);
			Assert.Equal("rev-1", objDocument.CurrentRevision);
			Assert.Equal("accepted", objDocument.ValidationState);
			Assert.Equal("Kestrel", objDocument.DisplayName);
			// Regression test: UpdatedAt used to be parsed into a discarded local variable and never
			// actually assigned to the DTO, so it always came back as default(DateTime).
			Assert.Equal(new DateTime(2026, 7, 17, 12, 0, 0, DateTimeKind.Utc), objDocument.UpdatedAt.ToUniversalTime());
		}

		[Fact]
		public void ParseDocument_FallsBackToMetadataNameWhenDisplayNameMissing()
		{
			// The real server's character-document metadata extractor currently populates
			// metadata.name (from the XML's charactername/name element), not metadata.displayName as
			// the OpenAPI spec and this client otherwise expect. Falling back keeps the document list
			// showing the actual character name instead of a raw GUID until that's reconciled
			// server-side.
			Dictionary<string, object> objJson = new Dictionary<string, object>
			{
				{ "id", "doc-1" },
				{ "metadata", new Dictionary<string, object> { { "name", "Kestrel" } } },
			};

			RunnersPointDocument objDocument = RunnersPointApiClient.ParseDocument(objJson);

			Assert.Equal("Kestrel", objDocument.DisplayName);
		}

		[Fact]
		public void ParseDocument_PrefersDisplayNameOverName()
		{
			Dictionary<string, object> objJson = new Dictionary<string, object>
			{
				{ "id", "doc-1" },
				{ "metadata", new Dictionary<string, object> { { "displayName", "Kestrel" }, { "name", "Old Name" } } },
			};

			RunnersPointDocument objDocument = RunnersPointApiClient.ParseDocument(objJson);

			Assert.Equal("Kestrel", objDocument.DisplayName);
		}

		[Fact]
		public void ExtractRawETag_ReadsUnquotedHeaderValue()
		{
			// Regression test: the real server sends ETag as a bare token (e.g. a revision UUID)
			// without the DQUOTEs RFC 7232 requires. HttpResponseMessage.Headers.ETag's strongly-typed
			// EntityTagHeaderValue parser silently refuses that and returns null instead of throwing,
			// which meant every If-Match sent back on a later pushRevision/archive call was empty and
			// got rejected by the server. ExtractRawETag reads the header as plain text instead.
			HttpResponseMessage objResponse = new HttpResponseMessage();
			objResponse.Headers.TryAddWithoutValidation("ETag", "4ee72674-de9f-402d-879c-ce9023094a25");

			Assert.Equal("4ee72674-de9f-402d-879c-ce9023094a25", RunnersPointApiClient.ExtractRawETag(objResponse));
			// Confirms the scenario the fix works around actually reproduces against .NET's own parser.
			Assert.Null(objResponse.Headers.ETag);
		}

		[Fact]
		public void ExtractRawETag_ReturnsNullWhenHeaderMissing()
		{
			HttpResponseMessage objResponse = new HttpResponseMessage();

			Assert.Null(RunnersPointApiClient.ExtractRawETag(objResponse));
		}

		[Fact]
		public void ParseDocument_ToleratesMissingOptionalFields()
		{
			Dictionary<string, object> objJson = new Dictionary<string, object>
			{
				{ "id", "doc-1" },
			};

			RunnersPointDocument objDocument = RunnersPointApiClient.ParseDocument(objJson);

			Assert.Equal("doc-1", objDocument.Id);
			Assert.Equal("", objDocument.Type);
			Assert.Null(objDocument.DisplayName);
		}

		[Fact]
		public void BuildMetadataPatchBody_SetsNonEmptyValues()
		{
			byte[] bytBody = RunnersPointApiClient.BuildMetadataPatchBody("Kestrel", "A shadowrunner.", "https://example.com/portrait.png");

			JavaScriptSerializer objSerializer = new JavaScriptSerializer();
			Dictionary<string, object> objPatch = objSerializer.Deserialize<Dictionary<string, object>>(Encoding.UTF8.GetString(bytBody));

			Assert.Equal("Kestrel", objPatch["displayName"]);
			Assert.Equal("A shadowrunner.", objPatch["description"]);
			Assert.Equal("https://example.com/portrait.png", objPatch["imageUrl"]);
		}

		[Fact]
		public void BuildMetadataPatchBody_ClearsEmptyValuesWithJsonNull()
		{
			// An empty/null argument must produce a JSON null (RFC 7396 field-clear), not an omitted key
			// or an empty string - the server treats a present empty string as "" (an actual value), only
			// null clears the field.
			byte[] bytBody = RunnersPointApiClient.BuildMetadataPatchBody("", null, "");

			JavaScriptSerializer objSerializer = new JavaScriptSerializer();
			Dictionary<string, object> objPatch = objSerializer.Deserialize<Dictionary<string, object>>(Encoding.UTF8.GetString(bytBody));

			Assert.True(objPatch.ContainsKey("displayName"));
			Assert.Null(objPatch["displayName"]);
			Assert.True(objPatch.ContainsKey("description"));
			Assert.Null(objPatch["description"]);
			Assert.True(objPatch.ContainsKey("imageUrl"));
			Assert.Null(objPatch["imageUrl"]);
		}

		[Fact]
		public void ParseSharedDocument_MapsShareGrant()
		{
			Dictionary<string, object> objJson = new Dictionary<string, object>
			{
				{ "id", "doc-1" },
				{ "type", "character" },
				{ "validationState", "accepted" },
				{ "share", new Dictionary<string, object>
					{
						{ "permission", "write" },
						{ "status", "active" },
						{ "expiresAt", "2026-08-17T00:00:00+00:00" },
					}
				},
			};

			RunnersPointSharedDocument objShared = RunnersPointApiClient.ParseSharedDocument(objJson);

			Assert.Equal("doc-1", objShared.Id);
			Assert.Equal("write", objShared.Permission);
			Assert.Equal("active", objShared.ShareStatus);
			Assert.True(objShared.ExpiresAt.HasValue);
			Assert.Equal(new DateTime(2026, 8, 17, 0, 0, 0, DateTimeKind.Utc), objShared.ExpiresAt.Value.ToUniversalTime());
		}

		[Fact]
		public void ParseSharedDocument_ToleratesNullExpiresAt()
		{
			Dictionary<string, object> objJson = new Dictionary<string, object>
			{
				{ "id", "doc-1" },
				{ "share", new Dictionary<string, object>
					{
						{ "permission", "read" },
						{ "status", "active" },
						{ "expiresAt", null },
					}
				},
			};

			RunnersPointSharedDocument objShared = RunnersPointApiClient.ParseSharedDocument(objJson);

			Assert.Equal("read", objShared.Permission);
			Assert.False(objShared.ExpiresAt.HasValue);
		}

		[Fact]
		public void ParseSharedDocument_ToleratesMissingShare()
		{
			Dictionary<string, object> objJson = new Dictionary<string, object>
			{
				{ "id", "doc-1" },
			};

			RunnersPointSharedDocument objShared = RunnersPointApiClient.ParseSharedDocument(objJson);

			Assert.Equal("doc-1", objShared.Id);
			Assert.Null(objShared.Permission);
			Assert.False(objShared.ExpiresAt.HasValue);
		}
	}
}
