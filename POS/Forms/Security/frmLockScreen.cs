using DevExpress.XtraEditors;
using DevExpress.XtraEditors.DXErrorProvider;
using DevExpress.XtraLayout;
using POS.Services.Security;
using System;
using System.Drawing;
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
        private readonly LabelControl _capsLockWarning = new LabelControl { Text = "Caps Lock is on" };
        private readonly LabelControl _error = new LabelControl { AutoSizeMode = LabelAutoSizeMode.None };
        private readonly DXErrorProvider _errorProvider = new DXErrorProvider();
        private bool _busy;

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
            Width = 460;
            Height = 270;
            TopMost = true;
            _password.Properties.UseSystemPasswordChar = true;
            _password.Properties.NullValuePrompt = "Enter your password";
            _capsLockWarning.Appearance.ForeColor = Color.DarkOrange;
            _error.Appearance.ForeColor = Color.Firebrick;
            _error.Appearance.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;
            _error.Text = string.Empty;

            var layout = new LayoutControl { Dock = DockStyle.Fill };
            Controls.Add(layout);
            layout.Controls.AddRange(new Control[]
            {
                _password, _unlockButton, _logoutButton, _capsLockWarning, _error
            });
            var root = new LayoutControlGroup { EnableIndentsWithoutBorders = DevExpress.Utils.DefaultBoolean.True };
            layout.Root = root;
            var heading = new LabelControl { Text = "Session Locked" };
            heading.Appearance.Font = new Font(heading.Font.FontFamily, 16, FontStyle.Bold);
            layout.Controls.Add(heading);
            root.AddItem(string.Empty, heading).TextVisible = false;
            root.AddItem("Signed in as", new LabelControl { Text = _username });
            root.AddItem("Password", _password);
            root.AddItem(string.Empty, _capsLockWarning).TextVisible = false;
            var errorItem = root.AddItem(string.Empty, _error);
            errorItem.TextVisible = false;
            errorItem.MinSize = new Size(0, 36);
            var buttons = new LayoutControlGroup { GroupBordersVisible = false };
            buttons.AddItem(string.Empty, _unlockButton).TextVisible = false;
            buttons.AddItem(string.Empty, _logoutButton).TextVisible = false;
            root.Add(buttons);

            _errorProvider.ContainerControl = this;
            _unlockButton.Click += Unlock;
            _logoutButton.Click += (sender, args) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };
            AcceptButton = _unlockButton;
            CancelButton = _logoutButton;
            _password.EditValueChanged += (sender, args) =>
            {
                _errorProvider.SetError(_password, string.Empty);
                _error.Text = string.Empty;
            };
            _password.KeyDown += (sender, args) => UpdateCapsLockWarning();
            _password.KeyUp += (sender, args) => UpdateCapsLockWarning();
            Activated += (sender, args) => UpdateCapsLockWarning();
            Shown += (sender, args) =>
            {
                UpdateCapsLockWarning();
                _password.Focus();
            };
        }

        private async void Unlock(object sender, EventArgs e)
        {
            if (_busy) return;
            if (string.IsNullOrWhiteSpace(_password.Text))
            {
                _errorProvider.SetError(_password, "Password is required.");
                _error.Text = "Enter the password for the locked session.";
                _password.Focus();
                return;
            }

            SetBusy(true);
            try
            {
                var result = await _userService.AuthenticateAsync(_username, _password.Text);
                if (!result.Succeeded)
                {
                    _error.Text = "The session could not be unlocked. Check the password and try again.";
                    _errorProvider.SetError(_password, "Unable to unlock this session.");
                    _password.Text = string.Empty;
                    _password.SelectAll();
                    _password.Focus();
                    return;
                }

                _roleService.SetupClaims(null, _username);
                _userService.RecordSessionUnlocked(result.Value, _username);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch
            {
                _error.Text = "The register could not be unlocked. Check the connection and try again.";
                _password.SelectAll();
                _password.Focus();
            }
            finally
            {
                if (DialogResult != DialogResult.OK) SetBusy(false);
            }
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            _password.Enabled = !busy;
            _unlockButton.Enabled = !busy;
            _logoutButton.Enabled = !busy;
            UseWaitCursor = busy;
        }

        private void UpdateCapsLockWarning()
        {
            _capsLockWarning.Visible = Control.IsKeyLocked(Keys.CapsLock);
        }
    }
}
