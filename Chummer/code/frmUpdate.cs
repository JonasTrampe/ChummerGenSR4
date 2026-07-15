using System;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace Chummer
{
	public partial class frmUpdate : Form
	{
		private const string GitHubLatestReleaseApiUrl = "https://api.github.com/repos/JonasTrampe/ChummerGenSR4/releases/latest";

		private bool _blnSilentMode = false;
		private string _strReleaseHtmlUrl = "";

		#region Control Methods
		public frmUpdate()
		{
			InitializeComponent();
			LanguageManager.Instance.Load(GlobalOptions.Instance.Language, this);
		}

		private void frmUpdate_Load(object sender, EventArgs e)
		{
			// Count the number of instances of Chummer that are currently running.
			string strFileName = Process.GetCurrentProcess().MainModule.FileName;
			int intCount = 0;
			foreach (Process objProcess in Process.GetProcesses())
			{
				try
				{
					if (objProcess.MainModule.FileName == strFileName)
						intCount++;
				}
				catch
				{
				}
			}

			// If there is more than 1 instance running, do not let the application be updated.
			if (intCount > 1)
			{
				if (!_blnSilentMode)
					MessageBox.Show(LanguageManager.Instance.GetString("Message_Update_MultipleInstances"), LanguageManager.Instance.GetString("Title_Update"), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				this.Close();
				return;
			}

			CheckForUpdate();
		}

		private void cmdDownload_Click(object sender, EventArgs e)
		{
			if (string.IsNullOrEmpty(_strReleaseHtmlUrl))
				return;

			// Opens the GitHub release page in the user's default browser rather than attempting to
			// download/self-replace the running exe in place - the old per-file WCF-free replacement
			// for that (see git history) was Windows-only (renaming the locked running exe) and
			// doesn't translate to how Linux/AppImage packaging will actually deliver updates.
			Process.Start(new ProcessStartInfo(_strReleaseHtmlUrl) { UseShellExecute = true });
		}

		private void cmdClose_Click(object sender, EventArgs e)
		{
			this.Close();
		}
		#endregion

		#region Custom Methods
		/// <summary>
		/// When running in silent mode, the update window is only shown if a newer release is
		/// actually found - otherwise it closes quietly instead of announcing "you're up to date".
		/// </summary>
		public bool SilentMode
		{
			get
			{
				return _blnSilentMode;
			}
			set
			{
				_blnSilentMode = value;
			}
		}

		/// <summary>
		/// Check GitHub's Releases API for a newer version than the one currently running.
		/// </summary>
		private void CheckForUpdate()
		{
			string strCurrentVersion = Application.ProductVersion;
			lblCurrentVersion.Text = LanguageManager.Instance.GetString("Label_Update_CurrentVersion") + " " + strCurrentVersion;

			string strJson;
			try
			{
				WebClient wc = new WebClient();
				// GitHub's API rejects requests with no User-Agent header.
				wc.Headers.Add("User-Agent", "ChummerGenSR4-UpdateCheck");
				wc.Encoding = Encoding.UTF8;
				strJson = wc.DownloadString(GitHubLatestReleaseApiUrl);
			}
			catch
			{
				if (!_blnSilentMode)
					MessageBox.Show(LanguageManager.Instance.GetString("Message_Update_CannotConnect"), LanguageManager.Instance.GetString("Title_Update"), MessageBoxButtons.OK, MessageBoxIcon.Error);
				this.Close();
				return;
			}

			JavaScriptSerializer objSerializer = new JavaScriptSerializer();
			System.Collections.Generic.Dictionary<string, object> objRelease;
			try
			{
				objRelease = (System.Collections.Generic.Dictionary<string, object>)objSerializer.DeserializeObject(strJson);
			}
			catch
			{
				if (!_blnSilentMode)
					MessageBox.Show(LanguageManager.Instance.GetString("Message_Update_CannotConnect"), LanguageManager.Instance.GetString("Title_Update"), MessageBoxButtons.OK, MessageBoxIcon.Error);
				this.Close();
				return;
			}

			string strTagName = objRelease.ContainsKey("tag_name") ? objRelease["tag_name"] as string : null;
			if (string.IsNullOrEmpty(strTagName))
			{
				if (!_blnSilentMode)
					MessageBox.Show(LanguageManager.Instance.GetString("Message_Update_CannotConnect"), LanguageManager.Instance.GetString("Title_Update"), MessageBoxButtons.OK, MessageBoxIcon.Error);
				this.Close();
				return;
			}

			Version objLatest = ParseReleaseVersion(strTagName);
			Version objCurrent = ParseReleaseVersion(strCurrentVersion);

			if (objLatest == null || objCurrent == null || objLatest <= objCurrent)
			{
				if (!_blnSilentMode)
					MessageBox.Show(LanguageManager.Instance.GetString("Message_Update_NoNewUpdates"), LanguageManager.Instance.GetString("Title_Update"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				this.Close();
				return;
			}

			// A newer release exists - populate the dialog and show it, even in silent mode.
			_strReleaseHtmlUrl = objRelease.ContainsKey("html_url") ? objRelease["html_url"] as string : "https://github.com/JonasTrampe/ChummerGenSR4/releases/latest";
			lblLatestVersion.Text = LanguageManager.Instance.GetString("Label_Update_LatestVersion") + " " + strTagName;
			txtReleaseNotes.Text = objRelease.ContainsKey("body") ? (objRelease["body"] as string ?? "") : "";

			this.Show();
		}

		/// <summary>
		/// Parses a version string that may have a leading "v" and/or a pre-release or build-metadata
		/// suffix (e.g. "v0.1.501-beta+abc123") down to the plain Major.Minor.Build form used to
		/// compare a release tag against Application.ProductVersion.
		/// </summary>
		private static Version ParseReleaseVersion(string strVersion)
		{
			string strTrimmed = strVersion.TrimStart('v', 'V');
			int intSuffixIndex = strTrimmed.IndexOfAny(new[] { '-', '+' });
			if (intSuffixIndex >= 0)
				strTrimmed = strTrimmed.Substring(0, intSuffixIndex);

			try
			{
				Version objParsed = new Version(strTrimmed);
				return new Version(objParsed.Major, objParsed.Minor, Math.Max(objParsed.Build, 0));
			}
			catch
			{
				return null;
			}
		}
		#endregion
	}
}
