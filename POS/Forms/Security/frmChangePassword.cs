using DevExpress.XtraEditors;
using DevExpress.XtraLayout;
using POS.Core;
using POS.Services.Security;
using System;
using System.Windows.Forms;

namespace POS.Forms.Security
{
    public sealed class frmChangePassword : XtraForm
    {
        private readonly UserService _userService;
        private readonly TextEdit _currentPassword = CreatePasswordEdit();
        private readonly TextEdit _newPassword = CreatePasswordEdit();
        private readonly TextEdit _confirmPassword = CreatePasswordEdit();
        private readonly SimpleButton _saveButton = new SimpleButton { Text = "Change Password" };
        private TextBox newPassword;
        private TextBox confirmPassword;
        private Label Title;
        private Button confirm;
        private Label label1;
        private Label label2;
        private readonly SimpleButton _cancelButton = new SimpleButton { Text = "Cancel" };

        public frmChangePassword(UserService userService)
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            Text = "Change Password";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Width = 430;
            Height = 245;

            var layout = new LayoutControl { Dock = DockStyle.Fill };
            Controls.Add(layout);
            layout.Controls.AddRange(new Control[]
            {
                _currentPassword, _newPassword, _confirmPassword, _saveButton, _cancelButton
            });

            var root = new LayoutControlGroup { EnableIndentsWithoutBorders = DevExpress.Utils.DefaultBoolean.True };
            layout.Root = root;
            root.AddItem("Current password", _currentPassword);
            root.AddItem("New password", _newPassword);
            root.AddItem("Confirm new password", _confirmPassword);
            var buttons = new LayoutControlGroup { GroupBordersVisible = false };
            buttons.AddItem(string.Empty, _saveButton).TextVisible = false;
            buttons.AddItem(string.Empty, _cancelButton).TextVisible = false;
            root.Add(buttons);

            _saveButton.Click += SavePassword;
            _cancelButton.Click += (sender, args) => Close();
            AcceptButton = _saveButton;
            CancelButton = _cancelButton;
        }

        private async void SavePassword(object sender, EventArgs e)
        {
            if (_newPassword.Text != _confirmPassword.Text)
            {
                XtraMessageBox.Show("The new passwords do not match.", "Change Password", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Enabled = false;
            try
            {
                var result = await _userService.ChangePasswordAsync(
                    CurrentUser.UserId,
                    _currentPassword.Text,
                    _newPassword.Text);
                if (!result.Succeeded)
                {
                    XtraMessageBox.Show(string.Join(Environment.NewLine, result.Errors), "Change Password", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                XtraMessageBox.Show("Password changed successfully.", "Change Password", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch
            {
                XtraMessageBox.Show("The password could not be changed. Please try again.", "Change Password", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (DialogResult != DialogResult.OK) Enabled = true;
            }
        }

        private static TextEdit CreatePasswordEdit()
        {
            var edit = new TextEdit();
            edit.Properties.UseSystemPasswordChar = true;
            return edit;
        }

        private void InitializeComponent()
        {
            this.newPassword = new System.Windows.Forms.TextBox();
            this.confirmPassword = new System.Windows.Forms.TextBox();
            this.Title = new System.Windows.Forms.Label();
            this.confirm = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // newPassword
            // 
            this.newPassword.Font = new System.Drawing.Font("Segoe UI", 15F);
            this.newPassword.Location = new System.Drawing.Point(96, 146);
            this.newPassword.Name = "newPassword";
            this.newPassword.Size = new System.Drawing.Size(263, 34);
            this.newPassword.TabIndex = 0;
            this.newPassword.UseWaitCursor = true;
            this.newPassword.TextChanged += new System.EventHandler(this.newPassword_TextChanged);
            // 
            // confirmPassword
            // 
            this.confirmPassword.Font = new System.Drawing.Font("Segoe UI", 15F);
            this.confirmPassword.Location = new System.Drawing.Point(96, 226);
            this.confirmPassword.Name = "confirmPassword";
            this.confirmPassword.Size = new System.Drawing.Size(263, 34);
            this.confirmPassword.TabIndex = 1;
            this.confirmPassword.UseWaitCursor = true;
            this.confirmPassword.TextChanged += new System.EventHandler(this.confirmPassword_TextChanged);
            // 
            // Title
            // 
            this.Title.AutoSize = true;
            this.Title.Font = new System.Drawing.Font("Segoe UI", 15F, System.Drawing.FontStyle.Bold);
            this.Title.ForeColor = System.Drawing.Color.White;
            this.Title.Location = new System.Drawing.Point(149, 49);
            this.Title.Name = "Title";
            this.Title.Size = new System.Drawing.Size(177, 28);
            this.Title.TabIndex = 2;
            this.Title.Text = "Change Password";
            this.Title.UseWaitCursor = true;
            this.Title.Click += new System.EventHandler(this.label1_Click);
            // 
            // confirm
            // 
            this.confirm.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.confirm.Location = new System.Drawing.Point(154, 272);
            this.confirm.Name = "confirm";
            this.confirm.Size = new System.Drawing.Size(114, 23);
            this.confirm.TabIndex = 3;
            this.confirm.Text = "Confirm Password";
            this.confirm.UseVisualStyleBackColor = true;
            this.confirm.UseWaitCursor = true;
            this.confirm.Click += new System.EventHandler(this.confirm_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.label1.ForeColor = System.Drawing.Color.White;
            this.label1.Location = new System.Drawing.Point(102, 117);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(107, 19);
            this.label1.TabIndex = 4;
            this.label1.Text = "New Password";
            this.label1.UseWaitCursor = true;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.label2.ForeColor = System.Drawing.Color.White;
            this.label2.Location = new System.Drawing.Point(102, 200);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(131, 19);
            this.label2.TabIndex = 5;
            this.label2.Text = "Confirm Password";
            this.label2.UseWaitCursor = true;
            // 
            // frmChangePassword
            // 
            this.Appearance.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(128)))), ((int)(((byte)(128)))), ((int)(((byte)(255)))));
            this.Appearance.Options.UseBackColor = true;
            this.ClientSize = new System.Drawing.Size(528, 385);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.confirm);
            this.Controls.Add(this.Title);
            this.Controls.Add(this.confirmPassword);
            this.Controls.Add(this.newPassword);
            this.Name = "frmChangePassword";
            this.Load += new System.EventHandler(this.frmChangePassword_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void newPassword_TextChanged(object sender, EventArgs e)
        {

        }

        private void confirm_Click(object sender, EventArgs e)
        {
            // The active DevExpress workflow is handled by SavePassword.
        }

        private void confirmPassword_TextChanged(object sender, EventArgs e)
        {

        }

        private void frmChangePassword_Load(object sender, EventArgs e)
        {

        }
    }
}
