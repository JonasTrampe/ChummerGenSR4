using System.Drawing;
using System.Windows.Forms;

namespace Chummer
{
	/// <summary>
	/// Small generic prompt with custom-labeled buttons (2 or 3) - used by the cloud-load freshness
	/// check in frmMain.LoadCharacter, where the choices ("Abort"/"Open Local File", or "Download
	/// Newer"/"Open Local Copy"/"Cancel") don't map onto any of MessageBox's fixed button sets.
	/// Built entirely in code rather than via the designer since it's just a label and a button row.
	/// </summary>
	public class frmCloudLoadPrompt : Form
	{
		public int ChosenIndex { get; private set; } = -1;

		public frmCloudLoadPrompt(string strTitle, string strMessage, params string[] astrButtonLabels)
		{
			Text = strTitle;
			FormBorderStyle = FormBorderStyle.FixedDialog;
			MaximizeBox = false;
			MinimizeBox = false;
			StartPosition = FormStartPosition.CenterParent;
			ShowInTaskbar = false;
			AutoSize = false;
			ClientSize = new Size(420, 140);

			Label lblMessage = new Label
			{
				Text = strMessage,
				Location = new Point(12, 12),
				Size = new Size(396, 70),
				AutoSize = false,
			};
			Controls.Add(lblMessage);

			int intButtonWidth = 120;
			int intSpacing = 10;
			int intTotalWidth = astrButtonLabels.Length * intButtonWidth + (astrButtonLabels.Length - 1) * intSpacing;
			int intStartX = ClientSize.Width - 12 - intTotalWidth;

			for (int i = 0; i < astrButtonLabels.Length; i++)
			{
				int intIndex = i;
				Button objButton = new Button
				{
					Text = astrButtonLabels[i],
					Location = new Point(intStartX + i * (intButtonWidth + intSpacing), 95),
					Size = new Size(intButtonWidth, 27),
				};
				objButton.Click += (sender, e) =>
				{
					ChosenIndex = intIndex;
					DialogResult = DialogResult.OK;
					Close();
				};
				Controls.Add(objButton);
				if (i == 0)
					AcceptButton = objButton;
			}
		}
	}
}
