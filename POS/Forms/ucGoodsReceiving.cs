using DevExpress.XtraEditors;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace POS.Forms
{
    public partial class ucGoodsReceiving : DevExpress.XtraEditors.XtraUserControl
    {
        private readonly Func<Form> _createPurchaseOrderReceipt;
        private bool _opening;

        public ucGoodsReceiving() : this(null)
        {
        }

        public ucGoodsReceiving(Func<Form> createPurchaseOrderReceipt)
        {
            _createPurchaseOrderReceipt = createPurchaseOrderReceipt;
            InitializeComponent();
            ConfigureResponsiveLayout();
            btnNewReceipt.Click += async (sender, args) => await OpenReceiptAsync();
            btnClear.Click += (sender, args) => ClearSummary();
            btnCancel.Click += (sender, args) => ClearSummary();
            btnPrintReview.Enabled = false;
            btnPrintReview.ToolTip = "Print review becomes available after a receipt-detail query is added.";
        }

        private void ConfigureResponsiveLayout()
        {
            SuspendLayout();
            AutoScroll = true;
            panelControl6.Dock = DockStyle.Fill;
            panelControl1.Dock = DockStyle.Fill;
            panelControl2.Dock = DockStyle.Fill;
            panelControl3.Dock = DockStyle.Fill;
            panelControl4.Dock = DockStyle.Fill;
            panelControl5.Dock = DockStyle.Fill;
            gridReceivingItems.Dock = DockStyle.Fill;
            searchControl1.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            btnNewReceipt.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            lblTotalAmount.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            lblPartialReceiving.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            lblNote.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnClear.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnReceiveGoods.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            pictureEdit1.Anchor = AnchorStyles.Top;
            pictureEdit2.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            groupControl1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            groupControl2.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

            var detail = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            detail.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 53));
            detail.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 47));
            detail.Controls.Add(panelControl2, 0, 0);
            detail.Controls.Add(panelControl4, 1, 0);
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5,
                Padding = new Padding(12), MinimumSize = new Size(1050, 760)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 170));
            Controls.Clear();
            root.Controls.Add(panelControl6, 0, 0);
            root.Controls.Add(panelControl1, 0, 1);
            root.Controls.Add(detail, 0, 2);
            root.Controls.Add(panelControl3, 0, 3);
            root.Controls.Add(panelControl5, 0, 4);
            Controls.Add(root);
            ResumeLayout(true);
        }

        private void simpleButton1_Click(object sender, EventArgs e)
        {

        }

        private void labelControl1_Click(object sender, EventArgs e)
        {

        }

        private void labelControl1_Click_1(object sender, EventArgs e)
        {

        }

        private void btnReceiveDirect_Click(object sender, EventArgs e)
        {

        }

        private void ucGoodsReceiving_Load(object sender, EventArgs e)
        {
            ClearSummary();
            lblReceivedBy.Text = POS.Core.CurrentUser.Fullname ?? POS.Core.CurrentUser.Username ?? "Current user";
        }

        private void groupControl1_Paint(object sender, PaintEventArgs e)
        {

        }

        private void panelControl3_Paint(object sender, PaintEventArgs e)
        {

        }

        private void labelControl2_Click(object sender, EventArgs e)
        {

        }

        private void labelControl3_Click(object sender, EventArgs e)
        {

        }

        private void labelControl4_Click(object sender, EventArgs e)
        {

        }

        private void labelControl6_Click(object sender, EventArgs e)
        {

        }

        private void labelControl5_Click(object sender, EventArgs e)
        {

        }

        private void dropDownButton1_Click(object sender, EventArgs e)
        {

        }

        private void labelControl7_Click(object sender, EventArgs e)
        {

        }

        private void labelControl8_Click(object sender, EventArgs e)
        {

        }

        private void searchControl1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void labelControl10_Click(object sender, EventArgs e)
        {

        }

        private void labelControl11_Click(object sender, EventArgs e)
        {

        }

        private void dateEdit1_EditValueChanged(object sender, EventArgs e)
        {

        }

        private void simpleButton5_Click(object sender, EventArgs e)
        {

        }

        private void simpleButton4_Click(object sender, EventArgs e)
        {

        }

        private void simpleButton6_Click(object sender, EventArgs e)
        {

        }

        private void pictureEdit2_EditValueChanged(object sender, EventArgs e)
        {

        }

        private void gridReceivingItems_Click(object sender, EventArgs e)
        {

        }

        private void lblPartialReceiving_Click(object sender, EventArgs e)
        {

        }

        private void lblTotalAmount_Click(object sender, EventArgs e)
        {

        }

        private void panelControl5_Paint(object sender, PaintEventArgs e)
        {

        }

        private void textRemarks_Paint(object sender, PaintEventArgs e)
        {

        }

        private void btnReceiveGoods_Click(object sender, EventArgs e)
        {
            _ = OpenReceiptAsync();
        }

        private async Task OpenReceiptAsync()
        {
            if (_opening) return;
            if (_createPurchaseOrderReceipt == null)
            {
                XtraMessageBox.Show("Purchase-order receiving is unavailable because its service factory was not supplied.",
                    "Goods Receiving", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try
            {
                _opening = true;
                btnNewReceipt.Enabled = false;
                btnReceiveGoods.Enabled = false;
                using (var form = _createPurchaseOrderReceipt())
                {
                    if (form.ShowDialog(FindForm()) == DialogResult.OK)
                    {
                        lblStatus.Text = "Posted";
                        lblNote.Text = "The goods receipt was posted and inventory was updated by the receiving service.";
                        lblReceiptDate.Text = DateTime.Now.ToString("g");
                    }
                }
                await Task.CompletedTask;
            }
            catch (Exception exception)
            {
                XtraMessageBox.Show(exception is System.ComponentModel.DataAnnotations.ValidationException || exception is UnauthorizedAccessException
                    ? exception.Message : "The receiving form could not be opened. Please try again.",
                    "Goods Receiving", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                _opening = false;
                if (!IsDisposed) { btnNewReceipt.Enabled = true; btnReceiveGoods.Enabled = true; }
            }
        }

        private void ClearSummary()
        {
            gridReceivingItems.DataSource = null;
            lblStatus.Text = "Ready";
            lblNote.Text = "Start a new purchase-order receipt. Posting rules and stock updates are enforced by the backend service.";
            lblTotalItems.Text = "0";
            lblTotalAmount.Text = "PHP 0.00";
        }

        private void panelControl6_Paint(object sender, PaintEventArgs e)
        {

        }
    }
}
