using System.Windows.Forms;

namespace Chummer
{
	/// <summary>
	/// Shown when a Save's push to RunnersPoint hits a 412 (the server has a newer revision than this
	/// session knew about) - lists what differs between the character being saved and the server's
	/// current revision (see CharacterDiff), and lets the user decide whether their local edits should
	/// still win.
	/// </summary>
	public partial class frmCloudConflict : Form
	{
		public enum ConflictChoice
		{
			Cancel,
			SaveLocallyOnly,
			OverwriteServer,
		}

		public ConflictChoice Choice { get; private set; } = ConflictChoice.Cancel;

		public frmCloudConflict(CharacterDiffResult objDiff)
		{
			InitializeComponent();
			LanguageManager.Instance.Load(GlobalOptions.Instance.Language, this);

			if (!objDiff.HasChanges)
			{
				lstDiff.Items.Add(new ListViewItem(new[] { "", "", LanguageManager.Instance.GetString("String_CloudConflict_NoDetectableDifference") }));
				return;
			}

			foreach (CharacterDiffEntry objEntry in objDiff.Entries)
			{
				string strCollection = string.IsNullOrEmpty(objEntry.Collection) ? "" : objEntry.Collection;
				lstDiff.Items.Add(new ListViewItem(new[] { strCollection, objEntry.Change + " " + objEntry.Name, objEntry.Detail }));
			}
		}

		private void cmdOverwriteServer_Click(object sender, System.EventArgs e)
		{
			Choice = ConflictChoice.OverwriteServer;
			DialogResult = DialogResult.OK;
			Close();
		}

		private void cmdSaveLocallyOnly_Click(object sender, System.EventArgs e)
		{
			Choice = ConflictChoice.SaveLocallyOnly;
			DialogResult = DialogResult.OK;
			Close();
		}

		private void cmdCancel_Click(object sender, System.EventArgs e)
		{
			Choice = ConflictChoice.Cancel;
			DialogResult = DialogResult.Cancel;
			Close();
		}
	}
}
