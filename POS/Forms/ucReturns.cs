using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using POS.Models.Operations;
using POS.Services.Point_of_Sale;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace POS.Forms
{
    public sealed class ReturnRequestedEventArgs : EventArgs
    {
        public ReturnRequestedEventArgs(int saleId) { SaleId = saleId; }
        public int SaleId { get; }
    }

    public sealed class ucReturns : XtraUserControl
    {
        private readonly SaleReceiptService _receipts;
        private readonly SaleReturnService _returns;
        private readonly TextEdit _search = new TextEdit();
        private readonly DateEdit _from = new DateEdit();
        private readonly DateEdit _to = new DateEdit();
        private readonly SimpleButton _apply = new SimpleButton { Text = "Search" };
        private readonly SimpleButton _clear = new SimpleButton { Text = "Clear" };
        private readonly SimpleButton _previous = new SimpleButton { Text = "Previous" };
        private readonly SimpleButton _next = new SimpleButton { Text = "Next" };
        private readonly SimpleButton _checkEligibility = new SimpleButton { Text = "Check Eligibility" };
        private readonly SimpleButton _startReturn = new SimpleButton { Text = "Start Return" };
        private readonly LabelControl _page = new LabelControl();
        private readonly LabelControl _eligibilityHeader = new LabelControl { AutoSizeMode = LabelAutoSizeMode.None };
        private readonly LabelControl _status = new LabelControl { AutoSizeMode = LabelAutoSizeMode.None };
        private readonly GridControl _receiptsGrid = new GridControl();
        private readonly GridView _receiptsView = new GridView();
        private readonly GridControl _linesGrid = new GridControl();
        private readonly GridView _linesView = new GridView();
        private readonly GridControl _paymentsGrid = new GridControl();
        private readonly GridView _paymentsView = new GridView();
        private SaleReturnEligibilityDTO _eligibility;
        private int _pageNumber = 1;
        private int _totalPages = 1;
        private bool _busy;

        public ucReturns(SaleReceiptService receipts, SaleReturnService returns)
        {
            _receipts = receipts ?? throw new ArgumentNullException(nameof(receipts));
            _returns = returns ?? throw new ArgumentNullException(nameof(returns));
            BuildUi();
        }

        public event EventHandler<ReturnRequestedEventArgs> ReturnRequested;

        private void BuildUi()
        {
            Dock = DockStyle.Fill;
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(12) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));

            var filters = new PanelControl { Dock = DockStyle.Fill };
            AddEditor(filters, "Receipt / customer / register", _search, 10, 8, 230);
            AddEditor(filters, "From", _from, 255, 8, 145);
            AddEditor(filters, "To", _to, 415, 8, 145);
            _apply.Location = new Point(575, 25);
            _clear.Location = new Point(675, 25);
            _previous.Location = new Point(775, 25);
            _next.Location = new Point(865, 25);
            _page.Location = new Point(960, 31);
            filters.Controls.AddRange(new Control[] { _apply, _clear, _previous, _next, _page });

            ConfigureGrid(_receiptsGrid, _receiptsView);
            ConfigureGrid(_linesGrid, _linesView);
            ConfigureGrid(_paymentsGrid, _paymentsView);
            var right = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            _eligibilityHeader.Dock = DockStyle.Fill;
            _eligibilityHeader.Padding = new Padding(8);
            _eligibilityHeader.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            _eligibilityHeader.Text = "Select a completed receipt and check its return eligibility.";
            var actions = new PanelControl { Dock = DockStyle.Fill };
            _checkEligibility.Location = new Point(8, 7);
            _startReturn.Location = new Point(132, 7);
            _startReturn.Enabled = false;
            actions.Controls.AddRange(new Control[] { _checkEligibility, _startReturn });
            right.Controls.Add(_eligibilityHeader, 0, 0);
            right.Controls.Add(_linesGrid, 0, 1);
            right.Controls.Add(_paymentsGrid, 0, 2);
            right.Controls.Add(actions, 0, 3);

            var split = new SplitContainerControl { Dock = DockStyle.Fill, SplitterPosition = 520 };
            split.Panel1.Controls.Add(_receiptsGrid);
            split.Panel2.Controls.Add(right);
            _status.Dock = DockStyle.Fill;
            _status.Appearance.ForeColor = Color.DimGray;
            root.Controls.Add(filters, 0, 0);
            root.Controls.Add(split, 0, 1);
            root.Controls.Add(_status, 0, 2);
            Controls.Add(root);

            _apply.Click += async (sender, args) => { _pageNumber = 1; await LoadReceiptsAsync(); };
            _clear.Click += async (sender, args) => { _search.Text = string.Empty; _from.EditValue = null; _to.EditValue = null; _pageNumber = 1; await LoadReceiptsAsync(); };
            _previous.Click += async (sender, args) => { if (_pageNumber > 1) { _pageNumber--; await LoadReceiptsAsync(); } };
            _next.Click += async (sender, args) => { if (_pageNumber < _totalPages) { _pageNumber++; await LoadReceiptsAsync(); } };
            _checkEligibility.Click += async (sender, args) => await LoadEligibilityAsync();
            _receiptsView.DoubleClick += async (sender, args) => await LoadEligibilityAsync();
            _startReturn.Click += StartReturn;
            Load += async (sender, args) => await LoadReceiptsAsync();
        }

        private static void AddEditor(Control parent, string caption, BaseEdit editor, int x, int y, int width)
        {
            parent.Controls.Add(new LabelControl { Text = caption, Location = new Point(x, y) });
            editor.Location = new Point(x, y + 18);
            editor.Size = new Size(width, 24);
            parent.Controls.Add(editor);
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

        private SaleReceiptSearchDTO BuildSearch()
        {
            if (_from.EditValue != null && _to.EditValue != null && _to.DateTime.Date < _from.DateTime.Date)
                throw new InvalidOperationException("The ending date cannot be before the starting date.");
            return new SaleReceiptSearchDTO
            {
                Search = _search.Text,
                FromUtc = _from.EditValue == null ? (DateTime?)null : _from.DateTime.Date.ToUniversalTime(),
                ToUtcExclusive = _to.EditValue == null ? (DateTime?)null : _to.DateTime.Date.AddDays(1).ToUniversalTime(),
                PageNumber = _pageNumber,
                PageSize = 50
            };
        }

        private async Task LoadReceiptsAsync()
        {
            if (_busy || IsDisposed) return;
            try
            {
                SetBusy(true, "Loading receipts...");
                var search = BuildSearch();
                var result = await Task.Run(() => _receipts.Search(search));
                if (IsDisposed) return;
                _pageNumber = result.PageNumber;
                _totalPages = result.TotalPages;
                _receiptsGrid.DataSource = new BindingList<SaleReceiptSummaryDTO>(result.Items);
                HideColumn(_receiptsView, "SaleId");
                _receiptsView.BestFitColumns();
                _page.Text = string.Format("Page {0} of {1}", _pageNumber, _totalPages);
                _status.Text = result.TotalCount == 0 ? "No completed receipts matched the filters." : string.Format("{0:N0} receipt(s) found.", result.TotalCount);
                _previous.Enabled = _pageNumber > 1;
                _next.Enabled = _pageNumber < _totalPages;
                ClearEligibility();
            }
            catch (Exception exception) { if (!IsDisposed) ShowError(exception); }
            finally { if (!IsDisposed) SetBusy(false, _status.Text); }
        }

        private async Task LoadEligibilityAsync()
        {
            if (_busy || IsDisposed) return;
            var receipt = _receiptsView.GetFocusedRow() as SaleReceiptSummaryDTO;
            if (receipt == null)
            {
                XtraMessageBox.Show("Select a receipt first.", "Returns", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                SetBusy(true, "Checking return eligibility...");
                _eligibility = await Task.Run(() => _returns.GetEligibility(receipt.SaleId));
                if (IsDisposed) return;
                _linesGrid.DataSource = new BindingList<SaleReturnEligibleLineDTO>(_eligibility.Lines);
                _paymentsGrid.DataSource = new BindingList<SaleReturnEligiblePaymentDTO>(_eligibility.Payments);
                HideColumn(_linesView, "SaleItemId");
                HideColumn(_linesView, "ProductId");
                HideColumn(_paymentsView, "OriginalPaymentId");
                _linesView.BestFitColumns();
                _paymentsView.BestFitColumns();
                var available = _eligibility.Lines.Any(x => x.EligibleQuantity > 0) && _eligibility.Payments.Any(x => x.EligibleAmount > 0);
                _startReturn.Enabled = available;
                _eligibilityHeader.Text = string.Format(
                    "Receipt {0} | Sold {1:g} | Customer: {2} | Original: PHP {3:N2} | Previously refunded: PHP {4:N2}\r\nDeadline: {5:g}{6}",
                    _eligibility.ReceiptNumber, _eligibility.SaleDateUtc.ToLocalTime(), _eligibility.CustomerName ?? "Walk-in",
                    _eligibility.OriginalTotal, _eligibility.PreviouslyRefunded, _eligibility.ReturnDeadlineUtc.ToLocalTime(),
                    _eligibility.RequiresLateApproval ? " | Manager approval required" : string.Empty);
                _status.Text = available ? "Eligible quantities are available. Start Return hands this receipt to the posting form." : "This receipt has no remaining eligible quantity or refundable payment.";
            }
            catch (Exception exception)
            {
                ClearEligibility();
                ShowError(exception);
            }
            finally { if (!IsDisposed) SetBusy(false, _status.Text); }
        }

        private void StartReturn(object sender, EventArgs e)
        {
            if (_eligibility == null) return;
            var handler = ReturnRequested;
            if (handler == null)
            {
                XtraMessageBox.Show(
                    "The return posting form has not been connected yet. Eligibility is ready and no transaction was changed.",
                    "Returns", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            handler(this, new ReturnRequestedEventArgs(_eligibility.SaleId));
        }

        private void ClearEligibility()
        {
            _eligibility = null;
            _linesGrid.DataSource = null;
            _paymentsGrid.DataSource = null;
            _startReturn.Enabled = false;
            _eligibilityHeader.Text = "Select a completed receipt and check its return eligibility.";
        }

        private static void HideColumn(GridView view, string fieldName)
        {
            var column = view.Columns.ColumnByFieldName(fieldName);
            if (column != null) column.Visible = false;
        }

        private void SetBusy(bool busy, string status)
        {
            _busy = busy;
            _apply.Enabled = !busy;
            _clear.Enabled = !busy;
            _checkEligibility.Enabled = !busy;
            if (busy) _startReturn.Enabled = false;
            _status.Text = status;
        }

        private static void ShowError(Exception exception)
        {
            var message = exception is UnauthorizedAccessException || exception is InvalidOperationException || exception is System.ComponentModel.DataAnnotations.ValidationException
                ? exception.Message
                : "Return eligibility could not be loaded. Please try again.";
            XtraMessageBox.Show(message, "Returns", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
