namespace Chummer
{
	partial class frmCloudMetadata
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
			this.lblNote = new System.Windows.Forms.Label();
			this.lblDisplayName = new System.Windows.Forms.Label();
			this.txtDisplayName = new System.Windows.Forms.TextBox();
			this.lblDescription = new System.Windows.Forms.Label();
			this.txtDescription = new System.Windows.Forms.TextBox();
			this.lblImageUrl = new System.Windows.Forms.Label();
			this.txtImageUrl = new System.Windows.Forms.TextBox();
			this.cmdOK = new System.Windows.Forms.Button();
			this.cmdCancel = new System.Windows.Forms.Button();
			this.SuspendLayout();
			//
			// lblNote
			//
			this.lblNote.Location = new System.Drawing.Point(12, 9);
			this.lblNote.Name = "lblNote";
			this.lblNote.Size = new System.Drawing.Size(430, 32);
			this.lblNote.TabIndex = 0;
			this.lblNote.Tag = "Label_CloudMetadata_Note";
			this.lblNote.Text = "Not sent to RunnersPoint yet - the server has no way to accept this from a client. Stored locally so it isn\'t lost once it can.";
			//
			// lblDisplayName
			//
			this.lblDisplayName.AutoSize = true;
			this.lblDisplayName.Location = new System.Drawing.Point(12, 50);
			this.lblDisplayName.Name = "lblDisplayName";
			this.lblDisplayName.Size = new System.Drawing.Size(78, 13);
			this.lblDisplayName.TabIndex = 1;
			this.lblDisplayName.Tag = "Label_CloudMetadata_DisplayName";
			this.lblDisplayName.Text = "Display Name:";
			//
			// txtDisplayName
			//
			this.txtDisplayName.Location = new System.Drawing.Point(110, 47);
			this.txtDisplayName.Name = "txtDisplayName";
			this.txtDisplayName.Size = new System.Drawing.Size(330, 20);
			this.txtDisplayName.TabIndex = 2;
			//
			// lblDescription
			//
			this.lblDescription.AutoSize = true;
			this.lblDescription.Location = new System.Drawing.Point(12, 76);
			this.lblDescription.Name = "lblDescription";
			this.lblDescription.Size = new System.Drawing.Size(63, 13);
			this.lblDescription.TabIndex = 3;
			this.lblDescription.Tag = "Label_CloudMetadata_Description";
			this.lblDescription.Text = "Description:";
			//
			// txtDescription
			//
			this.txtDescription.Location = new System.Drawing.Point(110, 76);
			this.txtDescription.Multiline = true;
			this.txtDescription.Name = "txtDescription";
			this.txtDescription.Size = new System.Drawing.Size(330, 80);
			this.txtDescription.TabIndex = 4;
			//
			// lblImageUrl
			//
			this.lblImageUrl.AutoSize = true;
			this.lblImageUrl.Location = new System.Drawing.Point(12, 164);
			this.lblImageUrl.Name = "lblImageUrl";
			this.lblImageUrl.Size = new System.Drawing.Size(56, 13);
			this.lblImageUrl.TabIndex = 5;
			this.lblImageUrl.Tag = "Label_CloudMetadata_ImageUrl";
			this.lblImageUrl.Text = "Image URL:";
			//
			// txtImageUrl
			//
			this.txtImageUrl.Location = new System.Drawing.Point(110, 161);
			this.txtImageUrl.Name = "txtImageUrl";
			this.txtImageUrl.Size = new System.Drawing.Size(330, 20);
			this.txtImageUrl.TabIndex = 6;
			//
			// cmdOK
			//
			this.cmdOK.Location = new System.Drawing.Point(285, 195);
			this.cmdOK.Name = "cmdOK";
			this.cmdOK.Size = new System.Drawing.Size(75, 27);
			this.cmdOK.TabIndex = 7;
			this.cmdOK.Tag = "String_OK";
			this.cmdOK.Text = "OK";
			this.cmdOK.UseVisualStyleBackColor = true;
			this.cmdOK.Click += new System.EventHandler(this.cmdOK_Click);
			//
			// cmdCancel
			//
			this.cmdCancel.Location = new System.Drawing.Point(366, 195);
			this.cmdCancel.Name = "cmdCancel";
			this.cmdCancel.Size = new System.Drawing.Size(75, 27);
			this.cmdCancel.TabIndex = 8;
			this.cmdCancel.Tag = "String_Cancel";
			this.cmdCancel.Text = "Cancel";
			this.cmdCancel.UseVisualStyleBackColor = true;
			this.cmdCancel.Click += new System.EventHandler(this.cmdCancel_Click);
			//
			// frmCloudMetadata
			//
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(454, 234);
			this.Controls.Add(this.cmdCancel);
			this.Controls.Add(this.cmdOK);
			this.Controls.Add(this.txtImageUrl);
			this.Controls.Add(this.lblImageUrl);
			this.Controls.Add(this.txtDescription);
			this.Controls.Add(this.lblDescription);
			this.Controls.Add(this.txtDisplayName);
			this.Controls.Add(this.lblDisplayName);
			this.Controls.Add(this.lblNote);
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "frmCloudMetadata";
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Tag = "Title_CloudMetadata";
			this.Text = "Cloud Document Metadata";
			this.Load += new System.EventHandler(this.frmCloudMetadata_Load);
			this.ResumeLayout(false);
			this.PerformLayout();
		}

		private System.Windows.Forms.Label lblNote;
		private System.Windows.Forms.Label lblDisplayName;
		private System.Windows.Forms.TextBox txtDisplayName;
		private System.Windows.Forms.Label lblDescription;
		private System.Windows.Forms.TextBox txtDescription;
		private System.Windows.Forms.Label lblImageUrl;
		private System.Windows.Forms.TextBox txtImageUrl;
		private System.Windows.Forms.Button cmdOK;
		private System.Windows.Forms.Button cmdCancel;
	}
}
