using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Chummer
{
	public class frmSelectWeaponCategories : Form
	{
		private readonly CheckedListBox _clbCategories = new CheckedListBox();

		public frmSelectWeaponCategories(IEnumerable<ListItem> lstCategories, ICollection<string> setSelectedCategories)
		{
			Text = LanguageManager.Instance.GetString("Title_SelectWeaponCategories");
			StartPosition = FormStartPosition.CenterParent;
			MinimizeBox = false;
			MaximizeBox = false;
			ShowInTaskbar = false;
			ClientSize = new Size(380, 440);

			_clbCategories.CheckOnClick = true;
			_clbCategories.DisplayMember = "Name";
			_clbCategories.ValueMember = "Value";
			_clbCategories.Location = new Point(12, 12);
			_clbCategories.Size = new Size(356, 380);
			_clbCategories.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
			foreach (ListItem objItem in lstCategories)
				_clbCategories.Items.Add(objItem, setSelectedCategories.Contains(objItem.Value));
			Controls.Add(_clbCategories);

			Button cmdOK = new Button();
			cmdOK.Text = LanguageManager.Instance.GetString("String_OK");
			cmdOK.DialogResult = DialogResult.OK;
			cmdOK.Location = new Point(212, 405);
			cmdOK.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
			Controls.Add(cmdOK);
			AcceptButton = cmdOK;

			Button cmdCancel = new Button();
			cmdCancel.Text = LanguageManager.Instance.GetString("String_Cancel");
			cmdCancel.DialogResult = DialogResult.Cancel;
			cmdCancel.Location = new Point(293, 405);
			cmdCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
			Controls.Add(cmdCancel);
			CancelButton = cmdCancel;
		}

		public IEnumerable<string> SelectedCategories
		{
			get
			{
				foreach (ListItem objItem in _clbCategories.CheckedItems)
					yield return objItem.Value;
			}
		}
	}
}
