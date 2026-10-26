using DevExpress.XtraEditors;
using POS.Core.Abstractions;
using POS.Core.Security;
using POS.Data.Context;
using POS.Models.Operations;
using POS.Services;
using POS.Services.Inventory;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace POS.Forms
{
    public sealed class frmStockAdjustment : XtraForm
    {
        private readonly InventoryService _products; private readonly InventoryLedgerService _ledger; private readonly LookUpEdit _product = new LookUpEdit(); private readonly SpinEdit _expected = new SpinEdit(), _delta = new SpinEdit(); private readonly MemoEdit _reason = new MemoEdit(); private readonly SimpleButton _post = new SimpleButton { Text = "Post adjustment" };
        public frmStockAdjustment(InventoryService products, InventoryLedgerService ledger) { _products = products ?? throw new ArgumentNullException(nameof(products)); _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger)); Text = "Stock adjustment"; Size = new Size(520, 360); StartPosition = FormStartPosition.CenterParent; BuildUi(); LoadProducts(); }
        private void BuildUi() { var p = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 2, RowCount = 5 }; p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150)); p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); Add(p, "Product", _product, 0); Add(p, "Expected quantity", _expected, 1); Add(p, "Signed quantity delta", _delta, 2); Add(p, "Required reason", _reason, 3); _reason.Height = 55; _reason.Properties.AutoHeight = false; p.Controls.Add(_post, 1, 4); Controls.Add(p); _product.Properties.DisplayMember = "Name"; _product.Properties.ValueMember = "Id"; _product.Properties.NullText = "Select product"; _expected.Properties.ReadOnly = true; _post.Click += Post; _product.EditValueChanged += (s, e) => LoadExpected(); }
        private static void Add(TableLayoutPanel p, string label, Control c, int row) { p.RowStyles.Add(new RowStyle(SizeType.Absolute, 48)); p.Controls.Add(new LabelControl { Text = label, Dock = DockStyle.Fill, Padding = new Padding(0, 10, 0, 0) }, 0, row); c.Dock = DockStyle.Fill; p.Controls.Add(c, 1, row); }
        private void LoadProducts() { try { _product.Properties.DataSource = _products.SearchProducts(new ProductSearchDTO { IsActive = true, PageSize = 200 }).Items; } catch (Exception ex) { XtraMessageBox.Show(this, ex.Message, "Inventory", MessageBoxButtons.OK, MessageBoxIcon.Warning); } }
        private void LoadExpected() { try { if (_product.EditValue != null) _expected.Value = _ledger.GetQuantityOnHand(Convert.ToInt32(_product.EditValue)); } catch (Exception ex) { _expected.Value = 0; XtraMessageBox.Show(this, ex.Message, "Inventory", MessageBoxButtons.OK, MessageBoxIcon.Warning); } }
        private void Post(object sender, EventArgs e) { try { if (_product.EditValue == null || _delta.Value == 0 || string.IsNullOrWhiteSpace(_reason.Text)) throw new System.ComponentModel.DataAnnotations.ValidationException("Select a product, enter a non-zero delta, and provide a reason."); _post.Enabled = false; _ledger.PostAdjustment(new StockAdjustmentDTO { RequestId = Guid.NewGuid(), ProductId = Convert.ToInt32(_product.EditValue), ExpectedQuantityOnHand = Convert.ToInt32(_expected.Value), QuantityDelta = Convert.ToInt32(_delta.Value), Reason = _reason.Text }); DialogResult = DialogResult.OK; Close(); } catch (Exception ex) { _post.Enabled = true; XtraMessageBox.Show(this, ex.Message, "Unable to post adjustment", MessageBoxButtons.OK, MessageBoxIcon.Warning); } }
    }
}
