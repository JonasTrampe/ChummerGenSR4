using System;
using System.Windows.Forms;

namespace Chummer
{
	/// <summary>
	/// Edits a character's RunnersPoint cloud document metadata (displayName/description/imageUrl, per
	/// the API's Document.metadata schema). Always saves locally on the character; the caller
	/// (frmCloudDocuments) is responsible for also pushing the values to the server via
	/// PATCH /documents/{documentId} when the character is already linked to a cloud document - this
	/// dialog itself has no server/API access and doesn't attempt that.
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
			string strImageUrl = txtImageUrl.Text.Trim();
			// Matches the server's own validation (DocumentMetadataPatch.imageUrl requires https://) -
			// catching this here avoids an avoidable round trip to find out.
			if (!string.IsNullOrEmpty(strImageUrl) && !strImageUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_CloudMetadata_ImageUrlMustBeHttps"),
					LanguageManager.Instance.GetString("MessageTitle_CloudMetadata_ImageUrlMustBeHttps"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}

			_objCharacter.CloudMetadataDisplayName = txtDisplayName.Text.Trim();
			_objCharacter.CloudMetadataDescription = txtDescription.Text.Trim();
			_objCharacter.CloudMetadataImageUrl = strImageUrl;

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
