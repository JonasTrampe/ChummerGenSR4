using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Forms;
using Serilog;

namespace Chummer
{
	/// <summary>
	/// Cloud-aware Save logic shared by frmCreate and frmCareer's SaveCharacter(). A character not
	/// linked to a cloud document behaves exactly as before - untouched. A cloud-linked character has
	/// its save content built in memory first (via Character.Save(Stream)) so that a user Cancel at the
	/// conflict prompt genuinely writes nothing to disk; only once the push has resolved (pushed,
	/// declined, or failed) does anything get written to FileName.
	/// </summary>
	public static class CloudSaveHelper
	{
		// Same states PushCurrent already treats as unsafe to push on top of (see frmCloudDocuments) -
		// the current revision hasn't finished quarantine/validation yet.
		private static readonly string[] s_astrInFlightStates = { "quarantined", "processing" };

		public static async Task<CloudSaveOutcome> SaveAsync(IWin32Window objOwner, Character objCharacter, RunnersPointAuth objAuth, string strLastPushedHash)
		{
			byte[] bytContent;
			using (MemoryStream objStream = new MemoryStream())
			{
				objCharacter.Save(objStream);
				bytContent = objStream.ToArray();
			}

			if (string.IsNullOrEmpty(objCharacter.CloudDocumentId))
			{
				WriteToDisk(objCharacter, bytContent);
				return new CloudSaveOutcome { Result = CloudSaveResult.Saved, LastPushedHash = strLastPushedHash };
			}

			string strHash = Hash(bytContent);
			if (strHash == strLastPushedHash)
			{
				// Unchanged since the last successful push this session - skip the network round trip.
				// The local file may still be behind (a prior push attempt could have failed before ever
				// reaching this point), so it still gets written.
				WriteToDisk(objCharacter, bytContent);
				return new CloudSaveOutcome { Result = CloudSaveResult.Saved, LastPushedHash = strLastPushedHash };
			}

			RunnersPointApiClient objApiClient = new RunnersPointApiClient(objAuth);
			PushOutcome objOutcome = await TryPushAsync(objOwner, objApiClient, objCharacter, bytContent);

			if (objOutcome.Cancelled)
				return new CloudSaveOutcome { Result = CloudSaveResult.Cancelled, LastPushedHash = strLastPushedHash };

			if (objOutcome.Pushed)
			{
				strLastPushedHash = strHash;
				objCharacter.CloudLastKnownRevisionId = objOutcome.RevisionId;
				// CloudLastKnownRevisionId needs to be in the saved bytes too, not just the in-memory
				// Character - rebuild the content now that it's set.
				using (MemoryStream objStream = new MemoryStream())
				{
					objCharacter.Save(objStream);
					bytContent = objStream.ToArray();
				}
			}
			else if (!string.IsNullOrEmpty(objOutcome.ErrorMessage))
			{
				MessageBox.Show(objOwner, LanguageManager.Instance.GetString("Message_CloudSave_PushFailed").Replace("{0}", objOutcome.ErrorMessage),
					LanguageManager.Instance.GetString("MessageTitle_CloudSave_PushFailed"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
			}

			WriteToDisk(objCharacter, bytContent);
			return new CloudSaveOutcome { Result = CloudSaveResult.Saved, LastPushedHash = strLastPushedHash };
		}

		private class PushOutcome
		{
			public bool Pushed;
			public bool Cancelled;
			public string RevisionId;
			public string ErrorMessage;
		}

		private static async Task<PushOutcome> TryPushAsync(IWin32Window objOwner, RunnersPointApiClient objApiClient, Character objCharacter, byte[] bytContent)
		{
			try
			{
				Tuple<RunnersPointDocument, string> objCurrent = await objApiClient.GetDocumentAsync(objCharacter.CloudDocumentId);
				if (s_astrInFlightStates.Contains(objCurrent.Item1.ValidationState))
				{
					return new PushOutcome { ErrorMessage = LanguageManager.Instance.GetString("String_CloudSave_PushInFlight") };
				}

				Tuple<string, string> objGameProfile = await ResolveGameProfileAsync(objApiClient);
				if (objGameProfile == null)
					return new PushOutcome { ErrorMessage = LanguageManager.Instance.GetString("String_Cloud_NoGameProfile") };

				RunnersPointRevisionStatus objStatus = await objApiClient.PushRevisionAsync(
					objCharacter.CloudDocumentId, bytContent, objCurrent.Item2, objGameProfile.Item1, objGameProfile.Item2);
				return new PushOutcome { Pushed = true, RevisionId = objStatus.RevisionId };
			}
			catch (RunnersPointApiException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
			{
				return await HandleConflictAsync(objOwner, objApiClient, objCharacter, bytContent);
			}
			catch (RunnersPointApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
			{
				return new PushOutcome { ErrorMessage = LanguageManager.Instance.GetString("String_Cloud_AuthExpired") };
			}
			catch (RunnersPointApiException ex)
			{
				return new PushOutcome { ErrorMessage = ex.Message };
			}
			catch (System.Net.Http.HttpRequestException ex)
			{
				Log.Warning(ex, "RunnersPoint server unreachable during cloud save");
				return new PushOutcome { ErrorMessage = LanguageManager.Instance.GetString("String_Cloud_ServerUnreachable") };
			}
		}

		/// <summary>
		/// A 412 means the server's currentRevision has moved on since we last saw it - download that
		/// revision, diff it against the content we're about to save, and let the user decide whether
		/// their local edits should still win.
		/// </summary>
		private static async Task<PushOutcome> HandleConflictAsync(IWin32Window objOwner, RunnersPointApiClient objApiClient, Character objCharacter, byte[] bytContent)
		{
			Tuple<RunnersPointDocument, string> objCurrent;
			byte[] bytServerContent;
			try
			{
				objCurrent = await objApiClient.GetDocumentAsync(objCharacter.CloudDocumentId);
				Tuple<byte[], string> objDownload = await objApiClient.DownloadRevisionAsync(objCharacter.CloudDocumentId, objCurrent.Item1.CurrentRevision);
				bytServerContent = objDownload.Item1;
			}
			catch (Exception ex)
			{
				Log.Warning(ex, "Could not download the server's current revision to build a conflict diff");
				return new PushOutcome { ErrorMessage = LanguageManager.Instance.GetString("String_Cloud_ServerUnreachable") };
			}

			string strTempFile = Path.GetTempFileName();
			CharacterDiffResult objDiff;
			try
			{
				File.WriteAllBytes(strTempFile, bytServerContent);
				Character objServerCharacter = new Character { FileName = strTempFile };
				if (!objServerCharacter.Load())
					return new PushOutcome { ErrorMessage = LanguageManager.Instance.GetString("String_CloudSave_ConflictUnreadable") };
				objDiff = CharacterDiff.Compare(objCharacter, objServerCharacter);
			}
			finally
			{
				try { File.Delete(strTempFile); } catch (IOException) { }
			}

			using (frmCloudConflict frmConflict = new frmCloudConflict(objDiff))
			{
				if (frmConflict.ShowDialog(objOwner) != DialogResult.OK || frmConflict.Choice == frmCloudConflict.ConflictChoice.Cancel)
					return new PushOutcome { Cancelled = true };

				if (frmConflict.Choice == frmCloudConflict.ConflictChoice.SaveLocallyOnly)
					return new PushOutcome { Pushed = false };

				// OverwriteServer - retry once against the fresh ETag we just fetched above.
				try
				{
					Tuple<string, string> objGameProfile = await ResolveGameProfileAsync(objApiClient);
					if (objGameProfile == null)
						return new PushOutcome { ErrorMessage = LanguageManager.Instance.GetString("String_Cloud_NoGameProfile") };

					RunnersPointRevisionStatus objStatus = await objApiClient.PushRevisionAsync(
						objCharacter.CloudDocumentId, bytContent, objCurrent.Item2, objGameProfile.Item1, objGameProfile.Item2);
					return new PushOutcome { Pushed = true, RevisionId = objStatus.RevisionId };
				}
				catch (Exception ex)
				{
					Log.Warning(ex, "Retry-after-conflict push also failed");
					return new PushOutcome { ErrorMessage = ex.Message };
				}
			}
		}

		/// <summary>
		/// Resolves the SR4 GameProfileId/format the same way frmCloudDocuments does - looked up fresh
		/// each time rather than cached, since this can be called from a window that never opened Cloud
		/// Documents at all this session.
		/// </summary>
		private static async Task<Tuple<string, string>> ResolveGameProfileAsync(RunnersPointApiClient objApiClient)
		{
			RunnersPointCapabilities objCapabilities = await objApiClient.GetCapabilitiesAsync();
			RunnersPointGameProfile objProfile = objCapabilities.GameProfiles.FirstOrDefault(
				p => p.System.IndexOf("Shadowrun", StringComparison.OrdinalIgnoreCase) >= 0 && p.Edition.Contains("4"));
			if (objProfile == null)
				return null;
			string strFormat = objProfile.Formats.FirstOrDefault(f => f == "application/xml") ?? objProfile.Formats.FirstOrDefault();
			return string.IsNullOrEmpty(strFormat) ? null : new Tuple<string, string>(objProfile.Id, strFormat);
		}

		private static string Hash(byte[] bytContent)
		{
			using (SHA256 objSha256 = SHA256.Create())
				return Convert.ToBase64String(objSha256.ComputeHash(bytContent));
		}

		private static void WriteToDisk(Character objCharacter, byte[] bytContent)
		{
			File.WriteAllBytes(objCharacter.FileName, bytContent);
		}
	}
}
