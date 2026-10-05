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
    }
}
