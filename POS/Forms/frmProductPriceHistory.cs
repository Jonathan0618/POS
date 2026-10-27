using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using POS.Models.Operations;
using POS.Services;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace POS.Forms
{
    public sealed class frmProductPriceHistory : XtraForm
    {
        private const int PageSize = 50;
        private readonly InventoryService _service;
        private readonly int _productId;
        private readonly GridControl _grid = new GridControl();
        private readonly GridView _view = new GridView();
        private readonly SimpleButton _previous = new SimpleButton { Text = "Previous" };
        private readonly SimpleButton _next = new SimpleButton { Text = "Next" };
        private readonly SimpleButton _reload = new SimpleButton { Text = "Reload" };
        private readonly LabelControl _status = new LabelControl();
        private readonly System.Collections.Generic.List<DevExpress.XtraGrid.Columns.GridColumn> _moneyColumns =
            new System.Collections.Generic.List<DevExpress.XtraGrid.Columns.GridColumn>();
        private int _page = 1;
        private int _totalPages = 1;
        private bool _busy;

        public frmProductPriceHistory() : this(null, 0, null) { }

        public frmProductPriceHistory(InventoryService service, int productId, string productName)
        {
            _service = service;
            _productId = productId;
            Text = "Price History - " + (string.IsNullOrWhiteSpace(productName) ? "Product" : productName);
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new System.Drawing.Size(980, 520);
            Size = new System.Drawing.Size(1120, 640);
            BuildUi();
            Shown += async (sender, args) => await LoadPageAsync();
        }

        private void BuildUi()
        {
            _grid.Dock = DockStyle.Fill;
            _grid.MainView = _view;
            _grid.ViewCollection.Add(_view);
            _view.OptionsBehavior.Editable = false;
            _view.OptionsView.ShowGroupPanel = false;
            AddColumn("ChangedLocal", "Changed", "g");
            AddColumn(nameof(ProductPriceChangeDTO.UserId), "User");
            AddColumn(nameof(ProductPriceChangeDTO.RegisterCode), "Register");
            _moneyColumns.Add(AddColumn(nameof(ProductPriceChangeDTO.PreviousSellingPrice), "Previous selling", "n2"));
            _moneyColumns.Add(AddColumn(nameof(ProductPriceChangeDTO.SellingPrice), "New selling", "n2"));
            _moneyColumns.Add(AddColumn(nameof(ProductPriceChangeDTO.PreviousCostPrice), "Previous cost", "n2"));
            _moneyColumns.Add(AddColumn(nameof(ProductPriceChangeDTO.CostPrice), "New cost", "n2"));
            AddColumn(nameof(ProductPriceChangeDTO.Action), "Action");
            AddColumn(nameof(ProductPriceChangeDTO.CorrelationId), "Support ID");

            var footer = new PanelControl { Dock = DockStyle.Bottom, Height = 48 };
            _previous.SetBounds(12, 10, 90, 28);
            _next.SetBounds(108, 10, 90, 28);
            _reload.SetBounds(204, 10, 90, 28);
            _status.SetBounds(310, 16, 700, 22);
            footer.Controls.AddRange(new Control[] { _previous, _next, _reload, _status });
            Controls.Add(_grid);
            Controls.Add(footer);
            _previous.Click += async (sender, args) => { if (_page > 1) { _page--; await LoadPageAsync(); } };
            _next.Click += async (sender, args) => { _page++; await LoadPageAsync(); };
            _reload.Click += async (sender, args) => await LoadPageAsync();
        }

        private DevExpress.XtraGrid.Columns.GridColumn AddColumn(string field, string caption, string format = null)
        {
            var column = _view.Columns.AddVisible(field, caption);
            if (!string.IsNullOrEmpty(format))
            {
                column.DisplayFormat.FormatType = field == "ChangedLocal" ? DevExpress.Utils.FormatType.DateTime : DevExpress.Utils.FormatType.Numeric;
                column.DisplayFormat.FormatString = format;
            }
            return column;
        }

        private async Task LoadPageAsync()
        {
            if (_busy || _service == null || _productId <= 0) return;
            try
            {
                SetBusy(true, "Loading price history...");
                var result = await Task.Run(() => _service.GetProductPriceHistory(_productId, _page, PageSize));
                _page = result.PageNumber;
                _totalPages = result.TotalPages;
                var zone = ResolveTimeZone(result.TimeZoneId);
                var rows = result.Items.Select(x => new PriceRow(x, zone)).ToList();
                ConfigureMoneyColumns(result.CurrencyCode, result.MoneyDecimalPlaces);
                _grid.DataSource = new BindingList<PriceRow>(rows);
                _previous.Enabled = _page > 1;
                _next.Enabled = _page < result.TotalPages;
                _status.Text = rows.Count == 0 ? "No price changes were found." :
                    string.Format("Page {0} of {1} - {2:N0} change(s)", _page, result.TotalPages, result.TotalCount);
            }
            catch (Exception exception) { ShowError(exception, "Price history could not be loaded."); }
            finally { SetBusy(false, _status.Text); }
        }

        private void SetBusy(bool busy, string status)
        {
            _busy = busy;
            _grid.Enabled = !busy;
            _previous.Enabled = !busy && _page > 1;
            _next.Enabled = !busy && _page < _totalPages;
            _reload.Enabled = !busy;
            _status.Text = status;
            UseWaitCursor = busy;
        }

        private static void ShowError(Exception exception, string fallback)
        {
            var safe = exception is UnauthorizedAccessException || exception is InvalidOperationException ||
                exception is System.ComponentModel.DataAnnotations.ValidationException;
            XtraMessageBox.Show(safe ? exception.Message : fallback, "Price History",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void ConfigureMoneyColumns(string currencyCode, int decimalPlaces)
        {
            var currency = string.IsNullOrWhiteSpace(currencyCode) ? "PHP" : currencyCode;
            var places = Math.Max(0, Math.Min(4, decimalPlaces));
            foreach (var column in _moneyColumns)
            {
                column.Caption = column.Caption.Split('(')[0].TrimEnd() + " (" + currency + ")";
                column.DisplayFormat.FormatString = "n" + places;
            }
        }

        private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.Local; }
            catch (InvalidTimeZoneException) { return TimeZoneInfo.Local; }
        }

        private sealed class PriceRow
        {
            private readonly ProductPriceChangeDTO _source;
            private readonly TimeZoneInfo _timeZone;
            public PriceRow(ProductPriceChangeDTO source, TimeZoneInfo timeZone) { _source = source; _timeZone = timeZone; }
            public DateTime ChangedLocal { get { return TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(_source.ChangedUtc, DateTimeKind.Utc), _timeZone); } }
            public string UserId { get { return _source.UserId; } }
            public string RegisterCode { get { return _source.RegisterCode; } }
            public decimal? PreviousSellingPrice { get { return _source.PreviousSellingPrice; } }
            public decimal? SellingPrice { get { return _source.SellingPrice; } }
            public decimal? PreviousCostPrice { get { return _source.PreviousCostPrice; } }
            public decimal? CostPrice { get { return _source.CostPrice; } }
            public string Action { get { return _source.Action; } }
            public Guid? CorrelationId { get { return _source.CorrelationId; } }
        }
    }
}
