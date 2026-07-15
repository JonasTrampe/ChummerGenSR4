namespace Chummer
{
	partial class frmUpdate
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.lblCurrentVersion = new System.Windows.Forms.Label();
			this.lblLatestVersion = new System.Windows.Forms.Label();
			this.txtReleaseNotes = new System.Windows.Forms.TextBox();
			this.cmdDownload = new System.Windows.Forms.Button();
			this.cmdClose = new System.Windows.Forms.Button();
			this.SuspendLayout();
			//
			// lblCurrentVersion
			//
			this.lblCurrentVersion.AutoSize = true;
			this.lblCurrentVersion.Location = new System.Drawing.Point(12, 15);
			this.lblCurrentVersion.Name = "lblCurrentVersion";
			this.lblCurrentVersion.Size = new System.Drawing.Size(120, 13);
			this.lblCurrentVersion.TabIndex = 0;
			this.lblCurrentVersion.Tag = "Label_Update_CurrentVersion";
			this.lblCurrentVersion.Text = "Current Version:";
			//
			// lblLatestVersion
			//
			this.lblLatestVersion.AutoSize = true;
			this.lblLatestVersion.Location = new System.Drawing.Point(12, 35);
			this.lblLatestVersion.Name = "lblLatestVersion";
			this.lblLatestVersion.Size = new System.Drawing.Size(120, 13);
			this.lblLatestVersion.TabIndex = 1;
			this.lblLatestVersion.Tag = "Label_Update_LatestVersion";
			this.lblLatestVersion.Text = "Latest Version:";
			//
			// txtReleaseNotes
			//
			this.txtReleaseNotes.Location = new System.Drawing.Point(12, 60);
			this.txtReleaseNotes.Multiline = true;
			this.txtReleaseNotes.Name = "txtReleaseNotes";
			this.txtReleaseNotes.ReadOnly = true;
			this.txtReleaseNotes.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.txtReleaseNotes.Size = new System.Drawing.Size(560, 300);
			this.txtReleaseNotes.TabIndex = 2;
			//
			// cmdDownload
			//
			this.cmdDownload.AutoSize = true;
			this.cmdDownload.Location = new System.Drawing.Point(391, 366);
			this.cmdDownload.Name = "cmdDownload";
			this.cmdDownload.Size = new System.Drawing.Size(90, 23);
			this.cmdDownload.TabIndex = 3;
			this.cmdDownload.Tag = "Button_Update_Download";
			this.cmdDownload.Text = "Download";
			this.cmdDownload.UseVisualStyleBackColor = true;
			this.cmdDownload.Click += new System.EventHandler(this.cmdDownload_Click);
			//
			// cmdClose
			//
			this.cmdClose.AutoSize = true;
			this.cmdClose.Location = new System.Drawing.Point(487, 366);
			this.cmdClose.Name = "cmdClose";
			this.cmdClose.Size = new System.Drawing.Size(85, 23);
			this.cmdClose.TabIndex = 4;
			this.cmdClose.Tag = "Button_Update_Close";
			this.cmdClose.Text = "Close";
			this.cmdClose.UseVisualStyleBackColor = true;
			this.cmdClose.Click += new System.EventHandler(this.cmdClose_Click);
			//
			// frmUpdate
			//
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(584, 401);
			this.Controls.Add(this.cmdClose);
			this.Controls.Add(this.cmdDownload);
			this.Controls.Add(this.txtReleaseNotes);
			this.Controls.Add(this.lblLatestVersion);
			this.Controls.Add(this.lblCurrentVersion);
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "frmUpdate";
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Tag = "Title_Update";
			this.Text = "Chummer Update";
			this.Load += new System.EventHandler(this.frmUpdate_Load);
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.Label lblCurrentVersion;
		private System.Windows.Forms.Label lblLatestVersion;
		internal System.Windows.Forms.TextBox txtReleaseNotes;
		internal System.Windows.Forms.Button cmdDownload;
		internal System.Windows.Forms.Button cmdClose;
	}
}
