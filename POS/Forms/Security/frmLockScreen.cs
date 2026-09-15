using DevExpress.XtraEditors;
using DevExpress.XtraLayout;
using POS.Services.Security;
using System;
using System.Windows.Forms;

namespace POS.Forms.Security
{
    public sealed class frmLockScreen : XtraForm
    {
        private readonly UserService _userService;
        private readonly RoleService _roleService;
        private readonly string _username;
        private readonly TextEdit _password = new TextEdit();
        private readonly SimpleButton _unlockButton = new SimpleButton { Text = "Unlock" };
        private readonly SimpleButton _logoutButton = new SimpleButton { Text = "Log Out" };

        public frmLockScreen(UserService userService, RoleService roleService, string username)
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _roleService = roleService ?? throw new ArgumentNullException(nameof(roleService));
            _username = !string.IsNullOrWhiteSpace(username)
                ? username
                : throw new ArgumentException("A username is required.", nameof(username));

            Text = "Register Locked";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            ControlBox = false;
            Width = 420;
            Height = 190;
            TopMost = true;
            _password.Properties.UseSystemPasswordChar = true;

            var layout = new LayoutControl { Dock = DockStyle.Fill };
            Controls.Add(layout);
            layout.Controls.AddRange(new Control[] { _password, _unlockButton, _logoutButton });
            var root = new LayoutControlGroup { EnableIndentsWithoutBorders = DevExpress.Utils.DefaultBoolean.True };
            layout.Root = root;
            root.AddItem("User", new LabelControl { Text = _username });
            root.AddItem("Password", _password);
            var buttons = new LayoutControlGroup { GroupBordersVisible = false };
            buttons.AddItem(string.Empty, _unlockButton).TextVisible = false;
            buttons.AddItem(string.Empty, _logoutButton).TextVisible = false;
            root.Add(buttons);

            _unlockButton.Click += Unlock;
            _logoutButton.Click += (sender, args) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };
            AcceptButton = _unlockButton;
            Shown += (sender, args) => _password.Focus();
        }

        private async void Unlock(object sender, EventArgs e)
        {
            Enabled = false;
            try
            {
                var result = await _userService.AuthenticateAsync(_username, _password.Text);
                if (!result.Succeeded)
                {
                    XtraMessageBox.Show(string.Join(Environment.NewLine, result.Errors), "Register Locked", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    _password.SelectAll();
                    return;
                }

                _roleService.SetupClaims(null, _username);
                _userService.RecordSessionUnlocked(result.Value, _username);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch
            {
                XtraMessageBox.Show("The register could not be unlocked. Please try again.", "Register Locked", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (DialogResult != DialogResult.OK) Enabled = true;
            }
        }
    }
}
