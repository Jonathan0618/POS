using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using POS.Models.Operations;
using POS.Services.Reporting;
using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace POS.Forms
{
    public sealed class ucReports : XtraUserControl
    {
        private readonly ManagementReportService _reports;
        private readonly ReportOutputService _output;
        private readonly DateEdit _from = new DateEdit();
        private readonly DateEdit _to = new DateEdit();
        private readonly ComboBoxEdit _reportType = new ComboBoxEdit();
        private readonly SpinEdit _registerId = CreateIdEditor();
        private readonly TextEdit _userId = new TextEdit();
        private readonly SpinEdit _categoryId = CreateIdEditor();
        private readonly SpinEdit _productId = CreateIdEditor();
        private readonly SpinEdit _supplierId = CreateIdEditor();
        private readonly SpinEdit _customerId = CreateIdEditor();
        private readonly SpinEdit _maximumRows = CreateIdEditor(500);
        private readonly CheckEdit _includeInactive = new CheckEdit { Text = "Include inactive/cancelled" };
        private readonly SimpleButton _run = new SimpleButton { Text = "Run Report" };
        private readonly SimpleButton _export = new SimpleButton { Text = "Export CSV" };
        private readonly SimpleButton _print = new SimpleButton { Text = "Print Summary" };
        private readonly LabelControl _summary = new LabelControl { AutoSizeMode = LabelAutoSizeMode.None };
        private readonly LabelControl _status = new LabelControl { AutoSizeMode = LabelAutoSizeMode.None };
        private readonly GridControl _grid = new GridControl();
        private readonly GridView _view = new GridView();
        private ManagementReportDTO _current;
        private bool _busy;

        public ucReports(ManagementReportService reports, ReportOutputService output)
        {
            _reports = reports ?? throw new ArgumentNullException(nameof(reports));
            _output = output ?? throw new ArgumentNullException(nameof(output));
            BuildUi();
        }

        private void BuildUi()
        {
            Dock = DockStyle.Fill;
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(12) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 106));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));

            var filters = new PanelControl { Dock = DockStyle.Fill };
            AddEditor(filters, "Report", _reportType, 10, 10, 160);
            _reportType.Properties.Items.AddRange(new object[] { "Sales", "Items / Categories", "Tenders", "Inventory", "Stock Movements", "Purchasing", "Shifts", "Audit Summary" });
            _reportType.SelectedIndex = 0;
            AddEditor(filters, "From", _from, 185, 10, 145);
            AddEditor(filters, "To", _to, 345, 10, 145);
            AddEditor(filters, "Register", _registerId, 505, 10, 90);
            AddEditor(filters, "User", _userId, 610, 10, 90);
            AddEditor(filters, "Category", _categoryId, 715, 10, 90);
            AddEditor(filters, "Product", _productId, 820, 10, 90);
            AddEditor(filters, "Supplier", _supplierId, 10, 54, 90);
            AddEditor(filters, "Customer", _customerId, 115, 54, 90);
            AddEditor(filters, "Max rows", _maximumRows, 220, 54, 90);
            _maximumRows.Properties.MinValue = 1;
            _maximumRows.Properties.MaxValue = 500;
            _maximumRows.Value = 500;
            _includeInactive.Location = new Point(325, 73);
            _run.Location = new Point(505, 65);
            _export.Location = new Point(610, 65);
            _print.Location = new Point(715, 65);
            filters.Controls.AddRange(new Control[] { _includeInactive, _run, _export, _print });
            _from.DateTime = DateTime.Today;
            _to.DateTime = DateTime.Today;

            _summary.Dock = DockStyle.Fill;
            _summary.Appearance.Font = new Font(_summary.Font.FontFamily, 10, FontStyle.Bold);
            _summary.Appearance.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            _summary.Padding = new Padding(8);
            _summary.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            _summary.Text = "Run a report to view financial totals.";
            _status.Dock = DockStyle.Fill;
            _status.Appearance.ForeColor = Color.DimGray;
            _grid.Dock = DockStyle.Fill;
            _grid.MainView = _view;
            _grid.ViewCollection.Add(_view);
            _view.OptionsBehavior.Editable = false;
            _view.OptionsView.ShowGroupPanel = false;
            _view.OptionsView.ShowAutoFilterRow = true;

            root.Controls.Add(filters, 0, 0);
            root.Controls.Add(_summary, 0, 1);
            root.Controls.Add(_grid, 0, 2);
            root.Controls.Add(_status, 0, 3);
            Controls.Add(root);

            _run.Click += async (sender, args) => await RunAsync();
            _export.Click += async (sender, args) => await ExportAsync();
            _print.Click += async (sender, args) => await PrintAsync();
            _reportType.SelectedIndexChanged += (sender, args) => BindSelectedReport();
            Load += async (sender, args) => await RunAsync();
        }

        private static SpinEdit CreateIdEditor(decimal value = 0)
        {
            var editor = new SpinEdit { Value = value };
            editor.Properties.IsFloatValue = false;
            editor.Properties.MinValue = 0;
            editor.Properties.MaxValue = int.MaxValue;
            return editor;
        }

        private static void AddEditor(Control parent, string caption, BaseEdit editor, int x, int y, int width)
        {
            parent.Controls.Add(new LabelControl { Text = caption, Location = new Point(x, y) });
            editor.Location = new Point(x, y + 18);
            editor.Size = new Size(width, 24);
            parent.Controls.Add(editor);
        }

        private ManagementFilterDTO BuildFilter()
        {
            if (_to.DateTime.Date < _from.DateTime.Date)
                throw new InvalidOperationException("The ending date cannot be before the starting date.");
            return new ManagementFilterDTO
            {
                FromUtc = _from.DateTime.Date.ToUniversalTime(),
                ToUtcExclusive = _to.DateTime.Date.AddDays(1).ToUniversalTime(),
                RegisterStationId = OptionalId(_registerId),
                UserId = string.IsNullOrWhiteSpace(_userId.Text) ? null : _userId.Text.Trim(),
                CategoryId = OptionalId(_categoryId),
                ProductId = OptionalId(_productId),
                SupplierId = OptionalId(_supplierId),
                CustomerId = OptionalId(_customerId),
                IncludeInactiveOrCancelled = _includeInactive.Checked,
                MaximumRows = Math.Min(500, Math.Max(1, Convert.ToInt32(_maximumRows.Value)))
            };
        }

        private static int? OptionalId(SpinEdit editor)
        {
            var value = Convert.ToInt32(editor.Value);
            return value == 0 ? (int?)null : value;
        }

        private async Task RunAsync()
        {
            if (_busy || IsDisposed) return;
            try
            {
                SetBusy(true, "Loading report...");
                var filter = BuildFilter();
                _current = await Task.Run(() => _reports.GetReport(filter));
                if (IsDisposed) return;
                var f = _current.Financials;
                _summary.Text = string.Format(
                    "Gross: {0}    Refunds: {1}    Net: {2}    Tax: {3}    Discounts: {4}    Cost: {5}    Margin: {6}    Transactions: {7:N0}    Returns: {8:N0}",
                    Money(f.GrossSales), Money(f.Refunds), Money(f.NetSales), Money(f.Tax), Money(f.Discounts), Money(f.EstimatedCost), Money(f.EstimatedMargin), f.TransactionCount, f.ReturnCount);
                BindSelectedReport();
                _status.Text = string.Format("Generated {0:g}; filter range {1:d} to {2:d}.", _current.GeneratedUtc.ToLocalTime(), _from.DateTime.Date, _to.DateTime.Date);
            }
            catch (Exception exception)
            {
                if (!IsDisposed) ShowError(exception, "Reports");
            }
            finally
            {
                if (!IsDisposed) SetBusy(false, _status.Text);
            }
        }

        private void BindSelectedReport()
        {
            if (_current == null) return;
            object rows;
            switch (_reportType.SelectedIndex)
            {
                case 1: rows = new BindingList<ItemSalesReportRowDTO>(_current.Items.ToList()); break;
                case 2: rows = new BindingList<TenderReportRowDTO>(_current.Tenders.ToList()); break;
                case 3: rows = new BindingList<InventoryReportRowDTO>(_current.Inventory.ToList()); break;
                case 4: rows = new BindingList<MovementReportRowDTO>(_current.Movements.ToList()); break;
                case 5: rows = new BindingList<PurchasingReportRowDTO>(_current.Purchasing.ToList()); break;
                case 6: rows = new BindingList<ShiftReportRowDTO>(_current.Shifts.ToList()); break;
                case 7: rows = new BindingList<AuditActivityReportRowDTO>(_current.AuditActivity.ToList()); break;
                default: rows = new BindingList<SalesReportRowDTO>(_current.Sales.ToList()); break;
            }
            _grid.DataSource = rows;
            foreach (DevExpress.XtraGrid.Columns.GridColumn column in _view.Columns)
                if (column.FieldName.EndsWith("Id", StringComparison.Ordinal)) column.Visible = false;
            _view.BestFitColumns();
        }

        private async Task ExportAsync()
        {
            if (_busy) return;
            try
            {
                var filter = BuildFilter();
                using (var dialog = new SaveFileDialog { Filter = "CSV files (*.csv)|*.csv", AddExtension = true, DefaultExt = "csv" })
                {
                    if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
                    SetBusy(true, "Preparing CSV export...");
                    var export = await Task.Run(() => _output.ExportCsv(filter));
                    await Task.Run(() => File.WriteAllBytes(dialog.FileName, export.Content));
                    _status.Text = "Exported " + dialog.FileName;
                }
            }
            catch (Exception exception) { ShowError(exception, "Export Report"); }
            finally { if (!IsDisposed) SetBusy(false, _status.Text); }
        }

        private async Task PrintAsync()
        {
            if (_busy) return;
            var register = OptionalId(_registerId);
            if (!register.HasValue)
            {
                XtraMessageBox.Show("Select a register ID with a configured printer.", "Print Summary", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                var filter = BuildFilter();
                SetBusy(true, "Printing summary...");
                await Task.Run(() => _output.PrintSummary(filter, register.Value));
                _status.Text = "The management summary was sent to the configured printer.";
            }
            catch (Exception exception) { ShowError(exception, "Print Summary"); }
            finally { if (!IsDisposed) SetBusy(false, _status.Text); }
        }

        private void SetBusy(bool busy, string status)
        {
            _busy = busy;
            _run.Enabled = !busy;
            _export.Enabled = !busy;
            _print.Enabled = !busy;
            _status.Text = status;
        }

        private static string Money(decimal value) => "PHP " + value.ToString("N2");

        private static void ShowError(Exception exception, string caption)
        {
            var message = exception is UnauthorizedAccessException || exception is InvalidOperationException || exception is System.ComponentModel.DataAnnotations.ValidationException
                ? exception.Message
                : "The operation could not be completed. Please try again.";
            XtraMessageBox.Show(message, caption, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
