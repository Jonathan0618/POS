using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using POS.Common.Enumerations;
using POS.Core.Security;
using POS.Domains.Security;
using POS.Models.Security;
using POS.Services;
using POS.Services.Security;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace POS.Forms.Security
{
    public sealed class ucRolesPermissions : XtraUserControl
    {
        private const string AdministratorRole = "System Administrator";
        private readonly RoleService _service;
        private readonly ListBoxControl _roles = new ListBoxControl();
        private readonly TextEdit _roleName = new TextEdit();
        private readonly SimpleButton _add = new SimpleButton { Text = "Add Role" };
        private readonly SimpleButton _rename = new SimpleButton { Text = "Rename" };
        private readonly SimpleButton _delete = new SimpleButton { Text = "Delete" };
        private readonly SimpleButton _save = new SimpleButton { Text = "Save Permissions" };
        private readonly SimpleButton _reload = new SimpleButton { Text = "Reload" };
        private readonly LabelControl _notice = new LabelControl { AutoSizeMode = LabelAutoSizeMode.None };
        private readonly GridControl _permissions = new GridControl();
        private readonly GridView _view = new GridView();
        private readonly bool _canAdd;
        private readonly bool _canEdit;
        private readonly bool _canDelete;
        private bool _loading;

        public ucRolesPermissions(RoleService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _canAdd = AuthorizationService.HasPermission(ResourceCodes.Roles, ClaimActionType.Add);
            _canEdit = AuthorizationService.HasPermission(ResourceCodes.Roles, ClaimActionType.Edit);
            _canDelete = AuthorizationService.HasPermission(ResourceCodes.Roles, ClaimActionType.Delete);
            BuildUi();
        }

        private void BuildUi()
        {
            Dock = DockStyle.Fill;
            var split = new SplitContainerControl { Dock = DockStyle.Fill, SplitterPosition = 290 };
            var roleHeader = new PanelControl { Dock = DockStyle.Top, Height = 104 };
            roleHeader.Controls.Add(new LabelControl { Text = "Role name", Location = new Point(10, 9) });
            _roleName.Location = new Point(10, 28);
            _roleName.Size = new Size(170, 24);
            _add.Location = new Point(185, 26);
            _rename.Location = new Point(10, 62);
            _delete.Location = new Point(95, 62);
            _reload.Location = new Point(180, 62);
            roleHeader.Controls.AddRange(new Control[] { _roleName, _add, _rename, _delete, _reload });
            _roles.Dock = DockStyle.Fill;
            _roles.DisplayMember = "Name";
            split.Panel1.Controls.Add(_roles);
            split.Panel1.Controls.Add(roleHeader);

            var permissionHeader = new PanelControl { Dock = DockStyle.Top, Height = 64 };
            _notice.Dock = DockStyle.Fill;
            _notice.Padding = new Padding(8);
            _notice.Text = "Select a role. Permission changes take effect at the user's next sign-in.";
            _save.Dock = DockStyle.Right;
            _save.Width = 145;
            permissionHeader.Controls.Add(_notice);
            permissionHeader.Controls.Add(_save);
            _permissions.Dock = DockStyle.Fill;
            _permissions.MainView = _view;
            _permissions.ViewCollection.Add(_view);
            _view.OptionsView.ShowGroupPanel = false;
            _view.OptionsView.ShowAutoFilterRow = true;
            _view.OptionsBehavior.Editable = _canEdit;
            split.Panel2.Controls.Add(_permissions);
            split.Panel2.Controls.Add(permissionHeader);
            Controls.Add(split);

            _roles.SelectedIndexChanged += (sender, args) => LoadPermissions();
            _add.Click += AddRole;
            _rename.Click += RenameRole;
            _delete.Click += DeleteRole;
            _reload.Click += (sender, args) => LoadRoles();
            _save.Click += SavePermissions;
            Load += (sender, args) => LoadRoles();
        }

        private void LoadRoles()
        {
            RunSafe(() =>
            {
                var selectedId = (_roles.SelectedItem as RoleDTO)?.RoleId;
                var roles = _service.GetRoles().OrderBy(x => x.Name).ToList();
                _roles.DataSource = new BindingList<RoleDTO>(roles);
                if (selectedId != null)
                {
                    var index = roles.FindIndex(x => x.RoleId == selectedId);
                    if (index >= 0) _roles.SelectedIndex = index;
                }
                UpdateActionState();
            });
            LoadPermissions();
        }

        private void LoadPermissions()
        {
            if (_loading) return;
            RunSafe(() =>
            {
                var role = _roles.SelectedItem as RoleDTO;
                _permissions.DataSource = role == null
                    ? new BindingList<RoleClaimDTO>()
                    : new BindingList<RoleClaimDTO>(_service.GetClaims(role.RoleId).ToList());
                HideColumn("RoleClaimId");
                HideColumn("ModuleId");
                HideColumn("RoleId");
                var name = _view.Columns.ColumnByFieldName("Name");
                if (name != null) { name.Caption = "Resource"; name.OptionsColumn.AllowEdit = false; }
                _view.BestFitColumns();
                UpdateActionState();
            });
        }

        private void UpdateActionState()
        {
            var role = _roles.SelectedItem as RoleDTO;
            var administrator = role != null && string.Equals(role.Name, AdministratorRole, StringComparison.OrdinalIgnoreCase);
            _add.Enabled = _canAdd && !_loading;
            _rename.Enabled = _canEdit && role != null && !administrator && !_loading;
            _delete.Enabled = _canDelete && role != null && !administrator && !_loading;
            _save.Enabled = _canEdit && role != null && !_loading;
            _notice.Text = administrator
                ? "System Administrator is protected and cannot be renamed or deleted. Permission changes take effect at next sign-in."
                : "Permission changes take effect at the user's next sign-in.";
        }

        private void AddRole(object sender, EventArgs e)
        {
            var name = _roleName.Text.Trim();
            if (name.Length == 0) { ShowMessage("Role name is required."); return; }
            var refresh = false;
            RunSafe(() =>
            {
                var result = _service.AddRole(new RoleDTO { Name = name });
                if (!result.Succeeded) { ShowMessage(string.Join(Environment.NewLine, result.Errors)); return; }
                _roleName.Text = string.Empty;
                refresh = true;
            });
            if (refresh) LoadRoles();
        }

        private void RenameRole(object sender, EventArgs e)
        {
            var role = _roles.SelectedItem as RoleDTO;
            if (role == null) return;
            var name = XtraInputBox.Show("Enter the new role name:", "Rename role", role.Name);
            if (string.IsNullOrWhiteSpace(name)) return;
            var refresh = false;
            RunSafe(() =>
            {
                var result = _service.UpdateRole(new RoleDTO { RoleId = role.RoleId, Name = name });
                if (!result.Succeeded) { ShowMessage(string.Join(Environment.NewLine, result.Errors)); return; }
                refresh = true;
            });
            if (refresh) LoadRoles();
        }

        private void DeleteRole(object sender, EventArgs e)
        {
            var role = _roles.SelectedItem as RoleDTO;
            if (role == null) return;
            if (XtraMessageBox.Show("Delete role '" + role.Name + "'?", "Roles", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            var refresh = false;
            RunSafe(() =>
            {
                var result = _service.DeleteRole(role.RoleId);
                if (!result.Succeeded) { ShowMessage(string.Join(Environment.NewLine, result.Errors)); return; }
                refresh = true;
            });
            if (refresh) LoadRoles();
        }

        private void SavePermissions(object sender, EventArgs e)
        {
            var role = _roles.SelectedItem as RoleDTO;
            var rows = _permissions.DataSource as BindingList<RoleClaimDTO>;
            if (role == null || rows == null) return;
            if (XtraMessageBox.Show("Save the permission matrix for '" + role.Name + "'?", "Permissions", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            var saved = false;
            RunSafe(() =>
            {
                _view.PostEditor();
                _view.UpdateCurrentRow();
                foreach (var claim in rows) _service.UpdateClaim(claim);
                saved = true;
            });
            if (saved)
            {
                LoadPermissions();
                XtraMessageBox.Show("Permissions saved. They apply at the user's next sign-in.", "Permissions", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void RunSafe(Action action)
        {
            if (_loading) return;
            _loading = true;
            try { action(); }
            catch (Exception exception)
            {
                var message = exception is UnauthorizedAccessException || exception is InvalidOperationException
                    ? exception.Message : "The role operation could not be completed. Please reload and try again.";
                ShowMessage(message);
            }
            finally { _loading = false; UpdateActionState(); }
        }

        private void HideColumn(string fieldName)
        {
            var column = _view.Columns.ColumnByFieldName(fieldName);
            if (column != null) column.Visible = false;
        }

        private static void ShowMessage(string message)
        {
            XtraMessageBox.Show(message, "Roles and Permissions", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
