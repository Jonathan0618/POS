using POS.Core;
using POS.Models.Store;
using POS.Services.Credentials;
using POS.Services.Security;
using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace POS.Forms
{
   
    public partial class SignIn : DevExpress.XtraEditors.XtraForm
    {
        private readonly UserService _userService;
        private readonly RoleService _roleService;
        private readonly DevExpress.XtraEditors.LabelControl _capsLockWarning;
        private readonly DevExpress.XtraEditors.MarqueeProgressBarControl _signInProgress;
        private bool _authenticationInProgress;

        public SignIn() : this(new UserService(), new RoleService())
        {
        }

        public SignIn(UserService userService, RoleService roleService)
        {
            InitializeComponent();
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _roleService = roleService ?? throw new ArgumentNullException(nameof(roleService));
            _capsLockWarning = new DevExpress.XtraEditors.LabelControl
            {
                Text = "Caps Lock is on",
                Location = new System.Drawing.Point(470, 345),
                Visible = false
            };
            _capsLockWarning.Appearance.ForeColor = System.Drawing.Color.IndianRed;
            _capsLockWarning.Appearance.Options.UseForeColor = true;
            _signInProgress = new DevExpress.XtraEditors.MarqueeProgressBarControl
            {
                Location = new System.Drawing.Point(282, 420),
                Size = new System.Drawing.Size(316, 20),
                Text = "Signing in...",
                Visible = false
            };
            _signInProgress.Properties.ShowTitle = true;
            sidePanel2.Controls.Add(_capsLockWarning);
            sidePanel2.Controls.Add(_signInProgress);
            txtPassword.KeyDown += UpdateCapsLockWarning;
            txtPassword.KeyUp += UpdateCapsLockWarning;
            this.AcceptButton = btnSignIn;
        }

        private void labelControl5_Click(object sender, EventArgs e)
        {

        }

        private async void btnSignIn_Click(object sender, EventArgs e)
        {
            if (_authenticationInProgress)
                return;

            var username = txtUserName.Text.Trim();
            if (username.Length == 0 || txtPassword.Text.Length == 0)
            {
                MessageBox.Show("Enter both username and password.", "Sign in", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                (username.Length == 0 ? txtUserName : txtPassword).Focus();
                return;
            }

            SetAuthenticationBusy(true);
            try
            {
                var result = await _userService.AuthenticateAsync(username, txtPassword.Text);
                if (!result.Succeeded)
                {
                    MessageBox.Show(result.Errors[0], "Sign in", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (chkRememberMe.Checked)
                    CredentialStore.SaveUsername(username.Trim());
                else
                    CredentialStore.ClearCredentials();

                _roleService.SetupClaims(CurrentUser.RoleId, username);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch
            {
                MessageBox.Show(
                    "Sign in could not be completed. Please try again.",
                    "Sign in",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                if (DialogResult != DialogResult.OK)
                    SetAuthenticationBusy(false);
            }
        }

        private void SetAuthenticationBusy(bool busy)
        {
            _authenticationInProgress = busy;
            txtUserName.Enabled = !busy;
            txtPassword.Enabled = !busy;
            chkRememberMe.Enabled = !busy;
            btnSignIn.Enabled = !busy;
            _signInProgress.Visible = busy;
        }

        private void UpdateCapsLockWarning(object sender, KeyEventArgs e)
        {
            _capsLockWarning.Visible = Control.IsKeyLocked(Keys.CapsLock);
        }

        private void sidePanel2_Click(object sender, EventArgs e)
        {

        }

        private void SignIn_Load(object sender, EventArgs e)
        {
            var username = CredentialStore.LoadUsername();

            if (!string.IsNullOrWhiteSpace(username))
            {
                txtUserName.Text = username;
                chkRememberMe.Checked = true;
                txtPassword.Focus();
            }
            _capsLockWarning.Visible = Control.IsKeyLocked(Keys.CapsLock);
        }
    }
}
