using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using POS.Models.Operations;
using POS.Services.Reporting;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace POS.Forms
{
    public partial class ucDashboard : XtraUserControl
    {
        private readonly ManagementReportService _service;
        private readonly DateEdit _from = new DateEdit();
        private readonly DateEdit _to = new DateEdit();
        private readonly SpinEdit _registerId = new SpinEdit();
        private readonly SimpleButton _refresh = new SimpleButton { Text = "Refresh" };
        private readonly LabelControl _status = new LabelControl { AutoSizeMode = LabelAutoSizeMode.None };
        private readonly LabelControl[] _summary = new LabelControl[9];
        private readonly GridControl _topProducts = new GridControl();
        private readonly GridView _topProductsView = new GridView();
        private readonly GridControl _stockAlerts = new GridControl();
        private readonly GridView _stockAlertsView = new GridView();
        private bool _busy;

        public ucDashboard(ManagementReportService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            InitializeComponent();
            BuildUi();
        }

        private void BuildUi()
        {
            Dock = DockStyle.Fill;
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(12) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));

            var filters = new PanelControl { Dock = DockStyle.Fill };
            AddLabel(filters, "From", 12, 12);
            ConfigureDate(_from, 62, 9);
            AddLabel(filters, "To", 245, 12);
            ConfigureDate(_to, 275, 9);
            AddLabel(filters, "Register ID (0 = all)", 458, 12);
            _registerId.Location = new Point(585, 9);
            _registerId.Size = new Size(100, 24);
            _registerId.Properties.IsFloatValue = false;
            _registerId.Properties.MinValue = 0;
            _registerId.Properties.MaxValue = int.MaxValue;
            _refresh.Location = new Point(700, 8);
            _refresh.Size = new Size(100, 28);
            filters.Controls.AddRange(new Control[] { _from, _to, _registerId, _refresh });
            _from.DateTime = DateTime.Today;
            _to.DateTime = DateTime.Today;

            var cards = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 2 };
            for (var i = 0; i < 5; i++) cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
            cards.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            cards.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            var names = new[] { "Gross Sales", "Refunds", "Net Sales", "Tax", "Discounts", "Estimated Cost", "Estimated Margin", "Transactions", "Returns" };
            for (var i = 0; i < names.Length; i++)
            {
                _summary[i] = new LabelControl
                {
                    Dock = DockStyle.Fill,
                    Appearance = { TextOptions = { HAlignment = DevExpress.Utils.HorzAlignment.Center, VAlignment = DevExpress.Utils.VertAlignment.Center } },
                    BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple,
                    Text = names[i] + Environment.NewLine + "--"
                };
                _summary[i].Appearance.Font = new Font(_summary[i].Font.FontFamily, 11, FontStyle.Bold);
                cards.Controls.Add(_summary[i], i % 5, i / 5);
            }

            ConfigureGrid(_topProducts, _topProductsView);
            ConfigureGrid(_stockAlerts, _stockAlertsView);
            var grids = new SplitContainerControl { Dock = DockStyle.Fill, Horizontal = false, SplitterPosition = 450 };
            grids.Panel1.Text = "Top Products";
            grids.Panel1.Controls.Add(_topProducts);
            grids.Panel2.Text = "Stock Alerts";
            grids.Panel2.Controls.Add(_stockAlerts);

            _status.Dock = DockStyle.Fill;
            _status.Appearance.ForeColor = Color.DimGray;
            root.Controls.Add(filters, 0, 0);
            root.Controls.Add(cards, 0, 1);
            root.Controls.Add(grids, 0, 2);
            root.Controls.Add(_status, 0, 3);
            Controls.Add(root);

            _refresh.Click += async (sender, args) => await LoadDashboardAsync();
            Load += async (sender, args) => await LoadDashboardAsync();
        }

        private static void ConfigureDate(DateEdit editor, int x, int y)
        {
            editor.Location = new Point(x, y);
            editor.Size = new Size(165, 24);
        }

        private static void AddLabel(Control parent, string text, int x, int y)
        {
            parent.Controls.Add(new LabelControl { Text = text, Location = new Point(x, y + 3) });
        }

        private static void ConfigureGrid(GridControl grid, GridView view)
        {
            grid.Dock = DockStyle.Fill;
            grid.MainView = view;
            grid.ViewCollection.Add(view);
            view.OptionsBehavior.Editable = false;
            view.OptionsView.ShowGroupPanel = false;
            view.OptionsView.ShowAutoFilterRow = true;
        }

        private ManagementFilterDTO BuildFilter()
        {
            if (_to.DateTime.Date < _from.DateTime.Date)
                throw new InvalidOperationException("The ending date cannot be before the starting date.");
            var register = Convert.ToInt32(_registerId.Value);
            return new ManagementFilterDTO
            {
                FromUtc = _from.DateTime.Date.ToUniversalTime(),
                ToUtcExclusive = _to.DateTime.Date.AddDays(1).ToUniversalTime(),
                RegisterStationId = register == 0 ? (int?)null : register,
                MaximumRows = 100
            };
        }

        private async Task LoadDashboardAsync()
        {
            if (_busy || IsDisposed) return;
            try
            {
                SetBusy(true, "Loading dashboard...");
                var filter = BuildFilter();
                var result = await Task.Run(() => _service.GetDashboard(filter));
                if (IsDisposed) return;
                Bind(result);
            }
            catch (Exception exception)
            {
                if (!IsDisposed) ShowError(exception, "Dashboard");
            }
            finally
            {
                if (!IsDisposed) SetBusy(false, _status.Text);
            }
        }

        private void Bind(DashboardDTO result)
        {
            var f = result.Financials;
            var values = new[]
            {
                Money(f.GrossSales), Money(f.Refunds), Money(f.NetSales), Money(f.Tax), Money(f.Discounts),
                Money(f.EstimatedCost), Money(f.EstimatedMargin), f.TransactionCount.ToString("N0"), f.ReturnCount.ToString("N0")
            };
            var captions = new[] { "Gross Sales", "Refunds", "Net Sales", "Tax", "Discounts", "Estimated Cost", "Estimated Margin", "Transactions", "Returns" };
            for (var i = 0; i < _summary.Length; i++) _summary[i].Text = captions[i] + Environment.NewLine + values[i];
            _topProducts.DataSource = new BindingList<ItemSalesReportRowDTO>(result.TopProducts.ToList());
            _stockAlerts.DataSource = new BindingList<InventoryReportRowDTO>(result.StockAlerts.ToList());
            HideColumn(_topProductsView, "ProductId");
            HideColumn(_stockAlertsView, "ProductId");
            _topProductsView.BestFitColumns();
            _stockAlertsView.BestFitColumns();
            _status.Text = string.Format(
                "Generated {0:g} | Expiring: {1:N0} | Open shifts: {2:N0} | Shift issues: {3:N0}",
                result.GeneratedUtc.ToLocalTime(), result.ExpiringProductCount, result.OpenShiftCount, result.OpenShiftIssueCount);
        }

        private static void HideColumn(GridView view, string fieldName)
        {
            var column = view.Columns.ColumnByFieldName(fieldName);
            if (column != null) column.Visible = false;
        }

        private static string Money(decimal value) => "PHP " + value.ToString("N2");

        private void SetBusy(bool busy, string text)
        {
            _busy = busy;
            _refresh.Enabled = !busy;
            _from.Enabled = !busy;
            _to.Enabled = !busy;
            _registerId.Enabled = !busy;
            _status.Text = text;
        }

        private static void ShowError(Exception exception, string caption)
        {
            var message = exception is UnauthorizedAccessException || exception is InvalidOperationException || exception is System.ComponentModel.DataAnnotations.ValidationException
                ? exception.Message
                : "The information could not be loaded. Please try again.";
            XtraMessageBox.Show(message, caption, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
