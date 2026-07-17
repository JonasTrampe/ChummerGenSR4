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
			this.rdoMyDocuments = new System.Windows.Forms.RadioButton();
			this.rdoSharedWithMe = new System.Windows.Forms.RadioButton();
			this.lblApiToken = new System.Windows.Forms.Label();
			this.txtApiToken = new System.Windows.Forms.TextBox();
			this.cmdUseApiToken = new System.Windows.Forms.Button();
			this.lstDocuments = new System.Windows.Forms.ListView();
			this.colName = new System.Windows.Forms.ColumnHeader();
			this.colState = new System.Windows.Forms.ColumnHeader();
			this.colUpdated = new System.Windows.Forms.ColumnHeader();
			this.colShare = new System.Windows.Forms.ColumnHeader();
			this.cmdLogin = new System.Windows.Forms.Button();
			this.cmdLogout = new System.Windows.Forms.Button();
			this.cmdRefresh = new System.Windows.Forms.Button();
			this.cmdPushCurrent = new System.Windows.Forms.Button();
			this.cmdDownload = new System.Windows.Forms.Button();
			this.cmdArchive = new System.Windows.Forms.Button();
			this.cmdPushShared = new System.Windows.Forms.Button();
			this.lblStatus = new System.Windows.Forms.Label();
			this.SuspendLayout();
			//
			// rdoMyDocuments
			//
			this.rdoMyDocuments.Location = new System.Drawing.Point(12, 12);
			this.rdoMyDocuments.Name = "rdoMyDocuments";
			this.rdoMyDocuments.Size = new System.Drawing.Size(140, 20);
			this.rdoMyDocuments.TabIndex = 0;
			this.rdoMyDocuments.Tag = "Radio_Cloud_MyDocuments";
			this.rdoMyDocuments.Text = "My Documents";
			this.rdoMyDocuments.Checked = true;
			this.rdoMyDocuments.UseVisualStyleBackColor = true;
			this.rdoMyDocuments.CheckedChanged += new System.EventHandler(this.rdoDocumentMode_CheckedChanged);
			//
			// rdoSharedWithMe
			//
			this.rdoSharedWithMe.Location = new System.Drawing.Point(158, 12);
			this.rdoSharedWithMe.Name = "rdoSharedWithMe";
			this.rdoSharedWithMe.Size = new System.Drawing.Size(160, 20);
			this.rdoSharedWithMe.TabIndex = 1;
			this.rdoSharedWithMe.Tag = "Radio_Cloud_SharedWithMe";
			this.rdoSharedWithMe.Text = "Shared With Me";
			this.rdoSharedWithMe.UseVisualStyleBackColor = true;
			this.rdoSharedWithMe.CheckedChanged += new System.EventHandler(this.rdoDocumentMode_CheckedChanged);
			//
			// lblApiToken
			//
			this.lblApiToken.AutoSize = true;
			this.lblApiToken.Location = new System.Drawing.Point(12, 41);
			this.lblApiToken.Name = "lblApiToken";
			this.lblApiToken.Size = new System.Drawing.Size(60, 13);
			this.lblApiToken.TabIndex = 2;
			this.lblApiToken.Tag = "Label_Cloud_ApiToken";
			this.lblApiToken.Text = "API Token:";
			//
			// txtApiToken
			//
			this.txtApiToken.Location = new System.Drawing.Point(80, 38);
			this.txtApiToken.Name = "txtApiToken";
			this.txtApiToken.Size = new System.Drawing.Size(300, 20);
			this.txtApiToken.TabIndex = 3;
			this.txtApiToken.UseSystemPasswordChar = true;
			//
			// cmdUseApiToken
			//
			this.cmdUseApiToken.Location = new System.Drawing.Point(386, 36);
			this.cmdUseApiToken.Name = "cmdUseApiToken";
			this.cmdUseApiToken.Size = new System.Drawing.Size(90, 23);
			this.cmdUseApiToken.TabIndex = 4;
			this.cmdUseApiToken.Tag = "Button_Cloud_UseApiToken";
			this.cmdUseApiToken.Text = "Use Token";
			this.cmdUseApiToken.UseVisualStyleBackColor = true;
			this.cmdUseApiToken.Click += new System.EventHandler(this.cmdUseApiToken_Click);
			//
			// lstDocuments
			//
			this.lstDocuments.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
				this.colName,
				this.colState,
				this.colUpdated,
				this.colShare});
			this.lstDocuments.View = System.Windows.Forms.View.Details;
			this.lstDocuments.FullRowSelect = true;
			this.lstDocuments.MultiSelect = false;
			this.lstDocuments.HideSelection = false;
			this.lstDocuments.Location = new System.Drawing.Point(12, 64);
			this.lstDocuments.Name = "lstDocuments";
			this.lstDocuments.Size = new System.Drawing.Size(560, 300);
			this.lstDocuments.TabIndex = 5;
			this.lstDocuments.SelectedIndexChanged += new System.EventHandler(this.lstDocuments_SelectedIndexChanged);
			this.lstDocuments.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
				| System.Windows.Forms.AnchorStyles.Left)
				| System.Windows.Forms.AnchorStyles.Right)));
			//
			// colName
			//
			this.colName.Text = "Name";
			this.colName.Width = 220;
			//
			// colState
			//
			this.colState.Text = "Validation State";
			this.colState.Width = 110;
			//
			// colUpdated
			//
			this.colUpdated.Text = "Updated";
			this.colUpdated.Width = 130;
			//
			// colShare
			//
			this.colShare.Text = "Share";
			this.colShare.Width = 100;
			//
			// cmdLogin
			//
			this.cmdLogin.Location = new System.Drawing.Point(12, 372);
			this.cmdLogin.Name = "cmdLogin";
			this.cmdLogin.Size = new System.Drawing.Size(90, 27);
			this.cmdLogin.TabIndex = 6;
			this.cmdLogin.Tag = "Button_Cloud_Login";
			this.cmdLogin.Text = "Log In";
			this.cmdLogin.UseVisualStyleBackColor = true;
			this.cmdLogin.Click += new System.EventHandler(this.cmdLogin_Click);
			//
			// cmdLogout
			//
			this.cmdLogout.Location = new System.Drawing.Point(108, 372);
			this.cmdLogout.Name = "cmdLogout";
			this.cmdLogout.Size = new System.Drawing.Size(90, 27);
			this.cmdLogout.TabIndex = 7;
			this.cmdLogout.Tag = "Button_Cloud_Logout";
			this.cmdLogout.Text = "Log Out";
			this.cmdLogout.UseVisualStyleBackColor = true;
			this.cmdLogout.Click += new System.EventHandler(this.cmdLogout_Click);
			//
			// cmdRefresh
			//
			this.cmdRefresh.Location = new System.Drawing.Point(204, 372);
			this.cmdRefresh.Name = "cmdRefresh";
			this.cmdRefresh.Size = new System.Drawing.Size(90, 27);
			this.cmdRefresh.TabIndex = 8;
			this.cmdRefresh.Tag = "Button_Cloud_Refresh";
			this.cmdRefresh.Text = "Refresh";
			this.cmdRefresh.UseVisualStyleBackColor = true;
			this.cmdRefresh.Click += new System.EventHandler(this.cmdRefresh_Click);
			//
			// cmdPushCurrent
			//
			this.cmdPushCurrent.Location = new System.Drawing.Point(300, 372);
			this.cmdPushCurrent.Name = "cmdPushCurrent";
			this.cmdPushCurrent.Size = new System.Drawing.Size(150, 27);
			this.cmdPushCurrent.TabIndex = 9;
			this.cmdPushCurrent.Tag = "Button_Cloud_PushCurrent";
			this.cmdPushCurrent.Text = "Push Current Character";
			this.cmdPushCurrent.UseVisualStyleBackColor = true;
			this.cmdPushCurrent.Click += new System.EventHandler(this.cmdPushCurrent_Click);
			//
			// cmdDownload
			//
			this.cmdDownload.Location = new System.Drawing.Point(456, 372);
			this.cmdDownload.Name = "cmdDownload";
			this.cmdDownload.Size = new System.Drawing.Size(115, 27);
			this.cmdDownload.TabIndex = 10;
			this.cmdDownload.Tag = "Button_Cloud_Download";
			this.cmdDownload.Text = "Download Selected";
			this.cmdDownload.UseVisualStyleBackColor = true;
			this.cmdDownload.Click += new System.EventHandler(this.cmdDownload_Click);
			//
			// cmdArchive
			//
			this.cmdArchive.Location = new System.Drawing.Point(12, 406);
			this.cmdArchive.Name = "cmdArchive";
			this.cmdArchive.Size = new System.Drawing.Size(115, 27);
			this.cmdArchive.TabIndex = 11;
			this.cmdArchive.Tag = "Button_Cloud_Archive";
			this.cmdArchive.Text = "Archive Selected";
			this.cmdArchive.UseVisualStyleBackColor = true;
			this.cmdArchive.Click += new System.EventHandler(this.cmdArchive_Click);
			//
			// cmdPushShared
			//
			this.cmdPushShared.Location = new System.Drawing.Point(133, 406);
			this.cmdPushShared.Name = "cmdPushShared";
			this.cmdPushShared.Size = new System.Drawing.Size(180, 27);
			this.cmdPushShared.TabIndex = 12;
			this.cmdPushShared.Tag = "Button_Cloud_PushShared";
			this.cmdPushShared.Text = "Push Update to Selected";
			this.cmdPushShared.UseVisualStyleBackColor = true;
			this.cmdPushShared.Visible = false;
			this.cmdPushShared.Click += new System.EventHandler(this.cmdPushShared_Click);
			//
			// lblStatus
			//
			this.lblStatus.AutoSize = true;
			this.lblStatus.Location = new System.Drawing.Point(12, 442);
			this.lblStatus.Name = "lblStatus";
			this.lblStatus.Size = new System.Drawing.Size(38, 13);
			this.lblStatus.TabIndex = 13;
			this.lblStatus.Text = "";
			this.lblStatus.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			//
			// frmCloudDocuments
			//
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(584, 473);
			this.Controls.Add(this.lblStatus);
			this.Controls.Add(this.cmdPushShared);
			this.Controls.Add(this.cmdArchive);
			this.Controls.Add(this.cmdDownload);
			this.Controls.Add(this.cmdPushCurrent);
			this.Controls.Add(this.cmdRefresh);
			this.Controls.Add(this.cmdLogout);
			this.Controls.Add(this.cmdLogin);
			this.Controls.Add(this.lstDocuments);
			this.Controls.Add(this.cmdUseApiToken);
			this.Controls.Add(this.txtApiToken);
			this.Controls.Add(this.lblApiToken);
			this.Controls.Add(this.rdoSharedWithMe);
			this.Controls.Add(this.rdoMyDocuments);
			this.MinimumSize = new System.Drawing.Size(500, 402);
			this.Name = "frmCloudDocuments";
			this.Tag = "Title_CloudDocuments";
			this.Text = "Cloud Documents";
			this.Load += new System.EventHandler(this.frmCloudDocuments_Load);
			this.ResumeLayout(false);
			this.PerformLayout();
		}

		private System.Windows.Forms.RadioButton rdoMyDocuments;
		private System.Windows.Forms.RadioButton rdoSharedWithMe;
		private System.Windows.Forms.Label lblApiToken;
		private System.Windows.Forms.TextBox txtApiToken;
		private System.Windows.Forms.Button cmdUseApiToken;
		private System.Windows.Forms.ListView lstDocuments;
		private System.Windows.Forms.ColumnHeader colName;
		private System.Windows.Forms.ColumnHeader colState;
		private System.Windows.Forms.ColumnHeader colUpdated;
		private System.Windows.Forms.ColumnHeader colShare;
		private System.Windows.Forms.Button cmdLogin;
		private System.Windows.Forms.Button cmdLogout;
		private System.Windows.Forms.Button cmdRefresh;
		private System.Windows.Forms.Button cmdPushCurrent;
		private System.Windows.Forms.Button cmdDownload;
		private System.Windows.Forms.Button cmdArchive;
		private System.Windows.Forms.Button cmdPushShared;
		private System.Windows.Forms.Label lblStatus;
	}
}
