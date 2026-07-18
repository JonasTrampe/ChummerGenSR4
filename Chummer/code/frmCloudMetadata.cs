using System;
using System.Windows.Forms;

namespace Chummer
{
	/// <summary>
	/// Edits a character's RunnersPoint cloud document metadata (displayName/description/imageUrl, per
	/// the API's Document.metadata schema). Local-only for now - the API has no way for a client to
	/// submit metadata at all (create/pushRevision take only raw file bytes), so nothing here is sent
	/// anywhere yet. Staged for when/if the server adds that; stored in the character file either way so
	/// the values aren't lost in the meantime.
	/// </summary>
	public partial class frmCloudMetadata : Form
	{
		private readonly Character _objCharacter;

		public frmCloudMetadata(Character objCharacter)
		{
			_objCharacter = objCharacter;
			InitializeComponent();
			LanguageManager.Instance.Load(GlobalOptions.Instance.Language, this);
		}

		private void frmCloudMetadata_Load(object sender, EventArgs e)
		{
			txtDisplayName.Text = _objCharacter.CloudMetadataDisplayName;
			txtDescription.Text = _objCharacter.CloudMetadataDescription;
			txtImageUrl.Text = _objCharacter.CloudMetadataImageUrl;
		}

		private void cmdOK_Click(object sender, EventArgs e)
		{
			_objCharacter.CloudMetadataDisplayName = txtDisplayName.Text.Trim();
			_objCharacter.CloudMetadataDescription = txtDescription.Text.Trim();
			_objCharacter.CloudMetadataImageUrl = txtImageUrl.Text.Trim();

			if (!string.IsNullOrEmpty(_objCharacter.FileName))
				_objCharacter.Save();

			DialogResult = DialogResult.OK;
			Close();
		}

		private void cmdCancel_Click(object sender, EventArgs e)
		{
			DialogResult = DialogResult.Cancel;
			Close();
		}
	}
}
