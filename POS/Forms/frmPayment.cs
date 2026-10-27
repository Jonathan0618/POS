using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using POS.Common.Enumerations;
using POS.Models.Operations;
using POS.Services.Point_of_Sale;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace POS.Forms
{
    public sealed class frmPayment : XtraForm
    {
        private readonly SalesService _salesService;
        private readonly SaleReceiptService _receiptService;
        private readonly SaleCartDTO _cart;
        private readonly CashierShiftDTO _shift;
        private readonly int? _heldSaleId;
        private readonly string _heldRevision;
        private readonly BindingList<TenderRow> _tenders = new BindingList<TenderRow>();
        private readonly GridControl _grid = new GridControl();
        private readonly GridView _view = new GridView();
        private readonly LabelControl _due = new LabelControl();
        private readonly LabelControl _tendered = new LabelControl();
        private readonly LabelControl _remaining = new LabelControl();
        private readonly LabelControl _change = new LabelControl();
        private readonly LabelControl _paymentStatus = new LabelControl();
        private readonly SimpleButton _add = new SimpleButton { Text = "Add tender" };
        private readonly SimpleButton _remove = new SimpleButton { Text = "Remove tender" };
        private readonly SimpleButton _pay = new SimpleButton { Name = "btnPay", Text = "Pay" };
        private SaleCheckoutDTO _pendingCommand;
        private int? _committedSaleId;
        private bool _busy;
        private bool _completed;
        private bool _rejectedDetailsMustChange;
        private bool _uncertainOutcome;

        public frmPayment() : this(null, null, null, null, null) { }

        public frmPayment(SalesService salesService, SaleReceiptService receiptService, SaleCartDTO cart,
            CashierShiftDTO shift, HeldSaleDTO heldSale)
        {
            _salesService = salesService;
            _receiptService = receiptService;
            _cart = cart;
            _shift = shift;
            _heldSaleId = heldSale == null ? (int?)null : heldSale.Id;
            _heldRevision = heldSale == null ? null : heldSale.Revision;
            Text = "Payment";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new System.Drawing.Size(760, 560);
            Size = new System.Drawing.Size(820, 620);
            BuildUi();
            _tenders.ListChanged += (sender, args) => DetailsChanged();
            _tenders.Add(new TenderRow { TenderType = TenderType.Cash, Amount = cart == null ? 0 : cart.TotalAmount });
        }

        public bool CheckoutConfirmed { get { return _completed; } }

        private void BuildUi()
        {
            var header = new TableLayoutPanel { Dock = DockStyle.Top, Height = 100, ColumnCount = 4, Padding = new Padding(16) };
            for (var i = 0; i < 4; i++) header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            header.Controls.Add(Caption("Amount due", _due), 0, 0);
            header.Controls.Add(Caption("Tendered", _tendered), 1, 0);
            header.Controls.Add(Caption("Remaining", _remaining), 2, 0);
            header.Controls.Add(Caption("Change", _change), 3, 0);

            _grid.Dock = DockStyle.Fill;
            _grid.MainView = _view;
            _grid.ViewCollection.Add(_view);
            _grid.DataSource = _tenders;
            _view.OptionsView.ShowGroupPanel = false;
            var typeColumn = _view.Columns.AddVisible(nameof(TenderRow.TenderType), "Tender type");
            var types = new DevExpress.XtraEditors.Repository.RepositoryItemLookUpEdit();
            types.DataSource = Enum.GetValues(typeof(TenderType));
            _grid.RepositoryItems.Add(types);
            typeColumn.ColumnEdit = types;
            var amount = _view.Columns.AddVisible(nameof(TenderRow.Amount), "Amount");
            amount.DisplayFormat.FormatType = DevExpress.Utils.FormatType.Numeric;
            amount.DisplayFormat.FormatString = "n" + MoneyDecimalPlaces();
            var reference = _view.Columns.AddVisible(nameof(TenderRow.ExternalReference), "Payment reference (non-cash)");
            reference.ToolTip = "Enter the terminal receipt, approval, or transaction reference for non-cash payments. Leave blank for cash. Never enter card PAN, CVV, PIN, or access tokens.";
            _view.CellValueChanged += (sender, args) => DetailsChanged();

            var guidance = new PanelControl { Dock = DockStyle.Bottom, Height = 48, Padding = new Padding(14, 7, 14, 5) };
            var warning = new LabelControl
            {
                Dock = DockStyle.Top,
                Text = "Cash needs no reference. For card, e-wallet, or store credit, enter the terminal or approval reference only. Never enter PAN, CVV, PIN, or access tokens."
            };
            warning.Appearance.ForeColor = System.Drawing.Color.Firebrick;
            warning.Appearance.Options.UseForeColor = true;
            _paymentStatus.Dock = DockStyle.Bottom;
            guidance.Controls.Add(_paymentStatus);
            guidance.Controls.Add(warning);

            var footer = new PanelControl { Dock = DockStyle.Bottom, Height = 66 };
            _add.SetBounds(14, 14, 110, 36);
            _remove.SetBounds(132, 14, 120, 36);
            _pay.SetBounds(650, 14, 130, 36);
            _pay.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            footer.Controls.AddRange(new Control[] { _add, _remove, _pay });
            Controls.Add(_grid);
            Controls.Add(header);
            Controls.Add(guidance);
            Controls.Add(footer);
            _add.Click += (sender, args) => AddTender();
            _remove.Click += (sender, args) => RemoveTender();
            _pay.Click += async (sender, args) => await PayAsync();
            AcceptButton = _pay;
            FormClosing += PaymentFormClosing;
        }

        private static Control Caption(string caption, LabelControl value)
        {
            var panel = new PanelControl { Dock = DockStyle.Fill, BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder };
            var label = new LabelControl { Text = caption };
            label.SetBounds(2, 2, 150, 20);
            value.Appearance.Font = new System.Drawing.Font("Segoe UI", 14F, System.Drawing.FontStyle.Bold);
            value.SetBounds(2, 28, 170, 32);
            panel.Controls.Add(label);
            panel.Controls.Add(value);
            return panel;
        }

        private void AddTender()
        {
            if (_busy) return;
            if (_tenders.Count >= 10)
            {
                XtraMessageBox.Show("A checkout can contain at most 10 tenders.", "Payment");
                return;
            }
            _tenders.Add(new TenderRow { TenderType = TenderType.Cash });
        }

        private void RemoveTender()
        {
            if (_busy || _tenders.Count <= 1) return;
            var row = _view.GetFocusedRow() as TenderRow;
            if (row != null) _tenders.Remove(row);
        }

        private void RefreshSummary()
        {
            var due = _cart == null ? 0 : _cart.TotalAmount;
            var tendered = _tenders.Sum(x => x.Amount);
            var cash = _tenders.Where(x => x.TenderType == TenderType.Cash).Sum(x => x.Amount);
            _due.Text = Money(due);
            _tendered.Text = Money(tendered);
            _remaining.Text = Money(Math.Max(0, due - tendered));
            _change.Text = Money(cash > 0 ? Math.Max(0, tendered - due) : 0);
        }

        private void DetailsChanged()
        {
            if (_uncertainOutcome || _committedSaleId.HasValue) return;
            _pendingCommand = null;
            _rejectedDetailsMustChange = false;
            RefreshSummary();
        }

        private string Money(decimal amount)
        {
            return string.Format("{0} {1:N" + MoneyDecimalPlaces() + "}",
                _cart == null ? "PHP" : _cart.CurrencyCode, amount);
        }

        private int MoneyDecimalPlaces()
        {
            return _cart == null ? 2 : Math.Max(0, Math.Min(4, _cart.MoneyDecimalPlaces));
        }

        private async Task PayAsync()
        {
            if (_busy || _completed || _salesService == null || _receiptService == null) return;
            try
            {
                _view.CloseEditor();
                _view.UpdateCurrentRow();
                if (_rejectedDetailsMustChange)
                    throw new ValidationException("Change the rejected payment details before submitting a new request.");
                ValidateTenderRows();
                if (_pendingCommand == null) _pendingCommand = BuildCommand();
                SetBusy(true);
                if (!_committedSaleId.HasValue)
                {
                    var sale = await Task.Run(() => _salesService.Checkout(_pendingCommand));
                    _committedSaleId = sale.Id;
                    _uncertainOutcome = false;
                }
                var receipt = await Task.Run(() => _receiptService.Get(_committedSaleId.Value));
                _completed = true;
                using (var complete = new frmCheckoutComplete(_receiptService, receipt))
                    complete.ShowDialog(this);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception exception)
            {
                if (_committedSaleId.HasValue)
                {
                    _uncertainOutcome = true;
                }
                else if (IsKnownRejection(exception) && !RequiresIdenticalRetry(exception))
                {
                    _pendingCommand = null;
                    _rejectedDetailsMustChange = true;
                    _uncertainOutcome = false;
                }
                else
                {
                    _uncertainOutcome = true;
                }
                var message = _committedSaleId.HasValue
                    ? "The sale is committed, but its receipt could not be loaded. Retry receipt retrieval before starting another sale."
                    : SafeMessage(exception);
                XtraMessageBox.Show(message, "Payment", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally { if (!IsDisposed) SetBusy(false); }
        }

        private void ValidateTenderRows()
        {
            if (_tenders.Count < 1 || _tenders.Count > 10) throw new ValidationException("Enter between 1 and 10 tenders.");
            if (_tenders.Any(x => x.Amount <= 0)) throw new ValidationException("Every tender amount must be positive.");
            if (_tenders.Any(x => x.TenderType != TenderType.Cash && string.IsNullOrWhiteSpace(x.ExternalReference)))
                throw new ValidationException("Every non-cash payment requires a terminal or approval reference. Cash does not.");
            if (_tenders.Any(x => x.TenderType == TenderType.StoreCredit) && (!_cart.CustomerId.HasValue || _cart.CustomerId.Value <= 0))
                throw new ValidationException("Store credit requires a selected customer.");
            var nonCash = _tenders.Where(x => x.TenderType != TenderType.Cash).Sum(x => x.Amount);
            if (nonCash > _cart.TotalAmount) throw new ValidationException("Non-cash tenders cannot exceed the amount due.");
            if (_tenders.Sum(x => x.Amount) < _cart.TotalAmount) throw new ValidationException("Payment is less than the amount due.");
            if (_tenders.Sum(x => x.Amount) > _cart.TotalAmount && !_tenders.Any(x => x.TenderType == TenderType.Cash))
                throw new ValidationException("Only cash can produce change.");
        }

        private SaleCheckoutDTO BuildCommand()
        {
            return new SaleCheckoutDTO
            {
                RequestId = Guid.NewGuid(),
                RegisterStationId = _cart.RegisterStationId,
                CashierShiftId = _shift.Id,
                CustomerId = _cart.CustomerId,
                DiscountAmount = _cart.DiscountAmount,
                HeldSaleId = _heldSaleId,
                HeldRevision = _heldRevision,
                Items = _cart.Items.Select(x => new SaleCheckoutLineDTO { ProductId = x.ProductId, Quantity = x.Quantity }).ToList(),
                Tenders = _tenders.Select(x => new SaleTenderDTO
                {
                    TenderType = x.TenderType,
                    Amount = x.Amount,
                    ExternalReference = x.TenderType == TenderType.Cash ? null : x.ExternalReference.Trim()
                }).ToList()
            };
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            var detailsEditable = !busy && !_uncertainOutcome && !_committedSaleId.HasValue;
            _grid.Enabled = detailsEditable;
            _add.Enabled = detailsEditable;
            _remove.Enabled = detailsEditable;
            _pay.Enabled = !busy;
            _pay.Text = _committedSaleId.HasValue ? "Retry receipt" :
                (_uncertainOutcome ? "Retry unchanged" : "Pay");
            _paymentStatus.Text = busy ? "Checkout is processing. Keep this window open." :
                (_committedSaleId.HasValue ? "Sale committed; receipt retrieval is pending." :
                    (_uncertainOutcome ? "Outcome uncertain; retry the unchanged request." : string.Empty));
            UseWaitCursor = busy;
        }

        private void PaymentFormClosing(object sender, FormClosingEventArgs args)
        {
            if (_completed) return;
            if (_busy)
            {
                args.Cancel = true;
                XtraMessageBox.Show(this, "Checkout is still processing. Wait for it to finish before closing this window.",
                    "Payment", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!_uncertainOutcome && !_committedSaleId.HasValue) return;
            args.Cancel = true;
            XtraMessageBox.Show(this,
                _committedSaleId.HasValue
                    ? "The sale is committed. Retry receipt retrieval before starting another sale."
                    : "The checkout outcome is uncertain. Retry the unchanged request before closing.",
                "Payment", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private static bool IsKnownRejection(Exception exception)
        {
            return exception is ValidationException || exception is InvalidOperationException || exception is UnauthorizedAccessException ||
                exception is ArgumentException;
        }

        private static bool RequiresIdenticalRetry(Exception exception)
        {
            var message = exception == null ? string.Empty : exception.Message ?? string.Empty;
            return message.IndexOf("same request ID", StringComparison.OrdinalIgnoreCase) >= 0 ||
                message.IndexOf("committed concurrently", StringComparison.OrdinalIgnoreCase) >= 0 ||
                message.IndexOf("outcome is uncertain", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string SafeMessage(Exception exception)
        {
            return IsKnownRejection(exception) ? exception.Message :
                "The checkout result is uncertain. Do not change the details; retry to safely retrieve the same sale.";
        }

        private sealed class TenderRow
        {
            public TenderType TenderType { get; set; }
            public decimal Amount { get; set; }
            public string ExternalReference { get; set; }
        }
    }
}
