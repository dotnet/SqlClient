namespace Microsoft.Data.SqlClient.Samples.AzureSqlConnector
{
    partial class MainForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.lblServer = new System.Windows.Forms.Label();
            this.txtServer = new System.Windows.Forms.TextBox();
            this.lblDatabase = new System.Windows.Forms.Label();
            this.txtDatabase = new System.Windows.Forms.TextBox();
            this.lblAuthentication = new System.Windows.Forms.Label();
            this.cmbAuthentication = new System.Windows.Forms.ComboBox();
            this.lblUserId = new System.Windows.Forms.Label();
            this.txtUserId = new System.Windows.Forms.TextBox();
            this.lblPassword = new System.Windows.Forms.Label();
            this.txtPassword = new System.Windows.Forms.TextBox();
            this.lblEncrypt = new System.Windows.Forms.Label();
            this.cmbEncrypt = new System.Windows.Forms.ComboBox();
            this.chkTrustServerCertificate = new System.Windows.Forms.CheckBox();
            this.lblTimeout = new System.Windows.Forms.Label();
            this.numTimeout = new System.Windows.Forms.NumericUpDown();
            this.lblConnectionString = new System.Windows.Forms.Label();
            this.txtConnectionString = new System.Windows.Forms.TextBox();
            this.btnBuild = new System.Windows.Forms.Button();
            this.btnTest = new System.Windows.Forms.Button();
            this.btnCopy = new System.Windows.Forms.Button();
            this.btnClear = new System.Windows.Forms.Button();
            this.btnWhoAmI = new System.Windows.Forms.Button();
            this.lblStatus = new System.Windows.Forms.Label();
            this.txtStatus = new System.Windows.Forms.TextBox();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.statusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            ((System.ComponentModel.ISupportInitialize)(this.numTimeout)).BeginInit();
            this.statusStrip.SuspendLayout();
            this.SuspendLayout();
            //
            // lblServer
            //
            this.lblServer.AutoSize = true;
            this.lblServer.Location = new System.Drawing.Point(16, 18);
            this.lblServer.Name = "lblServer";
            this.lblServer.Size = new System.Drawing.Size(75, 13);
            this.lblServer.TabIndex = 0;
            this.lblServer.Text = "&Server name:";
            //
            // txtServer
            //
            this.txtServer.Location = new System.Drawing.Point(150, 15);
            this.txtServer.Name = "txtServer";
            this.txtServer.Size = new System.Drawing.Size(400, 20);
            this.txtServer.TabIndex = 1;
            //
            // lblDatabase
            //
            this.lblDatabase.AutoSize = true;
            this.lblDatabase.Location = new System.Drawing.Point(16, 48);
            this.lblDatabase.Name = "lblDatabase";
            this.lblDatabase.Size = new System.Drawing.Size(86, 13);
            this.lblDatabase.TabIndex = 2;
            this.lblDatabase.Text = "&Database name:";
            //
            // txtDatabase
            //
            this.txtDatabase.Location = new System.Drawing.Point(150, 45);
            this.txtDatabase.Name = "txtDatabase";
            this.txtDatabase.Size = new System.Drawing.Size(400, 20);
            this.txtDatabase.TabIndex = 3;
            //
            // lblAuthentication
            //
            this.lblAuthentication.AutoSize = true;
            this.lblAuthentication.Location = new System.Drawing.Point(16, 78);
            this.lblAuthentication.Name = "lblAuthentication";
            this.lblAuthentication.Size = new System.Drawing.Size(80, 13);
            this.lblAuthentication.TabIndex = 4;
            this.lblAuthentication.Text = "&Authentication:";
            //
            // cmbAuthentication
            //
            this.cmbAuthentication.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbAuthentication.FormattingEnabled = true;
            this.cmbAuthentication.Location = new System.Drawing.Point(150, 75);
            this.cmbAuthentication.Name = "cmbAuthentication";
            this.cmbAuthentication.Size = new System.Drawing.Size(400, 21);
            this.cmbAuthentication.TabIndex = 5;
            this.cmbAuthentication.SelectedIndexChanged += new System.EventHandler(this.cmbAuthentication_SelectedIndexChanged);
            //
            // lblUserId
            //
            this.lblUserId.AutoSize = true;
            this.lblUserId.Location = new System.Drawing.Point(16, 108);
            this.lblUserId.Name = "lblUserId";
            this.lblUserId.Size = new System.Drawing.Size(45, 13);
            this.lblUserId.TabIndex = 6;
            this.lblUserId.Text = "&User ID:";
            //
            // txtUserId
            //
            this.txtUserId.Location = new System.Drawing.Point(150, 105);
            this.txtUserId.Name = "txtUserId";
            this.txtUserId.Size = new System.Drawing.Size(400, 20);
            this.txtUserId.TabIndex = 7;
            //
            // lblPassword
            //
            this.lblPassword.AutoSize = true;
            this.lblPassword.Location = new System.Drawing.Point(16, 138);
            this.lblPassword.Name = "lblPassword";
            this.lblPassword.Size = new System.Drawing.Size(56, 13);
            this.lblPassword.TabIndex = 8;
            this.lblPassword.Text = "&Password:";
            //
            // txtPassword
            //
            this.txtPassword.Location = new System.Drawing.Point(150, 135);
            this.txtPassword.Name = "txtPassword";
            this.txtPassword.Size = new System.Drawing.Size(400, 20);
            this.txtPassword.TabIndex = 9;
            this.txtPassword.UseSystemPasswordChar = true;
            //
            // lblEncrypt
            //
            this.lblEncrypt.AutoSize = true;
            this.lblEncrypt.Location = new System.Drawing.Point(16, 168);
            this.lblEncrypt.Name = "lblEncrypt";
            this.lblEncrypt.Size = new System.Drawing.Size(46, 13);
            this.lblEncrypt.TabIndex = 10;
            this.lblEncrypt.Text = "&Encrypt:";
            //
            // cmbEncrypt
            //
            this.cmbEncrypt.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbEncrypt.FormattingEnabled = true;
            this.cmbEncrypt.Location = new System.Drawing.Point(150, 165);
            this.cmbEncrypt.Name = "cmbEncrypt";
            this.cmbEncrypt.Size = new System.Drawing.Size(200, 21);
            this.cmbEncrypt.TabIndex = 11;
            //
            // chkTrustServerCertificate
            //
            this.chkTrustServerCertificate.AutoSize = true;
            this.chkTrustServerCertificate.Location = new System.Drawing.Point(370, 167);
            this.chkTrustServerCertificate.Name = "chkTrustServerCertificate";
            this.chkTrustServerCertificate.Size = new System.Drawing.Size(149, 17);
            this.chkTrustServerCertificate.TabIndex = 12;
            this.chkTrustServerCertificate.Text = "&Trust server certificate";
            this.chkTrustServerCertificate.UseVisualStyleBackColor = true;
            //
            // lblTimeout
            //
            this.lblTimeout.AutoSize = true;
            this.lblTimeout.Location = new System.Drawing.Point(16, 198);
            this.lblTimeout.Name = "lblTimeout";
            this.lblTimeout.Size = new System.Drawing.Size(101, 13);
            this.lblTimeout.TabIndex = 13;
            this.lblTimeout.Text = "Connect timeout (s):";
            //
            // numTimeout
            //
            this.numTimeout.Location = new System.Drawing.Point(150, 196);
            this.numTimeout.Maximum = new decimal(new int[] { 600, 0, 0, 0 });
            this.numTimeout.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numTimeout.Name = "numTimeout";
            this.numTimeout.Size = new System.Drawing.Size(80, 20);
            this.numTimeout.TabIndex = 14;
            this.numTimeout.Value = new decimal(new int[] { 30, 0, 0, 0 });
            //
            // lblConnectionString
            //
            this.lblConnectionString.AutoSize = true;
            this.lblConnectionString.Location = new System.Drawing.Point(16, 230);
            this.lblConnectionString.Name = "lblConnectionString";
            this.lblConnectionString.Size = new System.Drawing.Size(94, 13);
            this.lblConnectionString.TabIndex = 15;
            this.lblConnectionString.Text = "Connection string:";
            //
            // txtConnectionString
            //
            this.txtConnectionString.Location = new System.Drawing.Point(16, 246);
            this.txtConnectionString.Multiline = true;
            this.txtConnectionString.Name = "txtConnectionString";
            this.txtConnectionString.ReadOnly = true;
            this.txtConnectionString.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtConnectionString.Size = new System.Drawing.Size(534, 60);
            this.txtConnectionString.TabIndex = 16;
            this.txtConnectionString.BackColor = System.Drawing.SystemColors.Info;
            //
            // btnBuild
            //
            this.btnBuild.Location = new System.Drawing.Point(16, 316);
            this.btnBuild.Name = "btnBuild";
            this.btnBuild.Size = new System.Drawing.Size(140, 26);
            this.btnBuild.TabIndex = 17;
            this.btnBuild.Text = "&Build Connection String";
            this.btnBuild.UseVisualStyleBackColor = true;
            this.btnBuild.Click += new System.EventHandler(this.btnBuild_Click);
            //
            // btnTest
            //
            this.btnTest.Location = new System.Drawing.Point(166, 316);
            this.btnTest.Name = "btnTest";
            this.btnTest.Size = new System.Drawing.Size(120, 26);
            this.btnTest.TabIndex = 18;
            this.btnTest.Text = "Te&st Connection";
            this.btnTest.UseVisualStyleBackColor = true;
            this.btnTest.Click += new System.EventHandler(this.btnTest_Click);
            //
            // btnCopy
            //
            this.btnCopy.Location = new System.Drawing.Point(296, 316);
            this.btnCopy.Name = "btnCopy";
            this.btnCopy.Size = new System.Drawing.Size(120, 26);
            this.btnCopy.TabIndex = 19;
            this.btnCopy.Text = "Cop&y to Clipboard";
            this.btnCopy.UseVisualStyleBackColor = true;
            this.btnCopy.Click += new System.EventHandler(this.btnCopy_Click);
            //
            // btnClear
            //
            this.btnClear.Location = new System.Drawing.Point(426, 316);
            this.btnClear.Name = "btnClear";
            this.btnClear.Size = new System.Drawing.Size(124, 26);
            this.btnClear.TabIndex = 20;
            this.btnClear.Text = "Cl&ear All";
            this.btnClear.UseVisualStyleBackColor = true;
            this.btnClear.Click += new System.EventHandler(this.btnClear_Click);
            //
            // btnWhoAmI
            //
            this.btnWhoAmI.Location = new System.Drawing.Point(16, 348);
            this.btnWhoAmI.Name = "btnWhoAmI";
            this.btnWhoAmI.Size = new System.Drawing.Size(534, 26);
            this.btnWhoAmI.TabIndex = 21;
            this.btnWhoAmI.Text = "&Who Am I? (run identity query on the database)";
            this.btnWhoAmI.UseVisualStyleBackColor = true;
            this.btnWhoAmI.Click += new System.EventHandler(this.btnWhoAmI_Click);
            //
            // lblStatus
            //
            this.lblStatus.AutoSize = true;
            this.lblStatus.Location = new System.Drawing.Point(16, 386);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(40, 13);
            this.lblStatus.TabIndex = 22;
            this.lblStatus.Text = "Result:";
            //
            // txtStatus
            //
            this.txtStatus.Location = new System.Drawing.Point(16, 402);
            this.txtStatus.Multiline = true;
            this.txtStatus.Name = "txtStatus";
            this.txtStatus.ReadOnly = true;
            this.txtStatus.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.txtStatus.Size = new System.Drawing.Size(534, 160);
            this.txtStatus.TabIndex = 23;
            this.txtStatus.WordWrap = false;
            this.txtStatus.Font = new System.Drawing.Font("Consolas", 9F);
            //
            // statusStrip
            //
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.statusLabel});
            this.statusStrip.Location = new System.Drawing.Point(0, 578);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Size = new System.Drawing.Size(566, 22);
            this.statusStrip.TabIndex = 24;
            //
            // statusLabel
            //
            this.statusLabel.Name = "statusLabel";
            this.statusLabel.Size = new System.Drawing.Size(39, 17);
            this.statusLabel.Text = "Ready";
            //
            // MainForm
            //
            this.AcceptButton = this.btnTest;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(566, 600);
            this.Controls.Add(this.statusStrip);
            this.Controls.Add(this.txtStatus);
            this.Controls.Add(this.lblStatus);
            this.Controls.Add(this.btnWhoAmI);
            this.Controls.Add(this.btnClear);
            this.Controls.Add(this.btnCopy);
            this.Controls.Add(this.btnTest);
            this.Controls.Add(this.btnBuild);
            this.Controls.Add(this.txtConnectionString);
            this.Controls.Add(this.lblConnectionString);
            this.Controls.Add(this.numTimeout);
            this.Controls.Add(this.lblTimeout);
            this.Controls.Add(this.chkTrustServerCertificate);
            this.Controls.Add(this.cmbEncrypt);
            this.Controls.Add(this.lblEncrypt);
            this.Controls.Add(this.txtPassword);
            this.Controls.Add(this.lblPassword);
            this.Controls.Add(this.txtUserId);
            this.Controls.Add(this.lblUserId);
            this.Controls.Add(this.cmbAuthentication);
            this.Controls.Add(this.lblAuthentication);
            this.Controls.Add(this.txtDatabase);
            this.Controls.Add(this.lblDatabase);
            this.Controls.Add(this.txtServer);
            this.Controls.Add(this.lblServer);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Azure SQL Connector";
            ((System.ComponentModel.ISupportInitialize)(this.numTimeout)).EndInit();
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label lblServer;
        private System.Windows.Forms.TextBox txtServer;
        private System.Windows.Forms.Label lblDatabase;
        private System.Windows.Forms.TextBox txtDatabase;
        private System.Windows.Forms.Label lblAuthentication;
        private System.Windows.Forms.ComboBox cmbAuthentication;
        private System.Windows.Forms.Label lblUserId;
        private System.Windows.Forms.TextBox txtUserId;
        private System.Windows.Forms.Label lblPassword;
        private System.Windows.Forms.TextBox txtPassword;
        private System.Windows.Forms.Label lblEncrypt;
        private System.Windows.Forms.ComboBox cmbEncrypt;
        private System.Windows.Forms.CheckBox chkTrustServerCertificate;
        private System.Windows.Forms.Label lblTimeout;
        private System.Windows.Forms.NumericUpDown numTimeout;
        private System.Windows.Forms.Label lblConnectionString;
        private System.Windows.Forms.TextBox txtConnectionString;
        private System.Windows.Forms.Button btnBuild;
        private System.Windows.Forms.Button btnTest;
        private System.Windows.Forms.Button btnCopy;
        private System.Windows.Forms.Button btnClear;
        private System.Windows.Forms.Button btnWhoAmI;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.TextBox txtStatus;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel statusLabel;
    }
}
