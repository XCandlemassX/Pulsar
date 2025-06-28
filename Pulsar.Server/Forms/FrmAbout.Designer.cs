﻿namespace Pulsar.Server.Forms
{
    partial class FrmAbout
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FrmAbout));
            this.picIcon = new System.Windows.Forms.PictureBox();
            this.lblTitle = new System.Windows.Forms.Label();
            this.lblVersion = new System.Windows.Forms.Label();
            this.btnOkay = new System.Windows.Forms.Button();
            this.rtxtContent = new System.Windows.Forms.RichTextBox();
            this.lblLicense = new System.Windows.Forms.Label();
            this.lnkCredits = new System.Windows.Forms.LinkLabel();
            this.lnkGithubPage = new System.Windows.Forms.LinkLabel();
            this.lblSubTitle = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.cntTxtContent = new System.Windows.Forms.RichTextBox();
            ((System.ComponentModel.ISupportInitialize)(this.picIcon)).BeginInit();
            this.SuspendLayout();
            // 
            // picIcon
            // 
            this.picIcon.Image = global::Pulsar.Server.Properties.Resources.Pulsar_Server;
            this.picIcon.Location = new System.Drawing.Point(12, 12);
            this.picIcon.Name = "picIcon";
            this.picIcon.Size = new System.Drawing.Size(64, 64);
            this.picIcon.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize;
            this.picIcon.TabIndex = 0;
            this.picIcon.TabStop = false;
            // 
            // lblTitle
            // 
            this.lblTitle.AutoSize = true;
            this.lblTitle.Font = new System.Drawing.Font("Segoe UI", 15.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblTitle.Location = new System.Drawing.Point(82, 7);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(163, 30);
            this.lblTitle.TabIndex = 0;
            this.lblTitle.Text = "Pulsar";
            // 
            // lblVersion
            // 
            this.lblVersion.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Italic, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblVersion.Location = new System.Drawing.Point(438, 41);
            this.lblVersion.Name = "lblVersion";
            this.lblVersion.Size = new System.Drawing.Size(75, 13);
            this.lblVersion.TabIndex = 2;
            this.lblVersion.Text = "%VERSION%";
            this.lblVersion.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // btnOkay
            // 
            this.btnOkay.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnOkay.Location = new System.Drawing.Point(438, 370);
            this.btnOkay.Name = "btnOkay";
            this.btnOkay.Size = new System.Drawing.Size(75, 23);
            this.btnOkay.TabIndex = 7;
            this.btnOkay.Text = "&Okay";
            this.btnOkay.UseVisualStyleBackColor = true;
            this.btnOkay.Click += new System.EventHandler(this.btnOkay_Click);
            // 
            // rtxtContent
            // 
            this.rtxtContent.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.rtxtContent.Location = new System.Drawing.Point(15, 219);
            this.rtxtContent.Name = "rtxtContent";
            this.rtxtContent.ReadOnly = true;
            this.rtxtContent.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
            this.rtxtContent.Size = new System.Drawing.Size(498, 145);
            this.rtxtContent.TabIndex = 6;
            this.rtxtContent.Text = "";
            // 
            // lblLicense
            // 
            this.lblLicense.AutoSize = true;
            this.lblLicense.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblLicense.Location = new System.Drawing.Point(12, 201);
            this.lblLicense.Name = "lblLicense";
            this.lblLicense.Size = new System.Drawing.Size(46, 15);
            this.lblLicense.TabIndex = 5;
            this.lblLicense.Text = "License";
            // 
            // lnkCredits
            // 
            this.lnkCredits.AutoSize = true;
            this.lnkCredits.Location = new System.Drawing.Point(415, 83);
            this.lnkCredits.Name = "lnkCredits";
            this.lnkCredits.Size = new System.Drawing.Size(97, 13);
            this.lnkCredits.TabIndex = 4;
            this.lnkCredits.TabStop = true;
            this.lnkCredits.Text = "3rd-party licenses";
            this.lnkCredits.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.lnkCredits_LinkClicked);
            // 
            // lnkGithubPage
            // 
            this.lnkGithubPage.AutoSize = true;
            this.lnkGithubPage.Location = new System.Drawing.Point(441, 61);
            this.lnkGithubPage.Name = "lnkGithubPage";
            this.lnkGithubPage.Size = new System.Drawing.Size(73, 13);
            this.lnkGithubPage.TabIndex = 3;
            this.lnkGithubPage.TabStop = true;
            this.lnkGithubPage.Text = "GitHub page";
            this.lnkGithubPage.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.lnkGithubPage_LinkClicked);
            // 
            // lblSubTitle
            // 
            this.lblSubTitle.AutoSize = true;
            this.lblSubTitle.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblSubTitle.Location = new System.Drawing.Point(84, 37);
            this.lblSubTitle.Name = "lblSubTitle";
            this.lblSubTitle.Size = new System.Drawing.Size(170, 17);
            this.lblSubTitle.TabIndex = 1;
            this.lblSubTitle.Text = "Remote Administration Tool";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(12, 108);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(74, 15);
            this.label1.TabIndex = 8;
            this.label1.Text = "Contributors";
            // 
            // cntTxtContent
            // 
            this.cntTxtContent.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cntTxtContent.Location = new System.Drawing.Point(14, 131);
            this.cntTxtContent.Name = "cntTxtContent";
            this.cntTxtContent.ReadOnly = true;
            this.cntTxtContent.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
            this.cntTxtContent.Size = new System.Drawing.Size(498, 62);
            this.cntTxtContent.TabIndex = 9;
            this.cntTxtContent.Text = "";
            // 
            // FrmAbout
            // 
            this.AcceptButton = this.btnOkay;
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.CancelButton = this.btnOkay;
            this.ClientSize = new System.Drawing.Size(525, 405);
            this.Controls.Add(this.cntTxtContent);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.lblSubTitle);
            this.Controls.Add(this.lnkGithubPage);
            this.Controls.Add(this.lnkCredits);
            this.Controls.Add(this.lblLicense);
            this.Controls.Add(this.rtxtContent);
            this.Controls.Add(this.btnOkay);
            this.Controls.Add(this.lblVersion);
            this.Controls.Add(this.lblTitle);
            this.Controls.Add(this.picIcon);
            this.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FrmAbout";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Pulsar - About";
            ((System.ComponentModel.ISupportInitialize)(this.picIcon)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.PictureBox picIcon;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblVersion;
        private System.Windows.Forms.Button btnOkay;
        private System.Windows.Forms.RichTextBox rtxtContent;
        private System.Windows.Forms.Label lblLicense;
        private System.Windows.Forms.LinkLabel lnkCredits;
        private System.Windows.Forms.LinkLabel lnkGithubPage;
        private System.Windows.Forms.Label lblSubTitle;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.RichTextBox cntTxtContent;
    }
}