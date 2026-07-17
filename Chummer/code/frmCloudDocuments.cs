using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

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
			UpdateSelectionButtons();

			if (!_objAuth.HasStoredLogin())
			{
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_NotLoggedIn"));
				return;
			}

			await RefreshAsync();
		}

		private async void cmdLogin_Click(object sender, EventArgs e)
		{
			try
			{
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_LoggingIn"));
				await _objAuth.LoginAsync();
				await RefreshAsync();
			}
			catch (Exception ex)
			{
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_LoginFailed").Replace("{0}", ex.Message));
			}
		}

		private async void cmdUseApiToken_Click(object sender, EventArgs e)
		{
			try
			{
				_objAuth.SetApiToken(txtApiToken.Text);
				txtApiToken.Text = "";
				await RefreshAsync();
			}
			catch (Exception ex)
			{
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_LoginFailed").Replace("{0}", ex.Message));
			}
		}

		private void cmdLogout_Click(object sender, EventArgs e)
		{
			_objAuth.Logout();
			lstDocuments.Items.Clear();
			UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_NotLoggedIn"));
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
					_strGameProfileId = objProfile.Id;
					_strGameProfileFormat = objProfile.Formats.FirstOrDefault(f => f == "chum") ?? objProfile.Formats.FirstOrDefault() ?? "chum";
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
			catch (Exception ex)
			{
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_Error").Replace("{0}", ex.Message));
			}
		}

		private static ListViewItem BuildListItem(RunnersPointDocument objDocument)
		{
			string strShare = "";
			if (objDocument is RunnersPointSharedDocument objShared)
				strShare = objShared.Permission + " (" + objShared.ShareStatus + ")";

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

		// States in which a document's most recent revision hasn't finished the async quarantine/
		// validation pipeline yet. Pushing on top of one of these would race the in-flight validation.
		private static readonly string[] s_astrInFlightStates = { "quarantined", "processing" };

		private async void cmdPushCurrent_Click(object sender, EventArgs e)
		{
			if (_objActiveCharacter == null)
				return;

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
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_Error").Replace("{0}", ex.Message));
			}
		}

		private async void cmdPushShared_Click(object sender, EventArgs e)
		{
			if (_objActiveCharacter == null || lstDocuments.SelectedItems.Count == 0)
				return;
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

			if (!s_astrDownloadableStates.Contains(objDocument.ValidationState))
			{
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_NotDownloadable").Replace("{0}", objDocument.ValidationState));
				return;
			}

			SaveFileDialog objDialog = new SaveFileDialog();
			objDialog.Filter = "Chummer Files (*.chum)|*.chum|All Files (*.*)|*.*";
			objDialog.FileName = string.IsNullOrEmpty(objDocument.DisplayName) ? objDocument.Id : objDocument.DisplayName;
			if (objDialog.ShowDialog(this) != DialogResult.OK)
				return;

			try
			{
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_Downloading"));
				byte[] bytContent = objDocument is RunnersPointSharedDocument
					? await _objApiClient.DownloadSharedDocumentRevisionAsync(objDocument.Id, objDocument.CurrentRevision)
					: await _objApiClient.DownloadRevisionAsync(objDocument.Id, objDocument.CurrentRevision);
				File.WriteAllBytes(objDialog.FileName, bytContent);
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_Ready"));
			}
			catch (Exception ex)
			{
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_Error").Replace("{0}", ex.Message));
			}
		}

		private async void cmdArchive_Click(object sender, EventArgs e)
		{
			if (SharedMode || lstDocuments.SelectedItems.Count == 0)
				return;
			RunnersPointDocument objDocument = (RunnersPointDocument)lstDocuments.SelectedItems[0].Tag;

			if (MessageBox.Show(LanguageManager.Instance.GetString("Message_Cloud_ConfirmArchive").Replace("{0}", objDocument.DisplayName ?? objDocument.Id),
				LanguageManager.Instance.GetString("MessageTitle_Cloud_ConfirmArchive"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
				return;

			try
			{
				Tuple<RunnersPointDocument, string> objCurrent = await _objApiClient.GetDocumentAsync(objDocument.Id);
				await _objApiClient.ArchiveDocumentAsync(objDocument.Id, objCurrent.Item2);
				await RefreshAsync();
			}
			catch (Exception ex)
			{
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_Error").Replace("{0}", ex.Message));
			}
		}

		private void lstDocuments_SelectedIndexChanged(object sender, EventArgs e)
		{
			UpdateSelectionButtons();
		}

		private void UpdateSelectionButtons()
		{
			if (lstDocuments.SelectedItems.Count == 0)
			{
				cmdDownload.Enabled = false;
				cmdArchive.Enabled = false;
				cmdPushShared.Enabled = false;
				return;
			}

			object objTag = lstDocuments.SelectedItems[0].Tag;
			cmdDownload.Enabled = true;
			cmdArchive.Enabled = !SharedMode;
			cmdPushShared.Enabled = SharedMode && _objActiveCharacter != null && objTag is RunnersPointSharedDocument objShared
				&& objShared.Permission == "write" && objShared.ShareStatus == "active";
		}

		private void UpdateStatus(string strText)
		{
			lblStatus.Text = strText;
		}
	}
}
