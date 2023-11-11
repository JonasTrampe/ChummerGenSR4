using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Chummer
{
    public partial class frmSelectQuantity : Form
    {
        public enum Operation
        {
            Add,
            Remove
        }

        #region Control Events
        public frmSelectQuantity(Operation op, int max)
        {
            InitializeComponent();

            Op = op;
            if (op == Operation.Add)
            {
                lblOp.Text = @"+";
                nudMarkup.Enabled = true;
                chkFreeItem.Enabled = true;
            }
            else
            {
                lblOp.Text = @"-";
                nudMarkup.Enabled = false;
                chkFreeItem.Enabled = false;
            }

            LanguageManager.Instance.Load(GlobalOptions.Instance.Language, this);

            MoveControls();

            nudQuantity.Maximum = max;
        }

        private void cmdOK_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.OK;
        }

        private void cmdCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
        }

        #endregion

        #region Methods
        private void MoveControls()
        {
            lblOp.Left = lblQuantityLabel.Left + lblQuantityLabel.Width + 6;
            nudQuantity.Left = lblOp.Left + lblOp.Width + 6;
            var widthQuantity = lblOp.Left + lblOp.Width + 30;

            chkFreeItem.Left = nudQuantity.Left;
            var widthCheck = chkFreeItem.Left + chkFreeItem.Width + 30;

            nudMarkup.Left = nudQuantity.Left;
            lblMarkupPercentLabel.Left = nudMarkup.Left + nudMarkup.Width + 6;
            var widthMarkup = lblMarkupPercentLabel.Left + lblMarkupPercentLabel.Width + 30;

            this.Width = new[] { widthQuantity, widthCheck, widthMarkup }.Max();
            if (this.Width < 195)
                this.Width = 195;
        }

        private void chkFreeItem_CheckedChanged(object sender, EventArgs e)
        {
            nudMarkup.Enabled = !chkFreeItem.Checked;
        }

        #endregion

        #region Properties
        /// <summary>
        /// How many items should be added/removed
        /// </summary>
        public int Quantity => Convert.ToInt32(nudQuantity.Value, GlobalOptions.Instance.CultureInfo);

        public Operation Op
        {
            get;
        }

        /// <summary>
        /// Whether or not the item should be added for free.
        /// </summary>
        public bool FreeCost => chkFreeItem.Checked;

        /// <summary>
        /// Markup percentage.
        /// </summary>
        public int Markup => Convert.ToInt32(nudMarkup.Value);

        #endregion
    }
}
