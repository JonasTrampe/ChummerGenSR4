using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Chummer
{
	/// <summary>
	/// Personal cloud storage/sync against the RunnersPoint Character Document Storage API. Replaces
	/// the old Omae online-sharing feature - sharing/discovery of other users' content is explicitly
	/// out of scope here (see docs/api/open-questions-character-document-storage-v1.md upstream), this
	/// is purely "back up and recover my own characters."
	/// </summary>
	public partial class frmCloudDocuments : Form
	{
		private readonly RunnersPointAuth _objAuth = new RunnersPointAuth();
		private RunnersPointApiClient _objApiClient;
		private readonly Character _objActiveCharacter;
		private string _strGameProfileId = "";
		private string _strGameProfileFormat = "";

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

		private void cmdLogout_Click(object sender, EventArgs e)
		{
			_objAuth.Logout();
			lstDocuments.Items.Clear();
			UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_NotLoggedIn"));
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
				do
				{
					RunnersPointDocumentPage objPage = await _objApiClient.ListDocumentsAsync(_strGameProfileId, strCursor);
					foreach (RunnersPointDocument objDocument in objPage.Items)
					{
						ListViewItem objItem = new ListViewItem(new[]
						{
							string.IsNullOrEmpty(objDocument.DisplayName) ? objDocument.Id : objDocument.DisplayName,
							objDocument.ValidationState,
							objDocument.UpdatedAt.ToString("g"),
						});
						objItem.Tag = objDocument;
						lstDocuments.Items.Add(objItem);
					}
					strCursor = objPage.NextCursor;
				} while (!string.IsNullOrEmpty(strCursor));

				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_Ready"));
			}
			catch (Exception ex)
			{
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_Error").Replace("{0}", ex.Message));
			}
		}

		private async void cmdRefresh_Click(object sender, EventArgs e)
		{
			await RefreshAsync();
		}

		// States in which a document's most recent revision hasn't finished the async quarantine/
		// validation pipeline yet. Pushing on top of one of these would race the in-flight validation.
		private static readonly string[] s_astrInFlightStates = { "quarantined", "scanning", "validating", "processing" };

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

		// Whether downloadRevision is expected to work for a given document.validationState. The API
		// doesn't currently document this (see open-questions doc: "whether warning-state revisions
		// are downloadable" is unresolved) - "valid" and "warning" are the conservative assumption;
		// "processing"/"rejected"/"archived" are blocked client-side with an explanation instead of
		// just letting the download call fail with an unclear server error.
		private static readonly string[] s_astrDownloadableStates = { "valid", "warning" };

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
				byte[] bytContent = await _objApiClient.DownloadRevisionAsync(objDocument.Id, objDocument.CurrentRevision);
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
			if (lstDocuments.SelectedItems.Count == 0)
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

		private void UpdateStatus(string strText)
		{
			lblStatus.Text = strText;
		}
	}
}
