using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using POS.Common.Enumerations;
using POS.Core.Security;
using POS.Models.Security;
using POS.Services;
using POS.Services.Security;
using System;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;

namespace POS.Forms.Security
{
    public sealed class ucModules : XtraUserControl
    {
        private readonly ModuleService _service;
        private readonly GridControl _grid = new GridControl();
        private readonly GridView _view = new GridView();
        private readonly SimpleButton _delete = new SimpleButton { Text = "Delete Module" };
        private readonly bool _canAdd;
        private readonly bool _canEdit;
        private readonly bool _canDelete;
        private bool _loading;

        public ucModules(ModuleService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _canAdd = AuthorizationService.HasPermission(ResourceCodes.Modules, ClaimActionType.Add);
            _canEdit = AuthorizationService.HasPermission(ResourceCodes.Modules, ClaimActionType.Edit);
            _canDelete = AuthorizationService.HasPermission(ResourceCodes.Modules, ClaimActionType.Delete);
            Dock = DockStyle.Fill;

            var actions = new PanelControl { Dock = DockStyle.Top, Height = 48 };
            _delete.Location = new System.Drawing.Point(10, 10);
            _delete.Enabled = _canDelete;
            actions.Controls.Add(_delete);
            _grid.Dock = DockStyle.Fill;
            _grid.MainView = _view;
            _grid.ViewCollection.Add(_view);
            _view.OptionsView.ShowGroupPanel = false;
            _view.OptionsView.ShowAutoFilterRow = true;
            _view.OptionsBehavior.Editable = _canAdd || _canEdit;
            _view.OptionsView.NewItemRowPosition = _canAdd
                ? NewItemRowPosition.Top
                : NewItemRowPosition.None;
            _view.RowUpdated += View_RowUpdated;
            Controls.Add(_grid);
            Controls.Add(actions);
            _delete.Click += DeleteSelected;
            Load += (sender, args) => Reload();
        }

        private void Reload()
        {
            _loading = true;
            try
            {
                _grid.DataSource = new BindingList<ModuleDTO>(_service.GetAllModules().ToList());
                _view.BestFitColumns();
            }
            finally
            {
                _loading = false;
            }
        }

        private void View_RowUpdated(object sender, DevExpress.XtraGrid.Views.Base.RowObjectEventArgs e)
        {
            if (_loading) return;
            var module = e.Row as ModuleDTO;
            if (module == null) return;
            var result = module.ModuleId == 0
                ? _service.AddModule(module)
                : _service.UpdateModule(module);
            if (!result.Succeeded)
                XtraMessageBox.Show(string.Join(Environment.NewLine, result.Errors), "Modules", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            Reload();
        }

        private void DeleteSelected(object sender, EventArgs e)
        {
            var module = _view.GetFocusedRow() as ModuleDTO;
            if (module == null) return;
            if (XtraMessageBox.Show($"Delete module '{module.Name}'?", "Modules", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            var result = _service.DeleteModule(module);
            if (!result.Succeeded)
                XtraMessageBox.Show(string.Join(Environment.NewLine, result.Errors), "Modules", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            Reload();
        }
    }
}
