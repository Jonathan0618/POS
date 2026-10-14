using DevExpress.XtraEditors;
using DevExpress.XtraEditors.DXErrorProvider;
using DevExpress.XtraLayout;
using POS.Models.Security;
using POS.Services.Security;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace POS.Forms.Settings
{
    /// <summary>Creates or edits an employee's POS user account.</summary>
    public sealed class frmRegisterEdit : XtraForm
    {
        private readonly UserService _users;
        private readonly string _userId;
        private readonly TextEdit txtUsername = new TextEdit();
        private readonly ComboBoxEdit lueSalutation = new ComboBoxEdit();
        private readonly TextEdit txtFirstName = new TextEdit();
        private readonly TextEdit txtMiddleName = new TextEdit();
        private readonly TextEdit txtLastName = new TextEdit();
        private readonly TextEdit txtNameExtension = new TextEdit();
        private readonly LookUpEdit lueRole = new LookUpEdit();
        private readonly TextEdit txtPassword = PasswordEdit();
        private readonly TextEdit txtConfirmPassword = PasswordEdit();
        private readonly LabelControl lblPasswordHint = new LabelControl { AutoSizeMode = LabelAutoSizeMode.None };
        private readonly LabelControl lblState = new LabelControl { AutoSizeMode = LabelAutoSizeMode.None };
        private readonly SimpleButton btnSave = new SimpleButton { Text = "Save Employee" };
        private readonly SimpleButton btnCancel = new SimpleButton { Text = "Cancel" };
        private readonly DXErrorProvider dxError = new DXErrorProvider();
        private bool _busy;
        private bool _loaded;

        public frmRegisterEdit(UserService users, string userId = null)
        {
            _users = users ?? throw new ArgumentNullException(nameof(users));
            _userId = string.IsNullOrWhiteSpace(userId) ? null : userId;
            BuildUi();
        }

        private bool IsEdit => _userId != null;

        private void BuildUi()
        {
            Name = "frmRegisterEdit";
            Text = IsEdit ? "Edit Employee Account" : "Register Employee";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new System.Drawing.Size(570, 520);

            txtUsername.Properties.MaxLength = 100;
            txtFirstName.Properties.MaxLength = 100;
            txtMiddleName.Properties.MaxLength = 100;
            txtLastName.Properties.MaxLength = 100;
            txtNameExtension.Properties.MaxLength = 20;
            lueSalutation.Properties.Items.AddRange(new object[] { "Mr.", "Ms.", "Mrs.", "Dr." });
            lueSalutation.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.Standard;
            lueRole.Properties.DisplayMember = "Name";
            lueRole.Properties.ValueMember = "RoleId";
            lueRole.Properties.NullText = "Select a role";
            lueRole.Properties.ShowHeader = false;
            lblPasswordHint.Text = IsEdit
                ? "Leave both password fields empty to keep the current password."
                : "Create a password for the employee's first sign-in.";
            lblState.Text = "Loading roles...";

            var layout = new LayoutControl { Dock = DockStyle.Fill };
            Controls.Add(layout);
            layout.Controls.AddRange(new Control[]
            {
                txtUsername, lueSalutation, txtFirstName, txtMiddleName, txtLastName,
                txtNameExtension, lueRole, txtPassword, txtConfirmPassword,
                lblPasswordHint, lblState, btnSave, btnCancel
            });
            var root = new LayoutControlGroup { EnableIndentsWithoutBorders = DevExpress.Utils.DefaultBoolean.True };
            layout.Root = root;
            root.AddItem("Username", txtUsername);
            root.AddItem("Salutation", lueSalutation);
            root.AddItem("First name", txtFirstName);
            root.AddItem("Middle name", txtMiddleName);
            root.AddItem("Last name", txtLastName);
            root.AddItem("Name suffix", txtNameExtension);
            root.AddItem("Role", lueRole);
            root.AddItem(IsEdit ? "New password" : "Password", txtPassword);
            root.AddItem("Confirm password", txtConfirmPassword);
            var hintItem = root.AddItem(string.Empty, lblPasswordHint);
            hintItem.TextVisible = false;
            hintItem.MinSize = new System.Drawing.Size(0, 35);
            var stateItem = root.AddItem(string.Empty, lblState);
            stateItem.TextVisible = false;
            stateItem.MinSize = new System.Drawing.Size(0, 35);
            var buttons = new LayoutControlGroup { GroupBordersVisible = false };
            buttons.AddItem(string.Empty, btnSave).TextVisible = false;
            buttons.AddItem(string.Empty, btnCancel).TextVisible = false;
            root.Add(buttons);

            dxError.ContainerControl = this;
            AcceptButton = btnSave;
            CancelButton = btnCancel;
            btnCancel.DialogResult = DialogResult.Cancel;
            btnSave.Click += async (sender, args) => await SaveAsync();
            Shown += async (sender, args) => await LoadAsync();
        }

        private async Task LoadAsync()
        {
            if (_busy) return;
            try
            {
                SetBusy(true, "Loading employee account...");
                lueRole.Properties.DataSource = _users.GetRoles().OrderBy(x => x.Name).ToList();
                if (IsEdit)
                {
                    var user = await _users.GetUserByIdAsync(_userId);
                    if (user == null) throw new ValidationException("The selected employee account no longer exists.");
                    txtUsername.Text = user.Username;
                    lueSalutation.Text = user.Salutation;
                    txtFirstName.Text = user.FirstName;
                    txtMiddleName.Text = user.MiddleName;
                    txtLastName.Text = user.LastName;
                    txtNameExtension.Text = user.NameExt ?? user.NameExtension;
                    lueRole.EditValue = user.Role;
                }
                _loaded = true;
                lblState.Text = IsEdit ? "Employee account loaded." : "Enter the employee's account details.";
            }
            catch (Exception exception)
            {
                _loaded = false;
                lblState.Text = SafeMessage(exception);
                ShowError(exception);
            }
            finally
            {
                if (!IsDisposed) SetBusy(false, lblState.Text);
            }
        }

        private async Task SaveAsync()
        {
            if (_busy || !_loaded || !ValidateInput()) return;
            try
            {
                SetBusy(true, IsEdit ? "Saving employee account..." : "Registering employee...");
                if (IsEdit)
                {
                    await _users.UpdateUserAsync(new EditUserDTO
                    {
                        UserId = _userId,
                        Username = txtUsername.Text.Trim(),
                        Salutation = lueSalutation.Text.Trim(),
                        FirstName = txtFirstName.Text.Trim(),
                        MiddleName = Clean(txtMiddleName.Text),
                        LastName = txtLastName.Text.Trim(),
                        NameExtension = Clean(txtNameExtension.Text) ?? "N/A",
                        NameExt = Clean(txtNameExtension.Text),
                        Role = Convert.ToString(lueRole.EditValue),
                        Password = txtPassword.Text,
                        ConfirmPassword = txtConfirmPassword.Text
                    });
                }
                else
                {
                    await _users.AddUserAsync(new UserDTO
                    {
                        Username = txtUsername.Text.Trim(),
                        Salutation = lueSalutation.Text.Trim(),
                        FirstName = txtFirstName.Text.Trim(),
                        MiddleName = Clean(txtMiddleName.Text),
                        LastName = txtLastName.Text.Trim(),
                        NameExtension = Clean(txtNameExtension.Text) ?? "N/A",
                        NameExt = Clean(txtNameExtension.Text),
                        Role = Convert.ToString(lueRole.EditValue),
                        Password = txtPassword.Text
                    });
                }
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception exception)
            {
                lblState.Text = SafeMessage(exception);
                ShowError(exception);
            }
            finally
            {
                if (!IsDisposed) SetBusy(false, lblState.Text);
            }
        }

        private bool ValidateInput()
        {
            dxError.ClearErrors();
            Require(txtUsername, "Username is required.");
            if (string.IsNullOrWhiteSpace(lueSalutation.Text))
                dxError.SetError(lueSalutation, "Salutation is required.");
            Require(txtFirstName, "First name is required.");
            Require(txtLastName, "Last name is required.");
            if (lueRole.EditValue == null || string.IsNullOrWhiteSpace(Convert.ToString(lueRole.EditValue)))
                dxError.SetError(lueRole, "Select a role.");
            if (!IsEdit && string.IsNullOrEmpty(txtPassword.Text))
                dxError.SetError(txtPassword, "Password is required for a new employee.");
            if (!string.Equals(txtPassword.Text, txtConfirmPassword.Text, StringComparison.Ordinal))
                dxError.SetError(txtConfirmPassword, "Passwords do not match.");
            return !dxError.HasErrors;
        }

        private void Require(TextEdit editor, string message)
        {
            if (string.IsNullOrWhiteSpace(editor.Text)) dxError.SetError(editor, message);
        }

        private void SetBusy(bool busy, string state)
        {
            _busy = busy;
            txtUsername.Enabled = !busy;
            lueSalutation.Enabled = !busy;
            txtFirstName.Enabled = !busy;
            txtMiddleName.Enabled = !busy;
            txtLastName.Enabled = !busy;
            txtNameExtension.Enabled = !busy;
            lueRole.Enabled = !busy;
            txtPassword.Enabled = !busy;
            txtConfirmPassword.Enabled = !busy;
            btnSave.Enabled = !busy && _loaded;
            btnCancel.Enabled = !busy;
            UseWaitCursor = busy;
            lblState.Text = state;
        }

        private static TextEdit PasswordEdit()
        {
            var editor = new TextEdit();
            editor.Properties.UseSystemPasswordChar = true;
            return editor;
        }

        private static string Clean(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string SafeMessage(Exception exception)
        {
            return exception is ValidationException || exception is UnauthorizedAccessException || exception is InvalidOperationException
                ? exception.Message
                : "The employee account could not be saved. Please try again.";
        }

        private void ShowError(Exception exception)
        {
            XtraMessageBox.Show(this, SafeMessage(exception), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
