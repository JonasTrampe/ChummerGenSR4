namespace Chummer
{
	partial class frmCloudRevisions
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
			this.lstRevisions = new System.Windows.Forms.ListView();
			this.colCreated = new System.Windows.Forms.ColumnHeader();
			this.colState = new System.Windows.Forms.ColumnHeader();
			this.colSize = new System.Windows.Forms.ColumnHeader();
			this.colCurrent = new System.Windows.Forms.ColumnHeader();
			this.cmdDownload = new System.Windows.Forms.Button();
			this.cmdPurgeRevision = new System.Windows.Forms.Button();
			this.cmdPurgeDocument = new System.Windows.Forms.Button();
			this.cmdClose = new System.Windows.Forms.Button();
			this.lblKarma = new System.Windows.Forms.Label();
			this.pgbKarma = new System.Windows.Forms.ProgressBar();
			this.lblKarmaValue = new System.Windows.Forms.Label();
			this.cmdFetchKarma = new System.Windows.Forms.Button();
			this.lblStatus = new System.Windows.Forms.Label();
			this.SuspendLayout();
			//
			// lstRevisions
			//
			this.lstRevisions.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
				this.colCreated,
				this.colState,
				this.colSize,
				this.colCurrent});
			this.lstRevisions.View = System.Windows.Forms.View.Details;
			this.lstRevisions.FullRowSelect = true;
			this.lstRevisions.MultiSelect = false;
			this.lstRevisions.HideSelection = false;
			this.lstRevisions.Location = new System.Drawing.Point(12, 12);
			this.lstRevisions.Name = "lstRevisions";
			this.lstRevisions.Size = new System.Drawing.Size(560, 300);
			this.lstRevisions.TabIndex = 0;
			this.lstRevisions.SelectedIndexChanged += new System.EventHandler(this.lstRevisions_SelectedIndexChanged);
			this.lstRevisions.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
				| System.Windows.Forms.AnchorStyles.Left)
				| System.Windows.Forms.AnchorStyles.Right)));
			//
			// colCreated
			//
			this.colCreated.Text = "Created";
			this.colCreated.Width = 160;
			//
			// colState
			//
			this.colState.Text = "Validation State";
			this.colState.Width = 110;
			//
			// colSize
			//
			this.colSize.Text = "Size (bytes)";
			this.colSize.Width = 100;
			//
			// colCurrent
			//
			this.colCurrent.Text = "";
			this.colCurrent.Width = 80;
			//
			// cmdDownload
			//
			this.cmdDownload.Location = new System.Drawing.Point(12, 320);
			this.cmdDownload.Name = "cmdDownload";
			this.cmdDownload.Size = new System.Drawing.Size(140, 27);
			this.cmdDownload.TabIndex = 1;
			this.cmdDownload.Tag = "Button_CloudRevisions_Download";
			this.cmdDownload.Text = "Download Selected";
			this.cmdDownload.UseVisualStyleBackColor = true;
			this.cmdDownload.Click += new System.EventHandler(this.cmdDownload_Click);
			//
			// cmdPurgeRevision
			//
			this.cmdPurgeRevision.Location = new System.Drawing.Point(158, 320);
			this.cmdPurgeRevision.Name = "cmdPurgeRevision";
			this.cmdPurgeRevision.Size = new System.Drawing.Size(160, 27);
			this.cmdPurgeRevision.TabIndex = 2;
			this.cmdPurgeRevision.Tag = "Button_CloudRevisions_PurgeRevision";
			this.cmdPurgeRevision.Text = "Purge Revision...";
			this.cmdPurgeRevision.UseVisualStyleBackColor = true;
			this.cmdPurgeRevision.Click += new System.EventHandler(this.cmdPurgeRevision_Click);
			//
			// cmdPurgeDocument
			//
			this.cmdPurgeDocument.Location = new System.Drawing.Point(324, 320);
			this.cmdPurgeDocument.Name = "cmdPurgeDocument";
			this.cmdPurgeDocument.Size = new System.Drawing.Size(160, 27);
			this.cmdPurgeDocument.TabIndex = 3;
			this.cmdPurgeDocument.Tag = "Button_CloudRevisions_PurgeDocument";
			this.cmdPurgeDocument.Text = "Purge Document...";
			this.cmdPurgeDocument.UseVisualStyleBackColor = true;
			this.cmdPurgeDocument.Click += new System.EventHandler(this.cmdPurgeDocument_Click);
			//
			// cmdClose
			//
			this.cmdClose.Location = new System.Drawing.Point(497, 320);
			this.cmdClose.Name = "cmdClose";
			this.cmdClose.Size = new System.Drawing.Size(75, 27);
			this.cmdClose.TabIndex = 4;
			this.cmdClose.Tag = "Button_CloudRevisions_Close";
			this.cmdClose.Text = "Close";
			this.cmdClose.UseVisualStyleBackColor = true;
			this.cmdClose.Click += new System.EventHandler(this.cmdClose_Click);
			this.cmdClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			//
			// lblKarma
			//
			this.lblKarma.AutoSize = true;
			this.lblKarma.Location = new System.Drawing.Point(12, 357);
			this.lblKarma.Name = "lblKarma";
			this.lblKarma.Size = new System.Drawing.Size(45, 13);
			this.lblKarma.TabIndex = 5;
			this.lblKarma.Tag = "Label_CloudRevisions_Karma";
			this.lblKarma.Text = "Karma:";
			//
			// pgbKarma
			//
			this.pgbKarma.Location = new System.Drawing.Point(70, 353);
			this.pgbKarma.Name = "pgbKarma";
			this.pgbKarma.Size = new System.Drawing.Size(260, 20);
			this.pgbKarma.TabIndex = 6;
			//
			// lblKarmaValue
			//
			this.lblKarmaValue.AutoSize = true;
			this.lblKarmaValue.Location = new System.Drawing.Point(337, 357);
			this.lblKarmaValue.Name = "lblKarmaValue";
			this.lblKarmaValue.Size = new System.Drawing.Size(13, 13);
			this.lblKarmaValue.TabIndex = 7;
			this.lblKarmaValue.Text = "";
			//
			// cmdFetchKarma
			//
			this.cmdFetchKarma.Location = new System.Drawing.Point(456, 350);
			this.cmdFetchKarma.Name = "cmdFetchKarma";
			this.cmdFetchKarma.Size = new System.Drawing.Size(116, 27);
			this.cmdFetchKarma.TabIndex = 8;
			this.cmdFetchKarma.Tag = "Button_CloudRevisions_FetchKarma";
			this.cmdFetchKarma.Text = "Fetch Karma";
			this.cmdFetchKarma.UseVisualStyleBackColor = true;
			this.cmdFetchKarma.Click += new System.EventHandler(this.cmdFetchKarma_Click);
			//
			// lblStatus
			//
			this.lblStatus.AutoSize = true;
			this.lblStatus.Location = new System.Drawing.Point(12, 388);
			this.lblStatus.Name = "lblStatus";
			this.lblStatus.Size = new System.Drawing.Size(38, 13);
			this.lblStatus.TabIndex = 9;
			this.lblStatus.Text = "";
			this.lblStatus.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			//
			// frmCloudRevisions
			//
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(584, 419);
			this.Controls.Add(this.lblStatus);
			this.Controls.Add(this.cmdFetchKarma);
			this.Controls.Add(this.lblKarmaValue);
			this.Controls.Add(this.pgbKarma);
			this.Controls.Add(this.lblKarma);
			this.Controls.Add(this.cmdClose);
			this.Controls.Add(this.cmdPurgeDocument);
			this.Controls.Add(this.cmdPurgeRevision);
			this.Controls.Add(this.cmdDownload);
			this.Controls.Add(this.lstRevisions);
			this.MinimumSize = new System.Drawing.Size(500, 340);
			this.Name = "frmCloudRevisions";
			this.Tag = "Title_CloudRevisions";
			this.Text = "Revisions";
			this.Load += new System.EventHandler(this.frmCloudRevisions_Load);
			this.ResumeLayout(false);
			this.PerformLayout();
		}

		private System.Windows.Forms.ListView lstRevisions;
		private System.Windows.Forms.ColumnHeader colCreated;
		private System.Windows.Forms.ColumnHeader colState;
		private System.Windows.Forms.ColumnHeader colSize;
		private System.Windows.Forms.ColumnHeader colCurrent;
		private System.Windows.Forms.Button cmdDownload;
		private System.Windows.Forms.Button cmdPurgeRevision;
		private System.Windows.Forms.Button cmdPurgeDocument;
		private System.Windows.Forms.Button cmdClose;
		private System.Windows.Forms.Label lblKarma;
		private System.Windows.Forms.ProgressBar pgbKarma;
		private System.Windows.Forms.Label lblKarmaValue;
		private System.Windows.Forms.Button cmdFetchKarma;
		private System.Windows.Forms.Label lblStatus;
	}
}
