using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using POS.Models.Operations;
using POS.Services.Point_of_Sale;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace POS.Forms
{
    public sealed class ucReceipts : XtraUserControl
    {
        private readonly SaleReceiptService _service;
        private readonly TextEdit txtSearch = new TextEdit();
        private readonly DateEdit deFrom = new DateEdit();
        private readonly DateEdit deTo = new DateEdit();
        private readonly SimpleButton btnApply = new SimpleButton { Text = "Apply" };
        private readonly SimpleButton btnClear = new SimpleButton { Text = "Clear" };
        private readonly SimpleButton btnPrevious = new SimpleButton { Text = "Previous" };
        private readonly SimpleButton btnNext = new SimpleButton { Text = "Next" };
        private readonly SimpleButton btnPrint = new SimpleButton { Text = "Print Receipt" };
        private readonly SimpleButton btnReprint = new SimpleButton { Text = "Reprint" };
        private readonly LabelControl lblPage = new LabelControl();
        private readonly LabelControl lblStatus = new LabelControl();
        private readonly LabelControl lblDetail = new LabelControl { AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.None };
        private readonly GridControl gcReceipts = new GridControl();
        private readonly GridView gvReceipts = new GridView();
        private readonly GridControl gcLines = new GridControl();
        private readonly GridView gvLines = new GridView();
        private readonly GridControl gcPayments = new GridControl();
        private readonly GridView gvPayments = new GridView();
        private int _pageNumber = 1;
        private int _totalPages = 1;
        private bool _busy;
        private SaleReceiptDTO _selected;

        public ucReceipts(SaleReceiptService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            BuildUi();
        }

        public event EventHandler ReceiptChanged;

        private void BuildUi()
        {
            Name = "ucReceipts";
            Dock = DockStyle.Fill;
            var filters = new PanelControl { Dock = DockStyle.Top, Height = 72 };
            AddEditor(filters, "Search", txtSearch, 8, 8, 210);
            AddEditor(filters, "From", deFrom, 230, 8, 130);
            AddEditor(filters, "To", deTo, 370, 8, 130);
            btnApply.Location = new Point(515, 25); btnClear.Location = new Point(595, 25);
            btnPrevious.Location = new Point(675, 25); btnNext.Location = new Point(765, 25);
            lblPage.Location = new Point(855, 30); lblPage.AutoSize = true;
            filters.Controls.AddRange(new Control[] { btnApply, btnClear, btnPrevious, btnNext, lblPage });

            ConfigureGrid(gcReceipts, gvReceipts);
            ConfigureGrid(gcLines, gvLines);
            ConfigureGrid(gcPayments, gvPayments);
            var detail = new PanelControl { Dock = DockStyle.Fill };
            lblDetail.Dock = DockStyle.Top; lblDetail.Height = 66; lblDetail.Padding = new Padding(8);
            var detailButtons = new PanelControl { Dock = DockStyle.Bottom, Height = 42 };
            btnPrint.Location = new Point(8, 7); btnReprint.Location = new Point(120, 7);
            detailButtons.Controls.AddRange(new Control[] { btnPrint, btnReprint });
            var detailSplit = new SplitContainerControl { Dock = DockStyle.Fill, Horizontal = true, SplitterPosition = 250 };
            detailSplit.Panel1.Controls.Add(gcLines); detailSplit.Panel2.Controls.Add(gcPayments);
            detail.Controls.Add(detailSplit); detail.Controls.Add(detailButtons); detail.Controls.Add(lblDetail);
            var split = new SplitContainerControl { Dock = DockStyle.Fill, SplitterPosition = 540 };
            split.Panel1.Controls.Add(gcReceipts); split.Panel2.Controls.Add(detail);
            lblStatus.Dock = DockStyle.Bottom; lblStatus.Height = 28; lblStatus.Padding = new Padding(8);
            Controls.Add(split); Controls.Add(lblStatus); Controls.Add(filters);

            btnApply.Click += async (s, e) => { _pageNumber = 1; await LoadAsync(); };
            btnClear.Click += async (s, e) => { txtSearch.Text = string.Empty; deFrom.EditValue = null; deTo.EditValue = null; _pageNumber = 1; await LoadAsync(); };
            btnPrevious.Click += async (s, e) => { if (_pageNumber > 1) { _pageNumber--; await LoadAsync(); } };
            btnNext.Click += async (s, e) => { if (_pageNumber < _totalPages) { _pageNumber++; await LoadAsync(); } };
            gvReceipts.FocusedRowChanged += async (s, e) => await LoadSelectedAsync();
            gvReceipts.DoubleClick += async (s, e) => await LoadSelectedAsync();
            btnPrint.Click += async (s, e) => await PrintAsync(false);
            btnReprint.Click += async (s, e) => await PrintAsync(true);
            Load += async (s, e) => await LoadAsync();
        }

        private async Task LoadAsync()
        {
            if (_busy || IsDisposed) return;
            try
            {
                if (deFrom.EditValue != null && deTo.EditValue != null && deTo.DateTime.Date < deFrom.DateTime.Date)
                    throw new InvalidOperationException("The ending date cannot be before the starting date.");
                SetBusy(true, "Loading receipts...");
                var page = await Task.Run(() => _service.Search(new SaleReceiptSearchDTO
                {
                    Search = txtSearch.Text.Trim(),
                    FromUtc = deFrom.EditValue == null ? (DateTime?)null : deFrom.DateTime.Date.ToUniversalTime(),
                    ToUtcExclusive = deTo.EditValue == null ? (DateTime?)null : deTo.DateTime.Date.AddDays(1).ToUniversalTime(),
                    PageNumber = _pageNumber, PageSize = 50
                }));
                if (IsDisposed) return;
                _pageNumber = page.PageNumber; _totalPages = page.TotalPages;
                gcReceipts.DataSource = new BindingList<SaleReceiptSummaryDTO>(page.Items);
                lblPage.Text = string.Format("Page {0} of {1}", _pageNumber, _totalPages);
                lblStatus.Text = page.TotalCount == 0 ? "No receipts found." : string.Format("{0:N0} receipt(s) found.", page.TotalCount);
                btnPrevious.Enabled = _pageNumber > 1; btnNext.Enabled = _pageNumber < _totalPages;
                ClearDetail();
            }
            catch (Exception ex) { ShowError(ex); }
            finally { if (!IsDisposed) SetBusy(false, lblStatus.Text); }
        }

        private async Task LoadSelectedAsync()
        {
            if (_busy || IsDisposed) return;
            var row = gvReceipts.GetFocusedRow() as SaleReceiptSummaryDTO;
            if (row == null) { ClearDetail(); return; }
            try
            {
                SetBusy(true, "Loading receipt details...");
                _selected = await Task.Run(() => _service.Get(row.SaleId));
                gcLines.DataSource = new BindingList<SaleReceiptLineDTO>(_selected.Items);
                gcPayments.DataSource = new BindingList<SaleReceiptPaymentDTO>(_selected.Payments);
                lblDetail.Text = string.Format("Receipt {0} | {1:g} | {2} | Total {3} {4:N2}\r\nStore: {5} | Register: {6} | Cashier: {7}",
                    _selected.ReceiptNumber, _selected.SaleDateUtc.ToLocalTime(), _selected.Status,
                    _selected.CurrencyCode, _selected.TotalAmount, _selected.StoreName, _selected.RegisterCode, _selected.CashierName);
                btnPrint.Enabled = true; btnReprint.Enabled = true; ReceiptChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex) { ClearDetail(); ShowError(ex); }
            finally { if (!IsDisposed) SetBusy(false, lblStatus.Text); }
        }

        private async Task PrintAsync(bool reprint)
        {
            if (_busy || _selected == null) return;
            if (reprint && XtraMessageBox.Show(this, "Print a visibly marked reprint of this receipt?", "Confirm Reprint", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try { SetBusy(true, reprint ? "Printing reprint..." : "Printing receipt..."); await Task.Run(() => _service.Print(_selected.SaleId, reprint)); lblStatus.Text = reprint ? "Receipt reprinted." : "Receipt printed."; }
            catch (Exception ex) { ShowError(ex); }
            finally { if (!IsDisposed) SetBusy(false, lblStatus.Text); }
        }

        private void ClearDetail() { _selected = null; gcLines.DataSource = null; gcPayments.DataSource = null; lblDetail.Text = "Select a receipt to view its stored details."; btnPrint.Enabled = false; btnReprint.Enabled = false; }
        private void SetBusy(bool busy, string status) { _busy = busy; btnApply.Enabled = !busy; btnClear.Enabled = !busy; btnPrevious.Enabled = !busy && _pageNumber > 1; btnNext.Enabled = !busy && _pageNumber < _totalPages; btnPrint.Enabled = !busy && _selected != null; btnReprint.Enabled = !busy && _selected != null; lblStatus.Text = status; UseWaitCursor = busy; }
        private static void AddEditor(Control parent, string caption, BaseEdit editor, int x, int y, int width) { parent.Controls.Add(new LabelControl { Text = caption, Location = new Point(x, y) }); editor.Location = new Point(x, y + 18); editor.Size = new Size(width, 24); parent.Controls.Add(editor); }
        private static void ConfigureGrid(GridControl grid, GridView view) { grid.Dock = DockStyle.Fill; grid.MainView = view; grid.ViewCollection.Add(view); view.OptionsBehavior.Editable = false; view.OptionsView.ShowGroupPanel = false; view.OptionsView.ShowAutoFilterRow = true; }
        private static void ShowError(Exception ex) { var message = ex is InvalidOperationException || ex is UnauthorizedAccessException ? ex.Message : "The receipt operation could not be completed. Please try again."; XtraMessageBox.Show(message, "Receipts", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
}
