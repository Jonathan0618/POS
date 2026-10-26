using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraLayout;
using POS.Common.Enumerations;
using POS.Core;
using POS.Models.Operations;
using POS.Services.Point_of_Sale;
using POS.Services.Shifts;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace POS.Forms
{
    public sealed class frmSaleReturn : XtraForm
    {
        private readonly SaleReturnService _returns;
        private readonly CashierShiftService _shifts;
        private readonly SaleReturnEligibilityDTO _eligibility;
        private readonly BindingList<ReturnLineRow> _lines;
        private readonly BindingList<RefundRow> _refunds;
        private readonly GridControl gcLines = new GridControl();
        private readonly GridView gvLines = new GridView();
        private readonly GridControl gcRefunds = new GridControl();
        private readonly GridView gvRefunds = new GridView();
        private readonly MemoEdit memReason = new MemoEdit();
        private readonly SpinEdit spnExchangeSale = new SpinEdit();
        private readonly LabelControl lblHeader = new LabelControl { AutoSizeMode = LabelAutoSizeMode.None };
        private readonly LabelControl lblState = new LabelControl { AutoSizeMode = LabelAutoSizeMode.None };
        private readonly SimpleButton btnPost = new SimpleButton { Text = "Post Return" };
        private readonly SimpleButton btnCancel = new SimpleButton { Text = "Cancel" };
        private CashierShiftDTO _shift;
        private Guid _requestId = Guid.NewGuid();
        private bool _knownRejection;
        private bool _uncertainOutcome;
        private bool _busy;

        public frmSaleReturn(SaleReturnService returns, CashierShiftService shifts, SaleReturnEligibilityDTO eligibility)
        {
            _returns = returns ?? throw new ArgumentNullException(nameof(returns));
            _shifts = shifts ?? throw new ArgumentNullException(nameof(shifts));
            _eligibility = eligibility ?? throw new ArgumentNullException(nameof(eligibility));
            _lines = new BindingList<ReturnLineRow>(eligibility.Lines.Select(x => new ReturnLineRow(x)).ToList());
            _refunds = new BindingList<RefundRow>(eligibility.Payments.Select(x => new RefundRow(x)).ToList());
            BuildUi();
        }

        public SaleReturnResultDTO Result { get; private set; }

        private void BuildUi()
        {
            Name = "frmSaleReturn"; Text = "Return / Refund"; StartPosition = FormStartPosition.CenterParent;
            ClientSize = new System.Drawing.Size(900, 680); MinimizeBox = false; MaximizeBox = false;
            lblHeader.Text = string.Format("Receipt {0} | Sold {1:g} | Original PHP {2:N2} | Refunded PHP {3:N2}\r\nDeadline {4:g}{5}",
                _eligibility.ReceiptNumber, _eligibility.SaleDateUtc.ToLocalTime(), _eligibility.OriginalTotal,
                _eligibility.PreviouslyRefunded, _eligibility.ReturnDeadlineUtc.ToLocalTime(),
                _eligibility.RequiresLateApproval ? " | Manager approval required" : string.Empty);
            ConfigureGrid(gcLines, gvLines, _lines);
            ConfigureGrid(gcRefunds, gvRefunds, _refunds);
            spnExchangeSale.Properties.IsFloatValue = false; spnExchangeSale.Properties.MinValue = 0; spnExchangeSale.Properties.MaxValue = int.MaxValue;
            memReason.Properties.MaxLength = 250;
            var layout = new LayoutControl { Dock = DockStyle.Fill }; Controls.Add(layout);
            layout.Controls.AddRange(new Control[] { lblHeader, gcLines, gcRefunds, memReason, spnExchangeSale, lblState, btnPost, btnCancel });
            var root = new LayoutControlGroup { EnableIndentsWithoutBorders = DevExpress.Utils.DefaultBoolean.True }; layout.Root = root;
            var header = root.AddItem(string.Empty, lblHeader); header.TextVisible = false; header.MinSize = new System.Drawing.Size(0, 62);
            var lines = root.AddItem("Return items", gcLines); lines.MinSize = new System.Drawing.Size(0, 210);
            var refunds = root.AddItem("Refund allocation", gcRefunds); refunds.MinSize = new System.Drawing.Size(0, 170);
            root.AddItem("Reason", memReason);
            root.AddItem("Exchange sale ID (optional)", spnExchangeSale);
            var state = root.AddItem(string.Empty, lblState); state.TextVisible = false; state.MinSize = new System.Drawing.Size(0, 35);
            var buttons = new LayoutControlGroup { GroupBordersVisible = false };
            buttons.AddItem(string.Empty, btnPost).TextVisible = false; buttons.AddItem(string.Empty, btnCancel).TextVisible = false; root.Add(buttons);
            AcceptButton = btnPost; CancelButton = btnCancel; btnCancel.DialogResult = DialogResult.Cancel;
            btnPost.Click += async (sender, args) => await PostAsync();
            Shown += async (sender, args) => await LoadShiftAsync();
        }

        private static void ConfigureGrid(GridControl grid, GridView view, object data)
        {
            grid.MainView = view; grid.ViewCollection.Add(view); grid.DataSource = data;
            view.OptionsView.ShowGroupPanel = false; view.OptionsView.ShowAutoFilterRow = false;
        }

        private async Task LoadShiftAsync()
        {
            try
            {
                SetBusy(true, "Finding your open cashier shift...");
                var page = await Task.Run(() => _shifts.Search(new CashierShiftSearchDTO
                {
                    CashierUserId = CurrentUser.UserId, Status = "Open", PageNumber = 1, PageSize = 10
                }));
                _shift = page.Items.SingleOrDefault();
                if (_shift == null) throw new ValidationException("Open your cashier shift before posting a return.");
                lblState.Text = string.Format("Return will be posted to register {0}, shift #{1}.", _shift.RegisterCode, _shift.Id);
            }
            catch (Exception exception) { lblState.Text = SafeMessage(exception); ShowError(exception); }
            finally { if (!IsDisposed) SetBusy(false, lblState.Text); }
        }

        private async Task PostAsync()
        {
            if (_busy || _shift == null || !ValidateInput()) return;
            if (_knownRejection) { _requestId = Guid.NewGuid(); _knownRejection = false; }
            var request = new SaleReturnPostDTO
            {
                RequestId = _requestId, SaleId = _eligibility.SaleId, RegisterStationId = _shift.RegisterStationId,
                CashierShiftId = _shift.Id, ExchangeSaleId = spnExchangeSale.Value > 0 ? (int?)Convert.ToInt32(spnExchangeSale.Value) : null,
                Reason = memReason.Text.Trim(),
                Lines = _lines.Where(x => x.Quantity > 0).Select(x => new SaleReturnLinePostDTO
                { SaleItemId = x.SaleItemId, Quantity = x.Quantity, Disposition = x.Disposition }).ToList(),
                Refunds = _refunds.Where(x => x.Amount > 0).Select(x => new RefundTenderPostDTO
                { OriginalPaymentId = x.OriginalPaymentId, Amount = x.Amount, ExternalReference = Clean(x.ExternalReference) }).ToList()
            };
            if (XtraMessageBox.Show(this, "Post this return and refund? Completed return facts cannot be edited.", "Return / Refund",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try
            {
                SetBusy(true, "Posting return...");
                Result = await Task.Run(() => _returns.Post(request));
                XtraMessageBox.Show(this, string.Format("Return {0} posted for PHP {1:N2}.", Result.ReturnNumber, Result.TotalAmount),
                    "Return Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.OK; Close();
            }
            catch (ValidationException exception) { _knownRejection = true; lblState.Text = exception.Message; ShowError(exception); }
            catch (UnauthorizedAccessException exception) { _knownRejection = true; lblState.Text = exception.Message; ShowError(exception); }
            catch (Exception exception) { _uncertainOutcome = true; lblState.Text = "The outcome is uncertain. Retry the unchanged request before editing it."; ShowError(exception); }
            finally { if (!IsDisposed) SetBusy(false, lblState.Text); }
        }

        private bool ValidateInput()
        {
            if (string.IsNullOrWhiteSpace(memReason.Text)) return Warn("A return reason is required.");
            if (!_lines.Any(x => x.Quantity > 0)) return Warn("Select at least one item quantity to return.");
            if (_lines.Any(x => x.Quantity < 0 || x.Quantity > x.EligibleQuantity)) return Warn("A return quantity exceeds the eligible quantity.");
            if (!_refunds.Any(x => x.Amount > 0)) return Warn("Allocate the refund to at least one original payment.");
            if (_refunds.Any(x => x.Amount < 0 || x.Amount > x.EligibleAmount)) return Warn("A refund amount exceeds its eligible payment amount.");
            return true;
        }
        private bool Warn(string message) { XtraMessageBox.Show(this, message, "Return / Refund", MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; }
        private void SetBusy(bool busy, string state) { _busy = busy; gcLines.Enabled = !busy && !_uncertainOutcome; gcRefunds.Enabled = !busy && !_uncertainOutcome; memReason.Enabled = !busy && !_uncertainOutcome; spnExchangeSale.Enabled = !busy && !_uncertainOutcome; btnPost.Enabled = !busy && _shift != null; btnPost.Text = _uncertainOutcome ? "Retry Same Return" : "Post Return"; btnCancel.Enabled = !busy && !_uncertainOutcome; ControlBox = !_uncertainOutcome; lblState.Text = state; UseWaitCursor = busy; }
        private static string Clean(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        private static string SafeMessage(Exception exception) => exception is ValidationException || exception is UnauthorizedAccessException || exception is InvalidOperationException ? exception.Message : "The return could not be completed. Please try again.";
        private static void ShowError(Exception exception) { XtraMessageBox.Show(SafeMessage(exception), "Return / Refund", MessageBoxButtons.OK, MessageBoxIcon.Warning); }

        private sealed class ReturnLineRow
        {
            public ReturnLineRow(SaleReturnEligibleLineDTO source) { Source = source; Disposition = ReturnDisposition.Restock; }
            [Browsable(false)] public SaleReturnEligibleLineDTO Source { get; }
            [Browsable(false)] public int SaleItemId => Source.SaleItemId;
            public string ProductName => Source.ProductName;
            public int PurchasedQuantity => Source.PurchasedQuantity;
            public int ReturnedQuantity => Source.ReturnedQuantity;
            public int EligibleQuantity => Source.EligibleQuantity;
            public decimal ApproximateRefundPerUnit => Source.ApproximateRefundPerUnit;
            public int Quantity { get; set; }
            public ReturnDisposition Disposition { get; set; }
        }
        private sealed class RefundRow
        {
            public RefundRow(SaleReturnEligiblePaymentDTO source) { Source = source; }
            [Browsable(false)] public SaleReturnEligiblePaymentDTO Source { get; }
            [Browsable(false)] public long OriginalPaymentId => Source.OriginalPaymentId;
            public TenderType TenderType => Source.TenderType;
            public decimal OriginalAppliedAmount => Source.OriginalAppliedAmount;
            public decimal RefundedAmount => Source.RefundedAmount;
            public decimal EligibleAmount => Source.EligibleAmount;
            public decimal Amount { get; set; }
            public string ExternalReference { get; set; }
        }
    }
}
