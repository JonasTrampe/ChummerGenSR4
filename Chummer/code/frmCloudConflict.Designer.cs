namespace Chummer
{
	partial class frmCloudConflict
	{
		private System.ComponentModel.IContainer components = null;

		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		private void InitializeComponent()
		{
			this.lblExplanation = new System.Windows.Forms.Label();
			this.lstDiff = new System.Windows.Forms.ListView();
			this.colCollection = new System.Windows.Forms.ColumnHeader();
			this.colChange = new System.Windows.Forms.ColumnHeader();
			this.colDetail = new System.Windows.Forms.ColumnHeader();
			this.cmdOverwriteServer = new System.Windows.Forms.Button();
			this.cmdSaveLocallyOnly = new System.Windows.Forms.Button();
			this.cmdCancel = new System.Windows.Forms.Button();
			this.SuspendLayout();
			//
			// lblExplanation
			//
			this.lblExplanation.AutoSize = true;
			this.lblExplanation.Location = new System.Drawing.Point(12, 9);
			this.lblExplanation.Name = "lblExplanation";
			this.lblExplanation.Size = new System.Drawing.Size(300, 13);
			this.lblExplanation.TabIndex = 0;
			this.lblExplanation.Tag = "Label_CloudConflict_Explanation";
			this.lblExplanation.Text = "A newer revision exists on the server. What was different:";
			//
			// lstDiff
			//
			this.lstDiff.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
				this.colCollection,
				this.colChange,
				this.colDetail});
			this.lstDiff.View = System.Windows.Forms.View.Details;
			this.lstDiff.FullRowSelect = true;
			this.lstDiff.Location = new System.Drawing.Point(12, 30);
			this.lstDiff.Name = "lstDiff";
			this.lstDiff.Size = new System.Drawing.Size(600, 300);
			this.lstDiff.TabIndex = 1;
			this.lstDiff.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
				| System.Windows.Forms.AnchorStyles.Left)
				| System.Windows.Forms.AnchorStyles.Right)));
			//
			// colCollection
			//
			this.colCollection.Text = "Category";
			this.colCollection.Width = 110;
			//
			// colChange
			//
			this.colChange.Text = "Change";
			this.colChange.Width = 220;
			//
			// colDetail
			//
			this.colDetail.Text = "Detail";
			this.colDetail.Width = 250;
			//
			// cmdOverwriteServer
			//
			this.cmdOverwriteServer.Location = new System.Drawing.Point(12, 340);
			this.cmdOverwriteServer.Name = "cmdOverwriteServer";
			this.cmdOverwriteServer.Size = new System.Drawing.Size(160, 27);
			this.cmdOverwriteServer.TabIndex = 2;
			this.cmdOverwriteServer.Tag = "Button_CloudConflict_OverwriteServer";
			this.cmdOverwriteServer.Text = "Overwrite Server";
			this.cmdOverwriteServer.UseVisualStyleBackColor = true;
			this.cmdOverwriteServer.Click += new System.EventHandler(this.cmdOverwriteServer_Click);
			//
			// cmdSaveLocallyOnly
			//
			this.cmdSaveLocallyOnly.Location = new System.Drawing.Point(178, 340);
			this.cmdSaveLocallyOnly.Name = "cmdSaveLocallyOnly";
			this.cmdSaveLocallyOnly.Size = new System.Drawing.Size(160, 27);
			this.cmdSaveLocallyOnly.TabIndex = 3;
			this.cmdSaveLocallyOnly.Tag = "Button_CloudConflict_SaveLocallyOnly";
			this.cmdSaveLocallyOnly.Text = "Save Locally Only";
			this.cmdSaveLocallyOnly.UseVisualStyleBackColor = true;
			this.cmdSaveLocallyOnly.Click += new System.EventHandler(this.cmdSaveLocallyOnly_Click);
			//
			// cmdCancel
			//
			this.cmdCancel.Location = new System.Drawing.Point(537, 340);
			this.cmdCancel.Name = "cmdCancel";
			this.cmdCancel.Size = new System.Drawing.Size(75, 27);
			this.cmdCancel.TabIndex = 4;
			this.cmdCancel.Tag = "Button_CloudConflict_Cancel";
			this.cmdCancel.Text = "Cancel";
			this.cmdCancel.UseVisualStyleBackColor = true;
			this.cmdCancel.Click += new System.EventHandler(this.cmdCancel_Click);
			this.cmdCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			//
			// frmCloudConflict
			//
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(624, 379);
			this.Controls.Add(this.cmdCancel);
			this.Controls.Add(this.cmdSaveLocallyOnly);
			this.Controls.Add(this.cmdOverwriteServer);
			this.Controls.Add(this.lstDiff);
			this.Controls.Add(this.lblExplanation);
			this.MinimumSize = new System.Drawing.Size(500, 300);
			this.Name = "frmCloudConflict";
			this.Tag = "Title_CloudConflict";
			this.Text = "Cloud Save Conflict";
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.ResumeLayout(false);
			this.PerformLayout();
		}

		private System.Windows.Forms.Label lblExplanation;
		private System.Windows.Forms.ListView lstDiff;
		private System.Windows.Forms.ColumnHeader colCollection;
		private System.Windows.Forms.ColumnHeader colChange;
		private System.Windows.Forms.ColumnHeader colDetail;
		private System.Windows.Forms.Button cmdOverwriteServer;
		private System.Windows.Forms.Button cmdSaveLocallyOnly;
		private System.Windows.Forms.Button cmdCancel;
	}
}
