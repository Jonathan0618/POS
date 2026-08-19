namespace POS.Forms.Security
{
    partial class frmEditUser
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmEditUser));
            this.btnSave = new DevExpress.XtraEditors.SimpleButton();
            this.labelControl9 = new DevExpress.XtraEditors.LabelControl();
            this.slueRole = new DevExpress.XtraEditors.SearchLookUpEdit();
            this.searchLookUpEdit1View = new DevExpress.XtraGrid.Views.Grid.GridView();
            this.gridColumn1 = new DevExpress.XtraGrid.Columns.GridColumn();
            this.labelControl8 = new DevExpress.XtraEditors.LabelControl();
            this.txtPassword = new DevExpress.XtraEditors.TextEdit();
            this.labelControl7 = new DevExpress.XtraEditors.LabelControl();
            this.txtSalutation = new DevExpress.XtraEditors.TextEdit();
            this.labelControl6 = new DevExpress.XtraEditors.LabelControl();
            this.txtNameExt = new DevExpress.XtraEditors.TextEdit();
            this.labelControl5 = new DevExpress.XtraEditors.LabelControl();
            this.txtLastName = new DevExpress.XtraEditors.TextEdit();
            this.labelControl4 = new DevExpress.XtraEditors.LabelControl();
            this.txtMiddleName = new DevExpress.XtraEditors.TextEdit();
            this.labelControl3 = new DevExpress.XtraEditors.LabelControl();
            this.labelControl2 = new DevExpress.XtraEditors.LabelControl();
            this.txtFirstName = new DevExpress.XtraEditors.TextEdit();
            this.txtUsername = new DevExpress.XtraEditors.TextEdit();
            this.panelControl1 = new DevExpress.XtraEditors.PanelControl();
            this.labelControl1 = new DevExpress.XtraEditors.LabelControl();
            this.groupControl1 = new DevExpress.XtraEditors.GroupControl();
            this.textEdit1 = new DevExpress.XtraEditors.TextEdit();
            this.labelControl10 = new DevExpress.XtraEditors.LabelControl();
            this.dxErrorProvider1 = new DevExpress.XtraEditors.DXErrorProvider.DXErrorProvider(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.slueRole.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.searchLookUpEdit1View)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.txtPassword.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.txtSalutation.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.txtNameExt.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.txtLastName.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.txtMiddleName.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.txtFirstName.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.txtUsername.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.panelControl1)).BeginInit();
            this.panelControl1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.groupControl1)).BeginInit();
            this.groupControl1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.textEdit1.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dxErrorProvider1)).BeginInit();
            this.SuspendLayout();
            // 
            // btnSave
            // 
            this.btnSave.Appearance.BackColor = System.Drawing.Color.Turquoise;
            this.btnSave.Appearance.Options.UseBackColor = true;
            this.btnSave.ImageOptions.Image = ((System.Drawing.Image)(resources.GetObject("btnSave.ImageOptions.Image")));
            this.btnSave.Location = new System.Drawing.Point(308, 353);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(98, 32);
            this.btnSave.TabIndex = 37;
            this.btnSave.Text = "Save";
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // labelControl9
            // 
            this.labelControl9.Location = new System.Drawing.Point(12, 226);
            this.labelControl9.Name = "labelControl9";
            this.labelControl9.Size = new System.Drawing.Size(22, 13);
            this.labelControl9.TabIndex = 36;
            this.labelControl9.Text = "Role";
            // 
            // slueRole
            // 
            this.slueRole.Location = new System.Drawing.Point(109, 223);
            this.slueRole.Name = "slueRole";
            this.slueRole.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)});
            this.slueRole.Properties.DisplayMember = "Name";
            this.slueRole.Properties.NullText = "";
            this.slueRole.Properties.PopupView = this.searchLookUpEdit1View;
            this.slueRole.Properties.ValueMember = "Id";
            this.slueRole.Size = new System.Drawing.Size(297, 20);
            this.slueRole.TabIndex = 35;
            // 
            // searchLookUpEdit1View
            // 
            this.searchLookUpEdit1View.Columns.AddRange(new DevExpress.XtraGrid.Columns.GridColumn[] {
            this.gridColumn1});
            this.searchLookUpEdit1View.FocusRectStyle = DevExpress.XtraGrid.Views.Grid.DrawFocusRectStyle.RowFocus;
            this.searchLookUpEdit1View.Name = "searchLookUpEdit1View";
            this.searchLookUpEdit1View.OptionsSelection.EnableAppearanceFocusedCell = false;
            this.searchLookUpEdit1View.OptionsView.ShowGroupPanel = false;
            // 
            // gridColumn1
            // 
            this.gridColumn1.Caption = "Role";
            this.gridColumn1.FieldName = "Name";
            this.gridColumn1.Name = "gridColumn1";
            this.gridColumn1.Visible = true;
            this.gridColumn1.VisibleIndex = 0;
            // 
            // labelControl8
            // 
            this.labelControl8.Location = new System.Drawing.Point(12, 27);
            this.labelControl8.Name = "labelControl8";
            this.labelControl8.Size = new System.Drawing.Size(71, 13);
            this.labelControl8.TabIndex = 34;
            this.labelControl8.Text = "New Password";
            // 
            // txtPassword
            // 
            this.txtPassword.Location = new System.Drawing.Point(109, 24);
            this.txtPassword.Name = "txtPassword";
            this.txtPassword.Size = new System.Drawing.Size(297, 20);
            this.txtPassword.TabIndex = 33;
            // 
            // labelControl7
            // 
            this.labelControl7.Location = new System.Drawing.Point(12, 200);
            this.labelControl7.Name = "labelControl7";
            this.labelControl7.Size = new System.Drawing.Size(47, 13);
            this.labelControl7.TabIndex = 32;
            this.labelControl7.Text = "Salutation";
            // 
            // txtSalutation
            // 
            this.txtSalutation.Location = new System.Drawing.Point(109, 197);
            this.txtSalutation.Name = "txtSalutation";
            this.txtSalutation.Size = new System.Drawing.Size(297, 20);
            this.txtSalutation.TabIndex = 31;
            // 
            // labelControl6
            // 
            this.labelControl6.Location = new System.Drawing.Point(12, 174);
            this.labelControl6.Name = "labelControl6";
            this.labelControl6.Size = new System.Drawing.Size(77, 13);
            this.labelControl6.TabIndex = 30;
            this.labelControl6.Text = "Name Extension";
            // 
            // txtNameExt
            // 
            this.txtNameExt.Location = new System.Drawing.Point(109, 171);
            this.txtNameExt.Name = "txtNameExt";
            this.txtNameExt.Size = new System.Drawing.Size(297, 20);
            this.txtNameExt.TabIndex = 29;
            // 
            // labelControl5
            // 
            this.labelControl5.Location = new System.Drawing.Point(12, 148);
            this.labelControl5.Name = "labelControl5";
            this.labelControl5.Size = new System.Drawing.Size(46, 13);
            this.labelControl5.TabIndex = 28;
            this.labelControl5.Text = "Lastname";
            // 
            // txtLastName
            // 
            this.txtLastName.Location = new System.Drawing.Point(109, 145);
            this.txtLastName.Name = "txtLastName";
            this.txtLastName.Size = new System.Drawing.Size(297, 20);
            this.txtLastName.TabIndex = 27;
            // 
            // labelControl4
            // 
            this.labelControl4.Location = new System.Drawing.Point(12, 122);
            this.labelControl4.Name = "labelControl4";
            this.labelControl4.Size = new System.Drawing.Size(57, 13);
            this.labelControl4.TabIndex = 26;
            this.labelControl4.Text = "Middlename";
            // 
            // txtMiddleName
            // 
            this.txtMiddleName.Location = new System.Drawing.Point(109, 119);
            this.txtMiddleName.Name = "txtMiddleName";
            this.txtMiddleName.Size = new System.Drawing.Size(297, 20);
            this.txtMiddleName.TabIndex = 25;
            // 
            // labelControl3
            // 
            this.labelControl3.Location = new System.Drawing.Point(12, 96);
            this.labelControl3.Name = "labelControl3";
            this.labelControl3.Size = new System.Drawing.Size(45, 13);
            this.labelControl3.TabIndex = 24;
            this.labelControl3.Text = "Firstname";
            // 
            // labelControl2
            // 
            this.labelControl2.Location = new System.Drawing.Point(12, 70);
            this.labelControl2.Name = "labelControl2";
            this.labelControl2.Size = new System.Drawing.Size(48, 13);
            this.labelControl2.TabIndex = 23;
            this.labelControl2.Text = "Username";
            // 
            // txtFirstName
            // 
            this.txtFirstName.Location = new System.Drawing.Point(109, 93);
            this.txtFirstName.Name = "txtFirstName";
            this.txtFirstName.Size = new System.Drawing.Size(297, 20);
            this.txtFirstName.TabIndex = 22;
            // 
            // txtUsername
            // 
            this.txtUsername.Location = new System.Drawing.Point(109, 67);
            this.txtUsername.Name = "txtUsername";
            this.txtUsername.Size = new System.Drawing.Size(297, 20);
            this.txtUsername.TabIndex = 21;
            // 
            // panelControl1
            // 
            this.panelControl1.Appearance.BackColor = System.Drawing.Color.RoyalBlue;
            this.panelControl1.Appearance.Options.UseBackColor = true;
            this.panelControl1.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder;
            this.panelControl1.Controls.Add(this.labelControl1);
            this.panelControl1.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelControl1.Location = new System.Drawing.Point(0, 0);
            this.panelControl1.Name = "panelControl1";
            this.panelControl1.Size = new System.Drawing.Size(431, 51);
            this.panelControl1.TabIndex = 38;
            // 
            // labelControl1
            // 
            this.labelControl1.Appearance.Font = new System.Drawing.Font("Consolas", 14.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelControl1.Appearance.ForeColor = System.Drawing.Color.White;
            this.labelControl1.Appearance.Options.UseFont = true;
            this.labelControl1.Appearance.Options.UseForeColor = true;
            this.labelControl1.Location = new System.Drawing.Point(20, 16);
            this.labelControl1.Name = "labelControl1";
            this.labelControl1.Size = new System.Drawing.Size(80, 22);
            this.labelControl1.TabIndex = 0;
            this.labelControl1.Text = "ADD USER";
            // 
            // groupControl1
            // 
            this.groupControl1.Controls.Add(this.textEdit1);
            this.groupControl1.Controls.Add(this.labelControl10);
            this.groupControl1.Controls.Add(this.txtPassword);
            this.groupControl1.Controls.Add(this.labelControl8);
            this.groupControl1.Location = new System.Drawing.Point(0, 249);
            this.groupControl1.Name = "groupControl1";
            this.groupControl1.Size = new System.Drawing.Size(431, 89);
            this.groupControl1.TabIndex = 39;
            // 
            // textEdit1
            // 
            this.textEdit1.Location = new System.Drawing.Point(109, 50);
            this.textEdit1.Name = "textEdit1";
            this.textEdit1.Size = new System.Drawing.Size(297, 20);
            this.textEdit1.TabIndex = 35;
            // 
            // labelControl10
            // 
            this.labelControl10.Location = new System.Drawing.Point(12, 53);
            this.labelControl10.Name = "labelControl10";
            this.labelControl10.Size = new System.Drawing.Size(84, 13);
            this.labelControl10.TabIndex = 36;
            this.labelControl10.Text = "Confirm Password";
            // 
            // dxErrorProvider1
            // 
            this.dxErrorProvider1.ContainerControl = this;
            // 
            // frmEditUser
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(431, 401);
            this.Controls.Add(this.groupControl1);
            this.Controls.Add(this.panelControl1);
            this.Controls.Add(this.btnSave);
            this.Controls.Add(this.labelControl9);
            this.Controls.Add(this.slueRole);
            this.Controls.Add(this.labelControl7);
            this.Controls.Add(this.txtSalutation);
            this.Controls.Add(this.labelControl6);
            this.Controls.Add(this.txtNameExt);
            this.Controls.Add(this.labelControl5);
            this.Controls.Add(this.txtLastName);
            this.Controls.Add(this.labelControl4);
            this.Controls.Add(this.txtMiddleName);
            this.Controls.Add(this.labelControl3);
            this.Controls.Add(this.labelControl2);
            this.Controls.Add(this.txtFirstName);
            this.Controls.Add(this.txtUsername);
            this.Name = "frmEditUser";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Load += new System.EventHandler(this.frmEditUser_Load);
            ((System.ComponentModel.ISupportInitialize)(this.slueRole.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.searchLookUpEdit1View)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.txtPassword.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.txtSalutation.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.txtNameExt.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.txtLastName.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.txtMiddleName.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.txtFirstName.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.txtUsername.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.panelControl1)).EndInit();
            this.panelControl1.ResumeLayout(false);
            this.panelControl1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.groupControl1)).EndInit();
            this.groupControl1.ResumeLayout(false);
            this.groupControl1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.textEdit1.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dxErrorProvider1)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private DevExpress.XtraEditors.SimpleButton btnSave;
        private DevExpress.XtraEditors.LabelControl labelControl9;
        private DevExpress.XtraEditors.SearchLookUpEdit slueRole;
        private DevExpress.XtraGrid.Views.Grid.GridView searchLookUpEdit1View;
        private DevExpress.XtraGrid.Columns.GridColumn gridColumn1;
        private DevExpress.XtraEditors.LabelControl labelControl8;
        private DevExpress.XtraEditors.TextEdit txtPassword;
        private DevExpress.XtraEditors.LabelControl labelControl7;
        private DevExpress.XtraEditors.TextEdit txtSalutation;
        private DevExpress.XtraEditors.LabelControl labelControl6;
        private DevExpress.XtraEditors.TextEdit txtNameExt;
        private DevExpress.XtraEditors.LabelControl labelControl5;
        private DevExpress.XtraEditors.TextEdit txtLastName;
        private DevExpress.XtraEditors.LabelControl labelControl4;
        private DevExpress.XtraEditors.TextEdit txtMiddleName;
        private DevExpress.XtraEditors.LabelControl labelControl3;
        private DevExpress.XtraEditors.LabelControl labelControl2;
        private DevExpress.XtraEditors.TextEdit txtFirstName;
        private DevExpress.XtraEditors.TextEdit txtUsername;
        private DevExpress.XtraEditors.PanelControl panelControl1;
        private DevExpress.XtraEditors.LabelControl labelControl1;
        private DevExpress.XtraEditors.GroupControl groupControl1;
        private DevExpress.XtraEditors.TextEdit textEdit1;
        private DevExpress.XtraEditors.LabelControl labelControl10;
        private DevExpress.XtraEditors.DXErrorProvider.DXErrorProvider dxErrorProvider1;
    }
}