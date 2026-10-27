using DevExpress.XtraEditors;
using POS.Core;
using POS.Models.Operations;
using POS.Services.Purchasing;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace POS.Forms
{
    public partial class frmReceivePurchaseOrder : XtraForm
    {
        private readonly PurchaseOrderService _orders;
        private readonly GoodsReceiptService _receipts;
        private BindingList<ReceiptLineRow> _lines = new BindingList<ReceiptLineRow>();
        private PurchaseOrderDetailsDTO _selectedOrder;
        private bool _busy;

        public frmReceivePurchaseOrder() { InitializeComponent(); ConfigureUi(); }
        public frmReceivePurchaseOrder(PurchaseOrderService orders, GoodsReceiptService receipts) : this()
        {
            _orders = orders ?? throw new ArgumentNullException(nameof(orders));
            _receipts = receipts ?? throw new ArgumentNullException(nameof(receipts));
        }

        public int GoodsReceiptId { get; private set; }

        private void ConfigureUi()
        {
            ConfigureResponsiveLayout();
            lkpPurchaseOrder.Properties.DisplayMember = "OrderNumber";
            lkpPurchaseOrder.Properties.ValueMember = "Id";
            lkpPurchaseOrder.Properties.NullText = "Select an ordered purchase order";
            lkpSupplier.Enabled = false; lkpReceivedBy.Enabled = false; deReceiptDate.Enabled = false;
            txtStatus.Properties.ReadOnly = true; txtExpectedDate.Properties.ReadOnly = true;
            txtPOSupplier.Properties.ReadOnly = true; txtPODate.Properties.ReadOnly = true; txtPONumber.Properties.ReadOnly = true;
            var fields = new[] { "RowNo", "ProductName", "Sku", "OrderedQuantity", "ReceivedQuantity", "RemainingQuantity", "ReceiveQuantity", "UnitCost", "ReceiveTotal", "Action" };
            for (var i = 0; i < fields.Length && i < gvItems.Columns.Count; i++) gvItems.Columns[i].FieldName = fields[i];
            gvItems.OptionsView.ShowGroupPanel = false;
            gcItems.DataSource = _lines;
            lkpPurchaseOrder.EditValueChanged += async (sender, args) => await LoadSelectedOrderAsync();
            btnReceiveGoods.Click += async (sender, args) => await ReceiveAsync();
            btnCancel.Click += (sender, args) => { DialogResult = DialogResult.Cancel; Close(); };
        }

        private void ConfigureResponsiveLayout()
        {
            SuspendLayout();
            MinimumSize = new System.Drawing.Size(1260, 680);
            grpReceiptInfo.Dock = DockStyle.Fill;
            grpPODetails.Dock = DockStyle.Fill;
            grpItems.Dock = DockStyle.Fill;
            gcItems.Dock = DockStyle.Fill;
            panelControl1.Dock = DockStyle.Fill;
            panelControl2.Dock = DockStyle.Fill;

            var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
            header.Controls.Add(grpReceiptInfo, 0, 0);
            header.Controls.Add(grpPODetails, 1, 0);

            var summary = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
            summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
            summary.Controls.Add(panelControl1, 0, 0);
            summary.Controls.Add(panelControl2, 1, 0);

            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(8, 7, 8, 5)
            };
            actions.Controls.Add(btnReceiveGoods);
            actions.Controls.Add(btnCancel);

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(12) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 260));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            Controls.Clear();
            root.Controls.Add(header, 0, 0);
            root.Controls.Add(grpItems, 0, 1);
            root.Controls.Add(summary, 0, 2);
            root.Controls.Add(actions, 0, 3);
            Controls.Add(root);
            ResumeLayout(true);
        }

        private async void frmReceivePurchaseOrder_Load(object sender, EventArgs e)
        {
            if (_orders == null || _busy) return;
            try
            {
                SetBusy(true, "Loading ordered purchase orders...");
                var page = await Task.Run(() => _orders.Search(new PurchaseOrderSearchDTO { Status = "Pending", PageNumber = 1, PageSize = 200 }));
                lkpPurchaseOrder.Properties.DataSource = page.Items;
                deReceiptDate.DateTime = DateTime.Now;
                lkpReceivedBy.Text = CurrentUser.Fullname ?? CurrentUser.Username;
                lblStatusTitle.Text = page.TotalCount == 0 ? "No purchase orders are ready to receive." : "Select a purchase order to receive.";
                lblStatusNote.Text = string.Format("{0:N0} ordered or partially received purchase order(s) available.", page.TotalCount);
            }
            catch (Exception exception) { ShowError(exception); }
            finally { if (!IsDisposed) SetBusy(false, lblStatusNote.Text); }
        }

        private async Task LoadSelectedOrderAsync()
        {
            if (_busy || lkpPurchaseOrder.EditValue == null) return;
            try
            {
                SetBusy(true, "Loading purchase-order details...");
                _selectedOrder = await Task.Run(() => _orders.GetDetails(Convert.ToInt32(lkpPurchaseOrder.EditValue)));
                txtPONumber.Text = _selectedOrder.Order.OrderNumber;
                txtPODate.Text = _selectedOrder.Order.CreatedUtc.ToLocalTime().ToString("g");
                txtPOSupplier.Text = string.Format("{0} - {1}", _selectedOrder.Order.SupplierCode, _selectedOrder.Order.SupplierName);
                txtStatus.Text = _selectedOrder.Order.Status; lkpSupplier.Text = _selectedOrder.Order.SupplierName;
                _lines = new BindingList<ReceiptLineRow>(_selectedOrder.Items.Where(x => x.RemainingQuantity > 0)
                    .Select((x, index) => new ReceiptLineRow(x, index + 1)).ToList());
                gcItems.DataSource = _lines; gvItems.BestFitColumns();
                lblStatusTitle.Text = "Purchase order loaded";
                lblStatusNote.Text = "Enter only quantities physically received. Zero rows are not posted.";
                UpdateTotal();
            }
            catch (Exception exception) { _selectedOrder = null; _lines.Clear(); ShowError(exception); }
            finally { if (!IsDisposed) SetBusy(false, lblStatusNote.Text); }
        }

        private async Task ReceiveAsync()
        {
            if (_busy || !ValidateRequest()) return;
            var request = new PurchaseOrderReceiptDTO
            {
                ReceiptNumber = txtReceiptNumber.Text.Trim(), PurchaseOrderId = _selectedOrder.Order.Id,
                SupplierReference = Clean(txtSupplierReference.Text),
                Items = _lines.Where(x => x.ReceiveQuantity > 0).Select(x => new GoodsReceiptLineInputDTO { ProductId = x.ProductId, Quantity = x.ReceiveQuantity }).ToList()
            };
            if (XtraMessageBox.Show(this, string.Format("Post receipt {0} with {1:N0} line(s)?", request.ReceiptNumber, request.Items.Count),
                "Receive Goods", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            try
            {
                SetBusy(true, "Posting goods receipt...");
                GoodsReceiptId = await Task.Run(() => _receipts.ReceivePurchaseOrder(request));
                XtraMessageBox.Show(this, string.Format("Goods receipt #{0} was posted successfully.", GoodsReceiptId), "Receive Goods", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.OK; Close();
            }
            catch (Exception exception) { ShowError(exception); }
            finally { if (!IsDisposed) SetBusy(false, lblStatusNote.Text); }
        }

        private bool ValidateRequest()
        {
            if (_selectedOrder == null) return Warn("Select a purchase order.");
            if (string.IsNullOrWhiteSpace(txtReceiptNumber.Text)) return Warn("Receipt number is required.");
            var selected = _lines.Where(x => x.ReceiveQuantity > 0).ToList();
            if (selected.Count == 0) return Warn("Enter at least one received quantity.");
            if (selected.Any(x => x.ReceiveQuantity > x.RemainingQuantity)) return Warn("Received quantity cannot exceed the remaining ordered quantity.");
            return true;
        }
        private bool Warn(string message) { XtraMessageBox.Show(this, message, "Receive Goods", MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; }
        private void UpdateTotal() { lblTotalLabel.Text = "Receipt total"; btnTotal.Text = "PHP " + _lines.Sum(x => x.ReceiveTotal).ToString("N2"); }
        private void SetBusy(bool busy, string status)
        {
            _busy = busy; grpReceiptInfo.Enabled = !busy; grpItems.Enabled = !busy;
            btnReceiveGoods.Enabled = !busy && _selectedOrder != null; btnCancel.Enabled = !busy; lblStatusNote.Text = status; UseWaitCursor = busy;
        }
        private static string Clean(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        private static void ShowError(Exception exception)
        {
            var message = exception is ValidationException || exception is UnauthorizedAccessException || exception is InvalidOperationException
                ? exception.Message : "The goods receipt could not be loaded or posted. Please try again.";
            XtraMessageBox.Show(message, "Receive Purchase Order", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void grpReceiptInfo_Paint(object sender, PaintEventArgs e) { }
        private void lblPurchaseOrder_Click(object sender, EventArgs e) { }
        private void grpItems_Paint(object sender, PaintEventArgs e) { }
        private void lblStatusTitle_Click(object sender, EventArgs e) { }

        private sealed class ReceiptLineRow : INotifyPropertyChanged
        {
            private int _receiveQuantity;
            public ReceiptLineRow(PurchaseOrderLineDTO line, int rowNo) { Source = line; RowNo = rowNo; }
            [Browsable(false)] public PurchaseOrderLineDTO Source { get; }
            [Browsable(false)] public int ProductId => Source.ProductId;
            public int RowNo { get; }
            public string ProductName => Source.CurrentProductName;
            public string Sku => Source.CurrentSku;
            public int OrderedQuantity => Source.OrderedQuantity;
            public int ReceivedQuantity => Source.ReceivedQuantity;
            public int RemainingQuantity => Source.RemainingQuantity;
            public int ReceiveQuantity { get => _receiveQuantity; set { _receiveQuantity = value; Changed(nameof(ReceiveQuantity)); Changed(nameof(ReceiveTotal)); } }
            public decimal UnitCost => Source.UnitCost;
            public decimal ReceiveTotal => UnitCost * ReceiveQuantity;
            public string Action => ReceiveQuantity > 0 ? "Receive" : string.Empty;
            public event PropertyChangedEventHandler PropertyChanged;
            private void Changed(string name) { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)); }
        }
    }
}
