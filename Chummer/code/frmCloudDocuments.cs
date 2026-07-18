using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Serilog;

namespace Chummer
{
	/// <summary>
	/// Personal cloud storage/sync against the RunnersPoint Character Document Storage API. Replaces
	/// the old Omae online-sharing feature. Covers two surfaces: the user's own documents
	/// (create/push/download/archive), and documents explicitly shared with the user by someone else
	/// (browse/download, and push an update if the grant is read-write) - there is no public
	/// marketplace/discovery here, that lives on the RunnersPoint website.
	/// </summary>
	public partial class frmCloudDocuments : Form
	{
		private const string DocumentType = "character";

		// Masked placeholder shown in the API Token field when a token is already stored, so the field
		// isn't just blank and misleadingly implying nothing is configured. Never submitted as-is - it's
		// cleared as soon as the field gets focus, or ignored if somehow submitted unchanged.
		private const string ApiTokenPlaceholder = "................................";
		private bool _blnApiTokenPlaceholderShown;

		private readonly RunnersPointAuth _objAuth = new RunnersPointAuth();
		private RunnersPointApiClient _objApiClient;
		private readonly Character _objActiveCharacter;
		private string _strGameProfileId = "";
		private string _strGameProfileFormat = "";

		private bool SharedMode => rdoSharedWithMe.Checked;

		public frmCloudDocuments(Character objActiveCharacter)
		{
			_objActiveCharacter = objActiveCharacter;
			InitializeComponent();
			LanguageManager.Instance.Load(GlobalOptions.Instance.Language, this);
		}

		private async void frmCloudDocuments_Load(object sender, EventArgs e)
		{
			_objApiClient = new RunnersPointApiClient(_objAuth);
			cmdPushCurrent.Enabled = _objActiveCharacter != null;
			cmdEditMetadata.Enabled = _objActiveCharacter != null;
			UpdateSelectionButtons();

			// Reflect whatever's actually stored, rather than always defaulting to the API Token radio,
			// so the UI doesn't contradict a login made via the other method in an earlier session.
			if (_objAuth.HasStoredLogin() && !_objAuth.IsApiTokenLogin())
				rdoAuthOAuth.Checked = true;
			UpdateAuthModeVisibility();
			UpdateConnectionState();
			UpdateApiTokenPlaceholder();

			if (!_objAuth.HasStoredLogin())
			{
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_NotLoggedIn"));
				return;
			}

			await RefreshAsync();
		}

		private void rdoAuthMode_CheckedChanged(object sender, EventArgs e)
		{
			if (sender is RadioButton radio && !radio.Checked)
				return;
			UpdateAuthModeVisibility();
		}

		private void UpdateAuthModeVisibility()
		{
			lblApiToken.Visible = rdoAuthApiToken.Checked;
			txtApiToken.Visible = rdoAuthApiToken.Checked;
			cmdUseApiToken.Visible = rdoAuthApiToken.Checked;
			cmdLogin.Visible = rdoAuthOAuth.Checked;
		}

		/// <summary>
		/// Fills the API Token field with a masked placeholder when a token is already stored, so the
		/// field visibly says "something's configured" instead of looking empty/unset. Clears back out
		/// (via txtApiToken_Enter) the moment the user actually focuses the field to type a new one.
		/// </summary>
		private void UpdateApiTokenPlaceholder()
		{
			if (_objAuth.HasStoredLogin() && _objAuth.IsApiTokenLogin())
			{
				txtApiToken.Text = ApiTokenPlaceholder;
				_blnApiTokenPlaceholderShown = true;
			}
			else
			{
				txtApiToken.Text = "";
				_blnApiTokenPlaceholderShown = false;
			}
		}

		private void txtApiToken_Enter(object sender, EventArgs e)
		{
			if (!_blnApiTokenPlaceholderShown)
				return;
			txtApiToken.Text = "";
			_blnApiTokenPlaceholderShown = false;
		}

		/// <summary>
		/// Reflects whether/how we're actually logged in - separate from the short-lived cmdRefresh-style
		/// messages in lblStatus, so "am I connected, and how" stays visible instead of getting
		/// overwritten by the next status update. Also keeps cmdLogout from staying clickable (and
		/// implying there's something to log out of) once there's no stored login left.
		/// </summary>
		private void UpdateConnectionState()
		{
			bool blnLoggedIn = _objAuth.HasStoredLogin();
			cmdLogout.Enabled = blnLoggedIn;

			if (!blnLoggedIn)
			{
				lblConnectionState.Text = LanguageManager.Instance.GetString("String_Cloud_ConnectionState_NotConnected");
				return;
			}

			lblConnectionState.Text = _objAuth.IsApiTokenLogin()
				? LanguageManager.Instance.GetString("String_Cloud_ConnectionState_ApiToken")
				: LanguageManager.Instance.GetString("String_Cloud_ConnectionState_OAuth");
		}

		private async void cmdLogin_Click(object sender, EventArgs e)
		{
			try
			{
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_LoggingIn"));
				await _objAuth.LoginAsync();
				UpdateConnectionState();
				UpdateApiTokenPlaceholder();
				await RefreshAsync();
			}
			catch (System.Net.Http.HttpRequestException ex)
			{
				Log.Warning(ex, "RunnersPoint server unreachable");
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_ServerUnreachable"));
			}
			catch (Exception ex)
			{
				Log.Error(ex, "RunnersPoint login failed");
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_LoginFailed").Replace("{0}", ex.Message));
			}
		}

		private async void cmdUseApiToken_Click(object sender, EventArgs e)
		{
			// Nothing was actually typed - the field still shows the "a token is stored" placeholder,
			// not a real value to submit.
			if (_blnApiTokenPlaceholderShown)
				return;

			try
			{
				_objAuth.SetApiToken(txtApiToken.Text);
				UpdateConnectionState();
				UpdateApiTokenPlaceholder();
				await RefreshAsync();
			}
			catch (System.Net.Http.HttpRequestException ex)
			{
				Log.Warning(ex, "RunnersPoint server unreachable");
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_ServerUnreachable"));
			}
			catch (Exception ex)
			{
				Log.Error(ex, "RunnersPoint login failed");
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_LoginFailed").Replace("{0}", ex.Message));
			}
		}

		private void cmdLogout_Click(object sender, EventArgs e)
		{
			_objAuth.Logout();
			lstDocuments.Items.Clear();
			UpdateConnectionState();
			UpdateApiTokenPlaceholder();
			UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_NotLoggedIn"));
		}

		/// <summary>
		/// Clears a stored login that the server no longer honors (expired or revoked) so the UI doesn't
		/// just keep silently failing every call - the user needs to log in or paste a new token.
		/// </summary>
		private void HandleAuthExpired()
		{
			Log.Warning("RunnersPoint login rejected as expired/revoked (401) - clearing stored login");
			_objAuth.Logout();
			lstDocuments.Items.Clear();
			UpdateConnectionState();
			UpdateApiTokenPlaceholder();
			UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_AuthExpired"));
		}

		private async void rdoDocumentMode_CheckedChanged(object sender, EventArgs e)
		{
			// CheckedChanged fires for both the radio button losing and gaining the check - only react once.
			if (sender is RadioButton radio && !radio.Checked)
				return;

			cmdPushCurrent.Enabled = !SharedMode && _objActiveCharacter != null;
			cmdArchive.Visible = !SharedMode;
			cmdPushShared.Visible = SharedMode;
			await RefreshAsync();
		}

		private async System.Threading.Tasks.Task RefreshAsync()
		{
			try
			{
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_Refreshing"));

				if (_strGameProfileId == "")
				{
					RunnersPointCapabilities objCapabilities = await _objApiClient.GetCapabilitiesAsync();
					// Assumes the SR4 GameProfile is already provisioned server-side, per the RunnersPoint
					// integration design - Chummer just needs to find it, not create it.
					RunnersPointGameProfile objProfile = objCapabilities.GameProfiles.FirstOrDefault(
						p => p.System.IndexOf("Shadowrun", StringComparison.OrdinalIgnoreCase) >= 0 && p.Edition.Contains("4"));
					if (objProfile == null)
					{
						UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_NoGameProfile"));
						return;
					}
					string strFormat = objProfile.Formats.FirstOrDefault(f => f == "application/xml") ?? objProfile.Formats.FirstOrDefault();
					if (string.IsNullOrEmpty(strFormat))
					{
						UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_NoGameProfile"));
						return;
					}

					// The server advertises which document types/formats it actually accepts via
					// Capabilities.documentTypes - verify "character"/strFormat is really one of them
					// instead of just assuming it and letting create/pushRevision fail with a 422 later.
					RunnersPointDocumentTypeCapability objCharacterType = objCapabilities.DocumentTypes.FirstOrDefault(t => t.Id == DocumentType);
					if (objCharacterType == null || !objCharacterType.Formats.Any(f => f.MediaType == strFormat))
					{
						UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_UnsupportedDocumentType"));
						return;
					}

					_strGameProfileId = objProfile.Id;
					_strGameProfileFormat = strFormat;
				}

				lstDocuments.Items.Clear();
				string strCursor = null;
				if (SharedMode)
				{
					do
					{
						RunnersPointSharedDocumentPage objPage = await _objApiClient.ListSharedDocumentsAsync(_strGameProfileId, strCursor);
						foreach (RunnersPointSharedDocument objDocument in objPage.Items)
							lstDocuments.Items.Add(BuildListItem(objDocument));
						strCursor = objPage.NextCursor;
					} while (!string.IsNullOrEmpty(strCursor));
				}
				else
				{
					do
					{
						RunnersPointDocumentPage objPage = await _objApiClient.ListDocumentsAsync(_strGameProfileId, strCursor);
						foreach (RunnersPointDocument objDocument in objPage.Items)
							lstDocuments.Items.Add(BuildListItem(objDocument));
						strCursor = objPage.NextCursor;
					} while (!string.IsNullOrEmpty(strCursor));
				}

				UpdateSelectionButtons();
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_Ready"));
			}
			catch (RunnersPointApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
			{
				HandleAuthExpired();
			}
			catch (System.Net.Http.HttpRequestException ex)
			{
				// Distinguished from "wrong/expired credentials" - the login itself is still fine, the
				// server just isn't reachable right now (offline, wrong CloudApiBaseUrl, DNS/connection
				// failure). Don't wipe the stored login over what's likely a transient network problem.
				Log.Warning(ex, "RunnersPoint server unreachable");
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_ServerUnreachable"));
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Cloud Documents operation failed");
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_Error").Replace("{0}", ex.Message));
			}
		}

		private static ListViewItem BuildListItem(RunnersPointDocument objDocument)
		{
			string strShare = "";
			if (objDocument is RunnersPointSharedDocument objShared)
			{
				strShare = objShared.Permission + " (" + objShared.ShareStatus;
				if (objShared.ExpiresAt.HasValue)
					strShare += ", expires " + objShared.ExpiresAt.Value.ToLocalTime().ToString("d");
				strShare += ")";
			}

			ListViewItem objItem = new ListViewItem(new[]
			{
				string.IsNullOrEmpty(objDocument.DisplayName) ? objDocument.Id : objDocument.DisplayName,
				objDocument.ValidationState,
				objDocument.UpdatedAt.ToString("g"),
				strShare,
			});
			objItem.Tag = objDocument;
			return objItem;
		}

		private async void cmdRefresh_Click(object sender, EventArgs e)
		{
			await RefreshAsync();
		}

		private async void cmdEditMetadata_Click(object sender, EventArgs e)
		{
			if (_objActiveCharacter == null)
				return;

			DialogResult objResult;
			using (frmCloudMetadata frmMetadata = new frmCloudMetadata(_objActiveCharacter))
				objResult = frmMetadata.ShowDialog(this);

			// The dialog already saved the values locally regardless of the outcome below - pushing to
			// the server is a separate, best-effort step, and only applies once this character is
			// actually linked to a cloud document (CloudDocumentId set via a prior push).
			if (objResult != DialogResult.OK || string.IsNullOrEmpty(_objActiveCharacter.CloudDocumentId))
				return;

			try
			{
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_Pushing"));
				Tuple<RunnersPointDocument, string> objCurrent = await _objApiClient.GetDocumentAsync(_objActiveCharacter.CloudDocumentId);
				await _objApiClient.UpdateDocumentMetadataAsync(_objActiveCharacter.CloudDocumentId, objCurrent.Item2,
					_objActiveCharacter.CloudMetadataDisplayName, _objActiveCharacter.CloudMetadataDescription, _objActiveCharacter.CloudMetadataImageUrl);
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_MetadataUpdated"));
				await RefreshAsync();
			}
			catch (RunnersPointApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
			{
				HandleAuthExpired();
			}
			catch (RunnersPointApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.PreconditionFailed)
			{
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_PushStale"));
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Cloud Documents operation failed");
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_Error").Replace("{0}", ex.Message));
			}
		}

		// States in which a document's most recent revision hasn't finished the async quarantine/
		// validation pipeline yet. Pushing on top of one of these would race the in-flight validation.
		private static readonly string[] s_astrInFlightStates = { "quarantined", "processing" };

		private async void cmdPushCurrent_Click(object sender, EventArgs e)
		{
			if (_objActiveCharacter == null)
				return;
			if (string.IsNullOrEmpty(_objActiveCharacter.FileName))
			{
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_NotSavedLocally"));
				return;
			}

			try
			{
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_Pushing"));
				byte[] bytContent = File.ReadAllBytes(_objActiveCharacter.FileName);
				RunnersPointRevisionStatus objStatus;

				if (string.IsNullOrEmpty(_objActiveCharacter.CloudDocumentId))
				{
					objStatus = await _objApiClient.CreateDocumentAsync(bytContent, _strGameProfileId, _strGameProfileFormat);
					_objActiveCharacter.CloudDocumentId = objStatus.DocumentId;
					_objActiveCharacter.Save();
				}
				else
				{
					Tuple<RunnersPointDocument, string> objCurrent = await _objApiClient.GetDocumentAsync(_objActiveCharacter.CloudDocumentId);
					if (s_astrInFlightStates.Contains(objCurrent.Item1.ValidationState))
					{
						UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_PushInFlight"));
						return;
					}
					objStatus = await _objApiClient.PushRevisionAsync(_objActiveCharacter.CloudDocumentId, bytContent, objCurrent.Item2, _strGameProfileId, _strGameProfileFormat);
				}

				string strAccepted = LanguageManager.Instance.GetString("String_Cloud_PushAccepted");
				if (objStatus.Messages.Count > 0)
					strAccepted += " " + string.Join(" ", objStatus.Messages);
				UpdateStatus(strAccepted);
				await RefreshAsync();
			}
			catch (RunnersPointApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
			{
				HandleAuthExpired();
			}
			catch (RunnersPointApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.PreconditionFailed)
			{
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_PushStale"));
			}
			catch (RunnersPointApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
			{
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_PushIdempotencyConflict"));
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Cloud Documents operation failed");
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_Error").Replace("{0}", ex.Message));
			}
		}

		private async void cmdPushShared_Click(object sender, EventArgs e)
		{
			if (_objActiveCharacter == null || lstDocuments.SelectedItems.Count == 0)
				return;
			if (string.IsNullOrEmpty(_objActiveCharacter.FileName))
			{
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_NotSavedLocally"));
				return;
			}
			if (!(lstDocuments.SelectedItems[0].Tag is RunnersPointSharedDocument objDocument))
				return;
			if (objDocument.Permission != "write" || objDocument.ShareStatus != "active")
				return;

			if (MessageBox.Show(LanguageManager.Instance.GetString("Message_Cloud_ConfirmPushShared").Replace("{0}", objDocument.DisplayName ?? objDocument.Id),
				LanguageManager.Instance.GetString("MessageTitle_Cloud_ConfirmPushShared"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
				return;

			try
			{
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_Pushing"));
				byte[] bytContent = File.ReadAllBytes(_objActiveCharacter.FileName);

				Tuple<RunnersPointSharedDocument, string> objCurrent = await _objApiClient.GetSharedDocumentAsync(objDocument.Id);
				if (s_astrInFlightStates.Contains(objCurrent.Item1.ValidationState))
				{
					UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_PushInFlight"));
					return;
				}

				RunnersPointRevisionStatus objStatus = await _objApiClient.PushSharedDocumentRevisionAsync(
					objDocument.Id, bytContent, objCurrent.Item2, _strGameProfileId, _strGameProfileFormat);

				string strAccepted = LanguageManager.Instance.GetString("String_Cloud_PushAccepted");
				if (objStatus.Messages.Count > 0)
					strAccepted += " " + string.Join(" ", objStatus.Messages);
				UpdateStatus(strAccepted);
				await RefreshAsync();
			}
			catch (RunnersPointApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
			{
				HandleAuthExpired();
			}
			catch (RunnersPointApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.PreconditionFailed)
			{
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_PushStale"));
			}
			catch (RunnersPointApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
			{
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_PushIdempotencyConflict"));
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Cloud Documents operation failed");
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_Error").Replace("{0}", ex.Message));
			}
		}

		// Whether downloadRevision is expected to work for a given document.validationState - "accepted"
		// is the only state with a validated, servable revision; everything else (quarantined/processing/
		// rejected/archived) is blocked client-side with an explanation instead of letting the download
		// call fail with an unclear server error.
		private static readonly string[] s_astrDownloadableStates = { "accepted" };

		private async void cmdDownload_Click(object sender, EventArgs e)
		{
			if (lstDocuments.SelectedItems.Count == 0)
				return;
			RunnersPointDocument objDocument = (RunnersPointDocument)lstDocuments.SelectedItems[0].Tag;
			bool blnShared = objDocument is RunnersPointSharedDocument;

			// Re-fetch the document immediately before downloading rather than trusting the list
			// snapshot - it may have gone stale (someone else pushed a new revision, or it moved out of
			// a downloadable state) since the list was last refreshed.
			try
			{
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_Refreshing"));
				objDocument = blnShared
					? (await _objApiClient.GetSharedDocumentAsync(objDocument.Id)).Item1
					: (await _objApiClient.GetDocumentAsync(objDocument.Id)).Item1;
			}
			catch (RunnersPointApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
			{
				HandleAuthExpired();
				return;
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Cloud Documents operation failed");
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_Error").Replace("{0}", ex.Message));
				return;
			}

			if (!s_astrDownloadableStates.Contains(objDocument.ValidationState))
			{
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_NotDownloadable").Replace("{0}", objDocument.ValidationState));
				return;
			}

			Tuple<byte[], string> objDownload;
			try
			{
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_Downloading"));
				objDownload = blnShared
					? await _objApiClient.DownloadSharedDocumentRevisionAsync(objDocument.Id, objDocument.CurrentRevision)
					: await _objApiClient.DownloadRevisionAsync(objDocument.Id, objDocument.CurrentRevision);
			}
			catch (RunnersPointApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
			{
				HandleAuthExpired();
				return;
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Cloud Documents operation failed");
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_Error").Replace("{0}", ex.Message));
				return;
			}

			// Prefer the server's suggested filename (Content-Disposition) when it sent one; fall back to
			// the document's own display name/id otherwise.
			string strSuggestedFileName = !string.IsNullOrEmpty(objDownload.Item2)
				? objDownload.Item2
				: string.IsNullOrEmpty(objDocument.DisplayName) ? objDocument.Id : objDocument.DisplayName;

			SaveFileDialog objDialog = new SaveFileDialog();
			objDialog.Filter = "Chummer Files (*.chum)|*.chum|All Files (*.*)|*.*";
			objDialog.FileName = strSuggestedFileName;
			if (objDialog.ShowDialog(this) != DialogResult.OK)
				return;

			try
			{
				File.WriteAllBytes(objDialog.FileName, objDownload.Item1);

				// Make sure the saved file is linked back to the document it came from - a character
				// downloaded from somewhere else may never have had CloudDocumentId set, and without it
				// a later push from the opened character would create a duplicate document instead of
				// updating this one.
				Character objDownloaded = new Character { FileName = objDialog.FileName };
				if (objDownloaded.Load() && string.IsNullOrEmpty(objDownloaded.CloudDocumentId))
				{
					objDownloaded.CloudDocumentId = objDocument.Id;
					objDownloaded.Save();
				}

				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_Ready"));

				if (this.Owner is frmMain frmMainForm)
					frmMainForm.LoadCharacter(objDialog.FileName);
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Cloud Documents operation failed");
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_Error").Replace("{0}", ex.Message));
			}
		}

		private async void cmdArchive_Click(object sender, EventArgs e)
		{
			if (SharedMode || lstDocuments.SelectedItems.Count == 0)
				return;
			RunnersPointDocument objDocument = (RunnersPointDocument)lstDocuments.SelectedItems[0].Tag;
			bool blnArchived = objDocument.ValidationState == "archived";

			if (!blnArchived && MessageBox.Show(LanguageManager.Instance.GetString("Message_Cloud_ConfirmArchive").Replace("{0}", objDocument.DisplayName ?? objDocument.Id),
				LanguageManager.Instance.GetString("MessageTitle_Cloud_ConfirmArchive"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
				return;

			try
			{
				Tuple<RunnersPointDocument, string> objCurrent = await _objApiClient.GetDocumentAsync(objDocument.Id);
				if (blnArchived)
					await _objApiClient.UnarchiveDocumentAsync(objDocument.Id, objCurrent.Item2);
				else
					await _objApiClient.ArchiveDocumentAsync(objDocument.Id, objCurrent.Item2);
				await RefreshAsync();
			}
			catch (RunnersPointApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
			{
				HandleAuthExpired();
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Cloud Documents operation failed");
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_Error").Replace("{0}", ex.Message));
			}
		}

		private void lstDocuments_SelectedIndexChanged(object sender, EventArgs e)
		{
			UpdateSelectionButtons();
		}

		/// <summary>
		/// Whether the selected document's revisions can be purged from this dialog. Owner access
		/// (My Documents) always permits it; shared access only when the active grant carries the
		/// "purge" permission specifically - independent of "write"/"update", per the API's grant model.
		/// </summary>
		private static bool CanPurge(object objTag, bool blnShared)
		{
			return !blnShared || (objTag is RunnersPointSharedDocument objShared && objShared.Permission == "purge" && objShared.ShareStatus == "active");
		}

		private async void cmdRevisions_Click(object sender, EventArgs e)
		{
			if (lstDocuments.SelectedItems.Count == 0)
				return;
			RunnersPointDocument objDocument = (RunnersPointDocument)lstDocuments.SelectedItems[0].Tag;
			bool blnShared = SharedMode;
			bool blnCanPurge = CanPurge(objDocument, blnShared);

			using (frmCloudRevisions frmRevisions = new frmCloudRevisions(_objApiClient, objDocument, blnShared, blnCanPurge))
				frmRevisions.ShowDialog(this);

			await RefreshAsync();
		}

		private void UpdateSelectionButtons()
		{
			if (lstDocuments.SelectedItems.Count == 0)
			{
				cmdDownload.Enabled = false;
				cmdArchive.Enabled = false;
				cmdPushShared.Enabled = false;
				cmdRevisions.Enabled = false;
				return;
			}

			object objTag = lstDocuments.SelectedItems[0].Tag;
			cmdDownload.Enabled = true;
			cmdArchive.Enabled = !SharedMode;
			cmdRevisions.Enabled = true;
			cmdPushShared.Enabled = SharedMode && _objActiveCharacter != null && objTag is RunnersPointSharedDocument objShared
				&& objShared.Permission == "write" && objShared.ShareStatus == "active";

			// Archive and unarchive are mutually exclusive on the same document - reuse one button
			// rather than needing a second one in an already-tight button row.
			bool blnArchived = !SharedMode && objTag is RunnersPointDocument objDocument && objDocument.ValidationState == "archived";
			cmdArchive.Tag = blnArchived ? "Button_Cloud_Unarchive" : "Button_Cloud_Archive";
			cmdArchive.Text = LanguageManager.Instance.GetString(blnArchived ? "Button_Cloud_Unarchive" : "Button_Cloud_Archive");
		}

		private void UpdateStatus(string strText)
		{
			lblStatus.Text = strText;
		}

#if DEBUG
		/// <summary>
		/// Debug-build-only: dumps connection state and, if a document is selected, the raw request/
		/// response detail GetDebugDumpAsync exposes (full headers, both the typed and raw ETag parse,
		/// the raw body) - the sort of detail that only ever surfaces by looking at the wire format
		/// directly, not through the normal typed API surface.
		/// </summary>
		private async void cmdDebugInfo_Click(object sender, EventArgs e)
		{
			System.Text.StringBuilder objInfo = new System.Text.StringBuilder();
			objInfo.AppendLine("CloudApiBaseUrl: " + GlobalOptions.Instance.CloudApiBaseUrl);
			objInfo.AppendLine("HasStoredLogin: " + _objAuth.HasStoredLogin());
			objInfo.AppendLine("IsApiTokenLogin: " + _objAuth.IsApiTokenLogin());
			objInfo.AppendLine("GameProfileId: " + _strGameProfileId);
			objInfo.AppendLine("GameProfileFormat: " + _strGameProfileFormat);

			if (lstDocuments.SelectedItems.Count > 0 && lstDocuments.SelectedItems[0].Tag is RunnersPointDocument objDocument)
			{
				try
				{
					objInfo.AppendLine();
					objInfo.Append(await _objApiClient.GetDebugDumpAsync(objDocument.Id));
				}
				catch (Exception ex)
				{
					objInfo.AppendLine();
					objInfo.AppendLine("Failed to fetch document debug dump: " + ex);
				}
			}

			string strInfo = objInfo.ToString();
			Log.Debug("Cloud Documents debug info requested:{NewLine}{Info}", Environment.NewLine, strInfo);
			MessageBox.Show(strInfo, "Cloud Documents Debug Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}
#endif
	}
}
