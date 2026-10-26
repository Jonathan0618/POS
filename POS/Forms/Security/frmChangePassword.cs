using DevExpress.XtraEditors;
using DevExpress.XtraLayout;
using POS.Core;
using POS.Services.Security;
using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace POS.Forms.Security
{
    public sealed class frmChangePassword : XtraForm
    {
        private readonly UserService _userService;
        private readonly TextEdit _currentPassword = PasswordEdit();
        private readonly TextEdit _newPassword = PasswordEdit();
        private readonly TextEdit _confirmPassword = PasswordEdit();
        private readonly SimpleButton _saveButton = new SimpleButton { Text = "Change Password" };
        private readonly SimpleButton _cancelButton = new SimpleButton { Text = "Cancel" };
        private readonly LabelControl _state = new LabelControl { AutoSizeMode = LabelAutoSizeMode.None };
        private bool _busy;

        public frmChangePassword(UserService userService)
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            Name = "frmChangePassword"; Text = "Change Password"; StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; ClientSize = new System.Drawing.Size(440, 265);
            var layout = new LayoutControl { Dock = DockStyle.Fill }; Controls.Add(layout);
            layout.Controls.AddRange(new Control[] { _currentPassword, _newPassword, _confirmPassword, _state, _saveButton, _cancelButton });
            var root = new LayoutControlGroup { EnableIndentsWithoutBorders = DevExpress.Utils.DefaultBoolean.True }; layout.Root = root;
            root.AddItem("Current password", _currentPassword); root.AddItem("New password", _newPassword); root.AddItem("Confirm new password", _confirmPassword);
            var state = root.AddItem(string.Empty, _state); state.TextVisible = false; state.MinSize = new System.Drawing.Size(0, 28);
            var buttons = new LayoutControlGroup { GroupBordersVisible = false }; buttons.AddItem(string.Empty, _saveButton).TextVisible = false; buttons.AddItem(string.Empty, _cancelButton).TextVisible = false; root.Add(buttons);
            _saveButton.Click += async (s, e) => await SaveAsync(); _cancelButton.Click += (s, e) => Close();
            AcceptButton = _saveButton; CancelButton = _cancelButton;
        }

        private async Task SaveAsync()
        {
            if (_busy) return;
            if (string.IsNullOrWhiteSpace(_currentPassword.Text) || string.IsNullOrEmpty(_newPassword.Text)) { Warn("Current and new passwords are required."); return; }
            if (_newPassword.Text != _confirmPassword.Text) { Warn("The new passwords do not match."); return; }
            try
            {
                SetBusy(true, "Changing password...");
                var result = await _userService.ChangePasswordAsync(CurrentUser.UserId, _currentPassword.Text, _newPassword.Text);
                if (!result.Succeeded) { Warn(string.Join(Environment.NewLine, result.Errors)); return; }
                XtraMessageBox.Show(this, "Password changed successfully.", "Change Password", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.OK; Close();
            }
            catch { Warn("The password could not be changed. Please try again."); }
            finally { if (!IsDisposed) SetBusy(false, string.Empty); }
        }

        private void SetBusy(bool busy, string message) { _busy = busy; _currentPassword.Enabled = !busy; _newPassword.Enabled = !busy; _confirmPassword.Enabled = !busy; _saveButton.Enabled = !busy; _cancelButton.Enabled = !busy; _state.Text = message; UseWaitCursor = busy; }
        private void Warn(string message) { XtraMessageBox.Show(this, message, "Change Password", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        private static TextEdit PasswordEdit() { var edit = new TextEdit(); edit.Properties.UseSystemPasswordChar = true; return edit; }
    }
}
