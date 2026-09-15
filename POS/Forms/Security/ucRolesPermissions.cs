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
using System.Linq;
using System.Windows.Forms;

namespace POS.Forms.Security
{
    public sealed class ucRolesPermissions : XtraUserControl
    {
        private readonly RoleService _service;
        private readonly ListBoxControl _roles = new ListBoxControl();
        private readonly TextEdit _roleName = new TextEdit();
        private readonly SimpleButton _addRole = new SimpleButton { Text = "Add Role" };
        private readonly SimpleButton _renameRole = new SimpleButton { Text = "Rename" };
        private readonly SimpleButton _deleteRole = new SimpleButton { Text = "Delete" };
        private readonly GridControl _permissions = new GridControl();
        private readonly GridView _permissionView = new GridView();
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
            Dock = DockStyle.Fill;

            var split = new SplitContainerControl { Dock = DockStyle.Fill, SplitterPosition = 280 };
            var roleHeader = new PanelControl { Dock = DockStyle.Top, Height = 72 };
            _roleName.Location = new System.Drawing.Point(10, 10);
            _roleName.Size = new System.Drawing.Size(175, 20);
            _roleName.Properties.NullValuePrompt = "New role name";
            _addRole.Location = new System.Drawing.Point(190, 8);
            _renameRole.Location = new System.Drawing.Point(10, 39);
            _deleteRole.Location = new System.Drawing.Point(95, 39);
            roleHeader.Controls.AddRange(new Control[] { _roleName, _addRole, _renameRole, _deleteRole });
            _roles.Dock = DockStyle.Fill;
            _roles.DisplayMember = "Name";
            split.Panel1.Controls.Add(_roles);
            split.Panel1.Controls.Add(roleHeader);

            _permissions.Dock = DockStyle.Fill;
            _permissions.MainView = _permissionView;
            _permissions.ViewCollection.Add(_permissionView);
            _permissionView.OptionsView.ShowGroupPanel = false;
            _permissionView.OptionsView.ShowAutoFilterRow = true;
            _permissionView.OptionsBehavior.Editable = _canEdit;
            _permissionView.RowUpdated += PermissionView_RowUpdated;
            var notice = new LabelControl
            {
                Dock = DockStyle.Top,
                Height = 28,
                Text = "Permission changes take effect at the user's next sign-in."
            };
            split.Panel2.Controls.Add(_permissions);
            split.Panel2.Controls.Add(notice);
            Controls.Add(split);

            _roles.SelectedIndexChanged += (sender, args) => LoadPermissions();
            _addRole.Click += AddRole;
            _renameRole.Click += RenameRole;
            _deleteRole.Click += DeleteRole;
            Load += (sender, args) => LoadRoles();
        }

        private void LoadRoles()
        {
            _loading = true;
            try
            {
                _roles.DataSource = new BindingList<RoleDTO>(_service.GetRoles().ToList());
                _addRole.Enabled = _canAdd;
                _renameRole.Enabled = _canEdit;
                _deleteRole.Enabled = _canDelete;
            }
            finally
            {
                _loading = false;
            }
            LoadPermissions();
        }

        private void LoadPermissions()
        {
            if (_loading) return;
            var role = _roles.SelectedItem as RoleDTO;
            _loading = true;
            try
            {
                _permissions.DataSource = role == null
                    ? new BindingList<RoleClaimDTO>()
                    : new BindingList<RoleClaimDTO>(_service.GetClaims(role.RoleId).ToList());
                _permissionView.BestFitColumns();
                SetColumnVisibility("RoleClaimId", false);
                SetColumnVisibility("ModuleId", false);
                SetColumnVisibility("RoleId", false);
                var resourceColumn = _permissionView.Columns.ColumnByFieldName("Name");
                if (resourceColumn != null)
                {
                    resourceColumn.Caption = "Resource";
                    resourceColumn.OptionsColumn.AllowEdit = false;
                }
            }
            finally
            {
                _loading = false;
            }
        }

        private void SetColumnVisibility(string fieldName, bool visible)
        {
            var column = _permissionView.Columns.ColumnByFieldName(fieldName);
            if (column != null) column.Visible = visible;
        }

        private void AddRole(object sender, EventArgs e)
        {
            var name = _roleName.Text.Trim();
            if (name.Length == 0) return;
            try
            {
                var result = _service.AddRole(new RoleDTO { Name = name });
                if (!result.Succeeded)
                {
                    ShowErrors(result.Errors);
                    return;
                }
                _roleName.Text = string.Empty;
                LoadRoles();
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(ex.Message, "Roles", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void RenameRole(object sender, EventArgs e)
        {
            var role = _roles.SelectedItem as RoleDTO;
            if (role == null) return;
            var name = XtraInputBox.Show("Enter the new role name:", "Rename role", role.Name);
            if (string.IsNullOrWhiteSpace(name)) return;
            var result = _service.UpdateRole(new RoleDTO { RoleId = role.RoleId, Name = name });
            if (!result.Succeeded) ShowErrors(result.Errors);
            LoadRoles();
        }

        private void DeleteRole(object sender, EventArgs e)
        {
            var role = _roles.SelectedItem as RoleDTO;
            if (role == null) return;
            if (XtraMessageBox.Show($"Delete role '{role.Name}'?", "Roles", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            var result = _service.DeleteRole(role.RoleId);
            if (!result.Succeeded) ShowErrors(result.Errors);
            LoadRoles();
        }

        private static void ShowErrors(System.Collections.Generic.IEnumerable<string> errors)
        {
            XtraMessageBox.Show(string.Join(Environment.NewLine, errors), "Roles", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void PermissionView_RowUpdated(
            object sender,
            DevExpress.XtraGrid.Views.Base.RowObjectEventArgs e)
        {
            if (_loading || !_canEdit) return;
            var claim = e.Row as RoleClaimDTO;
            if (claim == null) return;
            try
            {
                _service.UpdateClaim(claim);
                LoadPermissions();
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(ex.Message, "Permissions", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                LoadPermissions();
            }
        }
    }
}
