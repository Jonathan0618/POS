using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using POS.Models.Operations;
using POS.Services.Settings;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace POS.Forms.Settings
{
    public sealed class ucRegisters : XtraUserControl
    {
        private readonly RegisterStationService _service;
        private readonly Func<RegisterStationDTO, Form> _createEditor;
        private readonly TextEdit _search = new TextEdit();
        private readonly ComboBoxEdit _active = new ComboBoxEdit();
        private readonly SimpleButton _apply = new SimpleButton { Text = "Search" };
        private readonly SimpleButton _add = new SimpleButton { Text = "Add Register" };
        private readonly SimpleButton _edit = new SimpleButton { Text = "Edit" };
        private readonly SimpleButton _toggle = new SimpleButton { Text = "Activate / Deactivate" };
        private readonly SimpleButton _previous = new SimpleButton { Text = "Previous" };
        private readonly SimpleButton _next = new SimpleButton { Text = "Next" };
        private readonly LabelControl _page = new LabelControl();
        private readonly LabelControl _status = new LabelControl { AutoSizeMode = LabelAutoSizeMode.None };
        private readonly GridControl _grid = new GridControl();
        private readonly GridView _view = new GridView();
        private int _pageNumber = 1;
        private int _totalPages = 1;
        private bool _busy;

        public ucRegisters(RegisterStationService service, Func<RegisterStationDTO, Form> createEditor)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _createEditor = createEditor ?? throw new ArgumentNullException(nameof(createEditor));
            BuildUi();
        }

        private void BuildUi()
        {
            Name = "ucRegisters";
            Dock = DockStyle.Fill;
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(12) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            var filters = new PanelControl { Dock = DockStyle.Fill };
            AddEditor(filters, "Code or name", _search, 10, 8, 220);
            AddEditor(filters, "State", _active, 245, 8, 130);
            _active.Properties.Items.AddRange(new object[] { "All", "Active", "Inactive" });
            _active.SelectedIndex = 0;
            _apply.Location = new Point(390, 25);
            _add.Location = new Point(490, 25);
            _edit.Location = new Point(600, 25);
            _toggle.Location = new Point(690, 25);
            _previous.Location = new Point(855, 25);
            _next.Location = new Point(945, 25);
            _page.Location = new Point(1040, 31);
            filters.Controls.AddRange(new Control[] { _apply, _add, _edit, _toggle, _previous, _next, _page });
            _grid.Dock = DockStyle.Fill;
            _grid.MainView = _view;
            _grid.ViewCollection.Add(_view);
            _view.OptionsBehavior.Editable = false;
            _view.OptionsView.ShowGroupPanel = false;
            _view.OptionsView.ShowAutoFilterRow = true;
            _status.Dock = DockStyle.Fill;
            root.Controls.Add(filters, 0, 0);
            root.Controls.Add(_grid, 0, 1);
            root.Controls.Add(_status, 0, 2);
            Controls.Add(root);
            _apply.Click += async (sender, args) => { _pageNumber = 1; await LoadPageAsync(); };
            _previous.Click += async (sender, args) => { if (_pageNumber > 1) { _pageNumber--; await LoadPageAsync(); } };
            _next.Click += async (sender, args) => { if (_pageNumber < _totalPages) { _pageNumber++; await LoadPageAsync(); } };
            _add.Click += async (sender, args) => await EditAsync(null);
            _edit.Click += async (sender, args) => await EditAsync(Selected);
            _view.DoubleClick += async (sender, args) => await EditAsync(Selected);
            _toggle.Click += async (sender, args) => await ToggleAsync();
            Load += async (sender, args) => await LoadPageAsync();
        }

        private RegisterStationDTO Selected => _view.GetFocusedRow() as RegisterStationDTO;
        private bool? ActiveFilter => _active.SelectedIndex == 1 ? true : _active.SelectedIndex == 2 ? (bool?)false : null;

        private async Task LoadPageAsync()
        {
            if (_busy || IsDisposed) return;
            try
            {
                SetBusy(true, "Loading registers...");
                var page = await Task.Run(() => _service.Search(_search.Text, ActiveFilter, _pageNumber, 50));
                if (IsDisposed) return;
                _pageNumber = page.PageNumber;
                _totalPages = page.TotalPages;
                _grid.DataSource = new BindingList<RegisterStationDTO>(page.Items);
                Hide("Id"); Hide("Revision");
                _view.BestFitColumns();
                _page.Text = string.Format("Page {0} of {1}", _pageNumber, _totalPages);
                _status.Text = page.TotalCount == 0 ? "No registers matched the filters." : string.Format("{0:N0} register(s) found.", page.TotalCount);
            }
            catch (Exception exception) { ShowError(exception); }
            finally { if (!IsDisposed) SetBusy(false, _status.Text); }
        }

        private async Task EditAsync(RegisterStationDTO selected)
        {
            if (_busy) return;
            try
            {
                RegisterStationDTO details = null;
                if (selected != null) details = await Task.Run(() => _service.GetDetails(selected.Id));
                using (var form = _createEditor(details))
                    if (form.ShowDialog(FindForm()) == DialogResult.OK) await LoadPageAsync();
            }
            catch (Exception exception) { ShowError(exception); }
        }

        private async Task ToggleAsync()
        {
            var selected = Selected;
            if (selected == null) { XtraMessageBox.Show("Select a register first.", "Registers"); return; }
            var action = selected.IsActive ? "deactivate" : "activate";
            if (XtraMessageBox.Show(string.Format("{0} register '{1}'?", char.ToUpper(action[0]) + action.Substring(1), selected.Code),
                "Registers", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            try
            {
                SetBusy(true, "Updating register...");
                await Task.Run(() => _service.SetActive(selected.Id, !selected.IsActive, selected.Revision));
                _busy = false;
                await LoadPageAsync();
            }
            catch (Exception exception) { ShowError(exception); }
            finally { if (!IsDisposed) SetBusy(false, _status.Text); }
        }

        private void Hide(string fieldName) { var column = _view.Columns.ColumnByFieldName(fieldName); if (column != null) column.Visible = false; }
        private void SetBusy(bool busy, string status)
        {
            _busy = busy;
            _search.Enabled = !busy; _active.Enabled = !busy; _apply.Enabled = !busy; _add.Enabled = !busy; _edit.Enabled = !busy; _toggle.Enabled = !busy;
            _previous.Enabled = !busy && _pageNumber > 1;
            _next.Enabled = !busy && _pageNumber < _totalPages;
            _status.Text = status;
        }
        private static void AddEditor(Control parent, string caption, BaseEdit editor, int x, int y, int width)
        {
            parent.Controls.Add(new LabelControl { Text = caption, Location = new Point(x, y) });
            editor.Location = new Point(x, y + 18); editor.Size = new Size(width, 24); parent.Controls.Add(editor);
        }
        private static void ShowError(Exception exception)
        {
            var message = exception is ValidationException || exception is UnauthorizedAccessException || exception is InvalidOperationException
                ? exception.Message : "Registers could not be loaded or saved. Please try again.";
            XtraMessageBox.Show(message, "Registers", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
