using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
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
