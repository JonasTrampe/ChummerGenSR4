using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows.Forms;
using Serilog;

namespace Chummer
{
	/// <summary>
	/// Lists a cloud document's revision history and lets the caller download any past revision, or
	/// (where the API allows it) permanently purge a single revision or the whole document. Purge is
	/// owner-only for a document's own revisions; on a shared document it further requires the active
	/// grant to carry the "purge" permission specifically (per the API's independent write/update/purge
	/// grant model) - blnCanPurge tells this dialog which case it's in rather than re-deriving it.
	/// </summary>
	public partial class frmCloudRevisions : Form
	{
		private readonly RunnersPointApiClient _objApiClient;
		private readonly RunnersPointDocument _objDocument;
		private readonly bool _blnShared;
		private readonly bool _blnCanPurge;

		public frmCloudRevisions(RunnersPointApiClient objApiClient, RunnersPointDocument objDocument, bool blnShared, bool blnCanPurge)
		{
			_objApiClient = objApiClient;
			_objDocument = objDocument;
			_blnShared = blnShared;
			_blnCanPurge = blnCanPurge;
			InitializeComponent();
			LanguageManager.Instance.Load(GlobalOptions.Instance.Language, this);
		}

		private async void frmCloudRevisions_Load(object sender, EventArgs e)
		{
			Text = LanguageManager.Instance.GetString("Title_CloudRevisions").Replace("{0}",
				string.IsNullOrEmpty(_objDocument.DisplayName) ? _objDocument.Id : _objDocument.DisplayName);
			// Purging the whole document only makes sense for the owner's own copy (never through shared
			// access unless the grant specifically carries "purge"), and only once it's archived - the
			// server enforces the same precondition, but checking it client-side avoids a pointless
			// round trip to find out.
			cmdPurgeDocument.Visible = _blnCanPurge;
			cmdPurgeDocument.Enabled = _blnCanPurge && _objDocument.ValidationState == "archived";
			ResetKarmaDisplay();
			await RefreshAsync();
		}

		private async System.Threading.Tasks.Task RefreshAsync()
		{
			try
			{
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_Refreshing"));
				lstRevisions.Items.Clear();
				System.Collections.Generic.List<RunnersPointRevision> lstFetchedRevisions = _blnShared
					? await _objApiClient.ListSharedRevisionsAsync(_objDocument.Id)
					: await _objApiClient.ListRevisionsAsync(_objDocument.Id);

				foreach (RunnersPointRevision objRevision in lstFetchedRevisions)
				{
					ListViewItem objItem = new ListViewItem(new[]
					{
						objRevision.CreatedAt.ToLocalTime().ToString("g"),
						objRevision.ValidationState,
						objRevision.SizeBytes.ToString(),
						objRevision.Id == _objDocument.CurrentRevision ? LanguageManager.Instance.GetString("String_CloudRevisions_Current") : "",
					});
					objItem.Tag = objRevision;
					lstRevisions.Items.Add(objItem);
				}

				UpdateSelectionButtons();
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_Ready"));
			}
			catch (RunnersPointApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
			{
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_AuthExpired"));
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Cloud Documents revisions operation failed");
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_Error").Replace("{0}", ex.Message));
			}
		}

		private void lstRevisions_SelectedIndexChanged(object sender, EventArgs e)
		{
			UpdateSelectionButtons();
			// Karma is not part of the API's Document/Revision schema - it can only come from actually
			// downloading and parsing that revision's content, which cmdFetchKarma_Click does on demand.
			// A newly-selected revision hasn't had that done yet, so clear out whatever the previous
			// selection showed rather than leaving a stale value next to the wrong row.
			ResetKarmaDisplay();
		}

		private void ResetKarmaDisplay()
		{
			pgbKarma.Value = 0;
			lblKarmaValue.Text = "";
			cmdFetchKarma.Enabled = lstRevisions.SelectedItems.Count > 0;
		}

		private void UpdateSelectionButtons()
		{
			bool blnSelected = lstRevisions.SelectedItems.Count > 0;
			cmdDownload.Enabled = blnSelected;
			cmdPurgeRevision.Enabled = blnSelected && _blnCanPurge;
		}

		/// <summary>
		/// Downloads the selected revision and reads Karma/CareerKarma out of its .chum content by
		/// loading it as a throwaway Character - Karma isn't exposed anywhere in the API itself, so this
		/// is the only way to show it. Deliberately on-demand (via this button) rather than automatic on
		/// selection, since it means a full download for every row the user merely glances at.
		/// </summary>
		private async void cmdFetchKarma_Click(object sender, EventArgs e)
		{
			if (lstRevisions.SelectedItems.Count == 0)
				return;
			RunnersPointRevision objRevision = (RunnersPointRevision)lstRevisions.SelectedItems[0].Tag;

			Tuple<byte[], string> objDownload;
			try
			{
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_Downloading"));
				objDownload = _blnShared
					? await _objApiClient.DownloadSharedDocumentRevisionAsync(_objDocument.Id, objRevision.Id)
					: await _objApiClient.DownloadRevisionAsync(_objDocument.Id, objRevision.Id);
			}
			catch (RunnersPointApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
			{
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_AuthExpired"));
				return;
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Cloud Documents revisions operation failed");
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_Error").Replace("{0}", ex.Message));
				return;
			}

			// Character.Load() only reads from a file path, not a byte buffer - stage the downloaded
			// content in a throwaway temp file just long enough to load it, then discard both the file
			// and the Character. Nothing here is saved or linked back to any real character.
			string strTempFile = Path.GetTempFileName();
			try
			{
				File.WriteAllBytes(strTempFile, objDownload.Item1);
				Character objTemp = new Character { FileName = strTempFile };
				if (!objTemp.Load())
				{
					UpdateStatus(LanguageManager.Instance.GetString("String_CloudRevisions_KarmaUnreadable"));
					return;
				}

				int intKarma = objTemp.Karma;
				int intCareerKarma = objTemp.CareerKarma;
				// CareerKarma (total ever earned) is usually >= Karma (currently unspent) but a character
				// saved mid-creation may have 0 CareerKarma recorded yet - fall back to Karma itself so the
				// bar doesn't divide by (or clamp against) zero.
				int intMax = Math.Max(intCareerKarma, intKarma);
				pgbKarma.Maximum = Math.Max(intMax, 1);
				pgbKarma.Value = Math.Min(Math.Max(intKarma, 0), pgbKarma.Maximum);
				lblKarmaValue.Text = intCareerKarma > 0
					? intKarma + " / " + intCareerKarma
					: intKarma.ToString();
				cmdFetchKarma.Enabled = false;
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_Ready"));
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Cloud Documents revisions operation failed");
				UpdateStatus(LanguageManager.Instance.GetString("String_CloudRevisions_KarmaUnreadable"));
			}
			finally
			{
				try
				{
					File.Delete(strTempFile);
				}
				catch (IOException)
				{
					// Best-effort cleanup of a throwaway temp file - not worth surfacing to the user.
				}
			}
		}

		private async void cmdDownload_Click(object sender, EventArgs e)
		{
			if (lstRevisions.SelectedItems.Count == 0)
				return;
			RunnersPointRevision objRevision = (RunnersPointRevision)lstRevisions.SelectedItems[0].Tag;

			Tuple<byte[], string> objDownload;
			try
			{
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_Downloading"));
				objDownload = _blnShared
					? await _objApiClient.DownloadSharedDocumentRevisionAsync(_objDocument.Id, objRevision.Id)
					: await _objApiClient.DownloadRevisionAsync(_objDocument.Id, objRevision.Id);
			}
			catch (RunnersPointApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
			{
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_AuthExpired"));
				return;
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Cloud Documents revisions operation failed");
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_Error").Replace("{0}", ex.Message));
				return;
			}

			string strSuggestedFileName = !string.IsNullOrEmpty(objDownload.Item2)
				? objDownload.Item2
				: string.IsNullOrEmpty(_objDocument.DisplayName) ? _objDocument.Id : _objDocument.DisplayName;

			SaveFileDialog objDialog = new SaveFileDialog();
			objDialog.Filter = "Chummer Files (*.chum)|*.chum|All Files (*.*)|*.*";
			objDialog.FileName = strSuggestedFileName;
			if (objDialog.ShowDialog(this) != DialogResult.OK)
				return;

			try
			{
				File.WriteAllBytes(objDialog.FileName, objDownload.Item1);
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_Ready"));
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Cloud Documents revisions operation failed");
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_Error").Replace("{0}", ex.Message));
			}
		}

		/// <summary>
		/// Fetches the document's current ETag fresh, right before a purge call - both purge endpoints
		/// require If-Match, and the list snapshot this dialog opened with may already be stale.
		/// </summary>
		private async System.Threading.Tasks.Task<string> GetCurrentETagAsync()
		{
			return _blnShared
				? (await _objApiClient.GetSharedDocumentAsync(_objDocument.Id)).Item2
				: (await _objApiClient.GetDocumentAsync(_objDocument.Id)).Item2;
		}

		private async void cmdPurgeRevision_Click(object sender, EventArgs e)
		{
			if (lstRevisions.SelectedItems.Count == 0)
				return;
			RunnersPointRevision objRevision = (RunnersPointRevision)lstRevisions.SelectedItems[0].Tag;

			if (MessageBox.Show(LanguageManager.Instance.GetString("Message_CloudRevisions_ConfirmPurgeRevision").Replace("{0}", objRevision.CreatedAt.ToLocalTime().ToString("g")),
				LanguageManager.Instance.GetString("MessageTitle_CloudRevisions_ConfirmPurgeRevision"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
				return;

			try
			{
				UpdateStatus(LanguageManager.Instance.GetString("String_CloudRevisions_Purging"));
				string strIfMatch = await GetCurrentETagAsync();
				if (_blnShared)
					await _objApiClient.PurgeSharedRevisionAsync(_objDocument.Id, objRevision.Id, strIfMatch);
				else
					await _objApiClient.PurgeRevisionAsync(_objDocument.Id, objRevision.Id, strIfMatch);

				// The purged revision may have been currentRevision (rolling the document back to the next
				// most recent one, or to a null-currentRevision archived shell if it was the only one) -
				// re-fetch rather than trust the snapshot this dialog opened with.
				_objDocument.CurrentRevision = _blnShared
					? (await _objApiClient.GetSharedDocumentAsync(_objDocument.Id)).Item1.CurrentRevision
					: (await _objApiClient.GetDocumentAsync(_objDocument.Id)).Item1.CurrentRevision;

				await RefreshAsync();
			}
			catch (RunnersPointApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
			{
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_AuthExpired"));
			}
			catch (RunnersPointApiException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
			{
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_PushStale"));
			}
			catch (RunnersPointApiException ex) when (ex.ProblemCode == "recent_authentication_required")
			{
				UpdateStatus(LanguageManager.Instance.GetString("String_CloudRevisions_RecentAuthRequired"));
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Cloud Documents revisions operation failed");
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_Error").Replace("{0}", ex.Message));
			}
		}

		private async void cmdPurgeDocument_Click(object sender, EventArgs e)
		{
			if (MessageBox.Show(LanguageManager.Instance.GetString("Message_CloudRevisions_ConfirmPurgeDocument").Replace("{0}", _objDocument.DisplayName ?? _objDocument.Id),
				LanguageManager.Instance.GetString("MessageTitle_CloudRevisions_ConfirmPurgeDocument"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
				return;

			try
			{
				UpdateStatus(LanguageManager.Instance.GetString("String_CloudRevisions_Purging"));
				string strIfMatch = await GetCurrentETagAsync();
				if (_blnShared)
					await _objApiClient.PurgeSharedDocumentAsync(_objDocument.Id, strIfMatch);
				else
					await _objApiClient.PurgeDocumentAsync(_objDocument.Id, strIfMatch);

				// The document no longer exists - nothing left in this dialog to refresh or select.
				DialogResult = DialogResult.OK;
				Close();
			}
			catch (RunnersPointApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
			{
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_AuthExpired"));
			}
			catch (RunnersPointApiException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
			{
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_PushStale"));
			}
			catch (RunnersPointApiException ex) when (ex.ProblemCode == "purge_not_eligible")
			{
				UpdateStatus(LanguageManager.Instance.GetString("String_CloudRevisions_PurgeNotEligible"));
			}
			catch (RunnersPointApiException ex) when (ex.ProblemCode == "recent_authentication_required")
			{
				UpdateStatus(LanguageManager.Instance.GetString("String_CloudRevisions_RecentAuthRequired"));
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Cloud Documents revisions operation failed");
				UpdateStatus(LanguageManager.Instance.GetString("String_Cloud_Error").Replace("{0}", ex.Message));
			}
		}

		private void cmdClose_Click(object sender, EventArgs e)
		{
			Close();
		}

		private void UpdateStatus(string strText)
		{
			lblStatus.Text = strText;
		}
	}
}
