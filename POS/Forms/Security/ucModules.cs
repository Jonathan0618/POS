using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using POS.Common.Enumerations;
using POS.Core.Security;
using POS.Models.Security;
using POS.Services;
using POS.Services.Security;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace POS.Forms.Security
{
    public sealed class ucModules : XtraUserControl
    {
        private readonly ModuleService _service;
        private readonly GridControl _grid = new GridControl();
        private readonly GridView _view = new GridView();
        private readonly TextEdit _name = new TextEdit();
        private readonly LookUpEdit _parent = new LookUpEdit();
        private readonly SimpleButton _add = new SimpleButton { Text = "Add" };
        private readonly SimpleButton _rename = new SimpleButton { Text = "Save Changes" };
        private readonly SimpleButton _delete = new SimpleButton { Text = "Delete" };
        private readonly SimpleButton _reload = new SimpleButton { Text = "Reload" };
        private readonly LabelControl _status = new LabelControl { AutoSizeMode = LabelAutoSizeMode.None };
        private readonly bool _canAdd;
        private readonly bool _canEdit;
        private readonly bool _canDelete;
        private bool _loading;

        private static readonly HashSet<string> StableResources = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ResourceCodes.Users, ResourceCodes.Roles, ResourceCodes.Modules, ResourceCodes.Settings,
            ResourceCodes.Products, ResourceCodes.Inventory, ResourceCodes.Suppliers, ResourceCodes.Purchasing,
            ResourceCodes.Customers, ResourceCodes.Shifts, ResourceCodes.Sales, ResourceCodes.Returns,
            ResourceCodes.Dashboard, ResourceCodes.Reports, ResourceCodes.Audit, ResourceCodes.Maintenance
        };

        public ucModules(ModuleService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _canAdd = AuthorizationService.HasPermission(ResourceCodes.Modules, ClaimActionType.Add);
            _canEdit = AuthorizationService.HasPermission(ResourceCodes.Modules, ClaimActionType.Edit);
            _canDelete = AuthorizationService.HasPermission(ResourceCodes.Modules, ClaimActionType.Delete);
            BuildUi();
        }

        private void BuildUi()
        {
            Dock = DockStyle.Fill;
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(12) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));

            var actions = new PanelControl { Dock = DockStyle.Fill };
            AddEditor(actions, "Resource / module name", _name, 10, 8, 220);
            AddEditor(actions, "Parent module", _parent, 245, 8, 210);
            _parent.Properties.DisplayMember = "Name";
            _parent.Properties.ValueMember = "ModuleId";
            _parent.Properties.NullText = "(No parent)";
            _add.Location = new Point(470, 25);
            _rename.Location = new Point(555, 25);
            _delete.Location = new Point(675, 25);
            _reload.Location = new Point(760, 25);
            actions.Controls.AddRange(new Control[] { _add, _rename, _delete, _reload });

            _grid.Dock = DockStyle.Fill;
            _grid.MainView = _view;
            _grid.ViewCollection.Add(_view);
            _view.OptionsBehavior.Editable = false;
            _view.OptionsView.ShowGroupPanel = false;
            _view.OptionsView.ShowAutoFilterRow = true;
            _status.Dock = DockStyle.Fill;
            _status.Appearance.ForeColor = Color.DimGray;
            root.Controls.Add(actions, 0, 0);
            root.Controls.Add(_grid, 0, 1);
            root.Controls.Add(_status, 0, 2);
            Controls.Add(root);

            _view.FocusedRowChanged += (sender, args) => BindSelection();
            _add.Click += AddModule;
            _rename.Click += SaveModule;
            _delete.Click += DeleteModule;
            _reload.Click += (sender, args) => Reload();
            Load += (sender, args) => Reload();
        }

        private static void AddEditor(Control parent, string caption, BaseEdit editor, int x, int y, int width)
        {
            parent.Controls.Add(new LabelControl { Text = caption, Location = new Point(x, y) });
            editor.Location = new Point(x, y + 18);
            editor.Size = new Size(width, 24);
            parent.Controls.Add(editor);
        }

        private void Reload()
        {
            RunSafe(() =>
            {
                var modules = _service.GetAllModules().OrderBy(x => x.Name).ToList();
                _grid.DataSource = new BindingList<ModuleDTO>(modules);
                _parent.Properties.DataSource = modules;
                HideColumn("ModuleId");
                var parentColumn = _view.Columns.ColumnByFieldName("ParentModuleId");
                if (parentColumn != null) parentColumn.Caption = "Parent ID";
                _view.BestFitColumns();
                _status.Text = modules.Count == 0 ? "No modules are defined." : modules.Count.ToString("N0") + " module(s). Seeded resource codes are protected in this screen.";
            });
            BindSelection();
        }

        private void BindSelection()
        {
            if (_loading) return;
            var module = _view.GetFocusedRow() as ModuleDTO;
            _name.Text = module?.Name ?? string.Empty;
            _parent.EditValue = module?.ParentModuleId;
            var stable = module != null && StableResources.Contains(module.Name);
            _add.Enabled = _canAdd;
            _rename.Enabled = _canEdit && module != null && !stable;
            _delete.Enabled = _canDelete && module != null && !stable;
            if (stable) _status.Text = "'" + module.Name + "' is a stable authorization resource and cannot be renamed or deleted here.";
        }

        private void AddModule(object sender, EventArgs e)
        {
            var module = NewEditorValue(0);
            if (module == null) return;
            var refresh = false;
            RunSafe(() =>
            {
                var result = _service.AddModule(module);
                if (!result.Succeeded) { ShowErrors(result.Errors); return; }
                refresh = true;
            });
            if (refresh) Reload();
        }

        private void SaveModule(object sender, EventArgs e)
        {
            var selected = _view.GetFocusedRow() as ModuleDTO;
            if (selected == null) return;
            if (StableResources.Contains(selected.Name)) { ShowErrors(new[] { "Seeded authorization resources cannot be renamed from this screen." }); return; }
            var module = NewEditorValue(selected.ModuleId);
            if (module == null) return;
            var refresh = false;
            RunSafe(() =>
            {
                var result = _service.UpdateModule(module);
                if (!result.Succeeded) { ShowErrors(result.Errors); return; }
                refresh = true;
            });
            if (refresh) Reload();
        }

        private void DeleteModule(object sender, EventArgs e)
        {
            var selected = _view.GetFocusedRow() as ModuleDTO;
            if (selected == null) return;
            if (StableResources.Contains(selected.Name)) { ShowErrors(new[] { "Seeded authorization resources cannot be deleted from this screen." }); return; }
            if (XtraMessageBox.Show("Delete module '" + selected.Name + "'? The service will reject referenced or parent modules.", "Modules", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            var refresh = false;
            RunSafe(() =>
            {
                var result = _service.DeleteModule(selected);
                if (!result.Succeeded) { ShowErrors(result.Errors); return; }
                refresh = true;
            });
            if (refresh) Reload();
        }

        private ModuleDTO NewEditorValue(int id)
        {
            if (string.IsNullOrWhiteSpace(_name.Text)) { ShowErrors(new[] { "Module name is required." }); return null; }
            return new ModuleDTO
            {
                ModuleId = id,
                Name = _name.Text.Trim(),
                ParentModuleId = _parent.EditValue == null ? (int?)null : Convert.ToInt32(_parent.EditValue)
            };
        }

        private void RunSafe(Action action)
        {
            if (_loading) return;
            _loading = true;
            try { action(); }
            catch (Exception exception)
            {
                ShowErrors(new[] { exception is UnauthorizedAccessException || exception is InvalidOperationException ? exception.Message : "The module operation could not be completed. Please reload and try again." });
            }
            finally { _loading = false; }
        }

        private void HideColumn(string fieldName)
        {
            var column = _view.Columns.ColumnByFieldName(fieldName);
            if (column != null) column.Visible = false;
        }

        private static void ShowErrors(IEnumerable<string> errors)
        {
            XtraMessageBox.Show(string.Join(Environment.NewLine, errors), "Modules", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
