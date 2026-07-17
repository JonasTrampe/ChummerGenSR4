namespace Chummer
{
	partial class frmCloudDocuments
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
			this.lstDocuments = new System.Windows.Forms.ListView();
			this.colName = new System.Windows.Forms.ColumnHeader();
			this.colState = new System.Windows.Forms.ColumnHeader();
			this.colUpdated = new System.Windows.Forms.ColumnHeader();
			this.cmdLogin = new System.Windows.Forms.Button();
			this.cmdLogout = new System.Windows.Forms.Button();
			this.cmdRefresh = new System.Windows.Forms.Button();
			this.cmdPushCurrent = new System.Windows.Forms.Button();
			this.cmdDownload = new System.Windows.Forms.Button();
			this.cmdArchive = new System.Windows.Forms.Button();
			this.lblStatus = new System.Windows.Forms.Label();
			this.SuspendLayout();
			//
			// lstDocuments
			//
			this.lstDocuments.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
				this.colName,
				this.colState,
				this.colUpdated});
			this.lstDocuments.View = System.Windows.Forms.View.Details;
			this.lstDocuments.FullRowSelect = true;
			this.lstDocuments.MultiSelect = false;
			this.lstDocuments.HideSelection = false;
			this.lstDocuments.Location = new System.Drawing.Point(12, 12);
			this.lstDocuments.Name = "lstDocuments";
			this.lstDocuments.Size = new System.Drawing.Size(560, 300);
			this.lstDocuments.TabIndex = 0;
			this.lstDocuments.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
				| System.Windows.Forms.AnchorStyles.Left)
				| System.Windows.Forms.AnchorStyles.Right)));
			//
			// colName
			//
			this.colName.Text = "Name";
			this.colName.Width = 280;
			//
			// colState
			//
			this.colState.Text = "Validation State";
			this.colState.Width = 120;
			//
			// colUpdated
			//
			this.colUpdated.Text = "Updated";
			this.colUpdated.Width = 140;
			//
			// cmdLogin
			//
			this.cmdLogin.Location = new System.Drawing.Point(12, 320);
			this.cmdLogin.Name = "cmdLogin";
			this.cmdLogin.Size = new System.Drawing.Size(90, 27);
			this.cmdLogin.TabIndex = 1;
			this.cmdLogin.Tag = "Button_Cloud_Login";
			this.cmdLogin.Text = "Log In";
			this.cmdLogin.UseVisualStyleBackColor = true;
			this.cmdLogin.Click += new System.EventHandler(this.cmdLogin_Click);
			//
			// cmdLogout
			//
			this.cmdLogout.Location = new System.Drawing.Point(108, 320);
			this.cmdLogout.Name = "cmdLogout";
			this.cmdLogout.Size = new System.Drawing.Size(90, 27);
			this.cmdLogout.TabIndex = 2;
			this.cmdLogout.Tag = "Button_Cloud_Logout";
			this.cmdLogout.Text = "Log Out";
			this.cmdLogout.UseVisualStyleBackColor = true;
			this.cmdLogout.Click += new System.EventHandler(this.cmdLogout_Click);
			//
			// cmdRefresh
			//
			this.cmdRefresh.Location = new System.Drawing.Point(204, 320);
			this.cmdRefresh.Name = "cmdRefresh";
			this.cmdRefresh.Size = new System.Drawing.Size(90, 27);
			this.cmdRefresh.TabIndex = 3;
			this.cmdRefresh.Tag = "Button_Cloud_Refresh";
			this.cmdRefresh.Text = "Refresh";
			this.cmdRefresh.UseVisualStyleBackColor = true;
			this.cmdRefresh.Click += new System.EventHandler(this.cmdRefresh_Click);
			//
			// cmdPushCurrent
			//
			this.cmdPushCurrent.Location = new System.Drawing.Point(300, 320);
			this.cmdPushCurrent.Name = "cmdPushCurrent";
			this.cmdPushCurrent.Size = new System.Drawing.Size(150, 27);
			this.cmdPushCurrent.TabIndex = 4;
			this.cmdPushCurrent.Tag = "Button_Cloud_PushCurrent";
			this.cmdPushCurrent.Text = "Push Current Character";
			this.cmdPushCurrent.UseVisualStyleBackColor = true;
			this.cmdPushCurrent.Click += new System.EventHandler(this.cmdPushCurrent_Click);
			//
			// cmdDownload
			//
			this.cmdDownload.Location = new System.Drawing.Point(456, 320);
			this.cmdDownload.Name = "cmdDownload";
			this.cmdDownload.Size = new System.Drawing.Size(115, 27);
			this.cmdDownload.TabIndex = 5;
			this.cmdDownload.Tag = "Button_Cloud_Download";
			this.cmdDownload.Text = "Download Selected";
			this.cmdDownload.UseVisualStyleBackColor = true;
			this.cmdDownload.Click += new System.EventHandler(this.cmdDownload_Click);
			//
			// cmdArchive
			//
			this.cmdArchive.Location = new System.Drawing.Point(12, 354);
			this.cmdArchive.Name = "cmdArchive";
			this.cmdArchive.Size = new System.Drawing.Size(115, 27);
			this.cmdArchive.TabIndex = 6;
			this.cmdArchive.Tag = "Button_Cloud_Archive";
			this.cmdArchive.Text = "Archive Selected";
			this.cmdArchive.UseVisualStyleBackColor = true;
			this.cmdArchive.Click += new System.EventHandler(this.cmdArchive_Click);
			//
			// lblStatus
			//
			this.lblStatus.AutoSize = true;
			this.lblStatus.Location = new System.Drawing.Point(12, 390);
			this.lblStatus.Name = "lblStatus";
			this.lblStatus.Size = new System.Drawing.Size(38, 13);
			this.lblStatus.TabIndex = 7;
			this.lblStatus.Text = "";
			this.lblStatus.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			//
			// frmCloudDocuments
			//
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(584, 421);
			this.Controls.Add(this.lblStatus);
			this.Controls.Add(this.cmdArchive);
			this.Controls.Add(this.cmdDownload);
			this.Controls.Add(this.cmdPushCurrent);
			this.Controls.Add(this.cmdRefresh);
			this.Controls.Add(this.cmdLogout);
			this.Controls.Add(this.cmdLogin);
			this.Controls.Add(this.lstDocuments);
			this.MinimumSize = new System.Drawing.Size(500, 350);
			this.Name = "frmCloudDocuments";
			this.Tag = "Title_CloudDocuments";
			this.Text = "Cloud Documents";
			this.Load += new System.EventHandler(this.frmCloudDocuments_Load);
			this.ResumeLayout(false);
			this.PerformLayout();
		}

		private System.Windows.Forms.ListView lstDocuments;
		private System.Windows.Forms.ColumnHeader colName;
		private System.Windows.Forms.ColumnHeader colState;
		private System.Windows.Forms.ColumnHeader colUpdated;
		private System.Windows.Forms.Button cmdLogin;
		private System.Windows.Forms.Button cmdLogout;
		private System.Windows.Forms.Button cmdRefresh;
		private System.Windows.Forms.Button cmdPushCurrent;
		private System.Windows.Forms.Button cmdDownload;
		private System.Windows.Forms.Button cmdArchive;
		private System.Windows.Forms.Label lblStatus;
	}
}
