using DevExpress.XtraEditors;
using POS.Models.Operations;
using POS.Services.Point_of_Sale;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace POS.Forms
{
    public sealed class frmCheckoutComplete : XtraForm
    {
        private readonly SaleReceiptService _receiptService;
        private readonly SaleReceiptDTO _receipt;
        private readonly SimpleButton _print = new SimpleButton { Name = "btnPrintReceipt", Text = "Print receipt" };
        private readonly SimpleButton _newSale = new SimpleButton { Name = "btnNewSale", Text = "New sale" };
        private readonly LabelControl _printStatus = new LabelControl();
        private bool _busy;

        public frmCheckoutComplete() : this(null, null) { }

        public frmCheckoutComplete(SaleReceiptService receiptService, SaleReceiptDTO receipt)
        {
            _receiptService = receiptService;
            _receipt = receipt;
            Text = "Checkout Complete";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new System.Drawing.Size(560, 380);
            BuildUi();
        }

        public bool SaleConfirmed { get { return _receipt != null; } }

        private void BuildUi()
        {
            var title = new LabelControl { Text = "SALE COMPLETE", AutoSizeMode = LabelAutoSizeMode.None };
            title.Appearance.Font = new System.Drawing.Font("Segoe UI", 22F, System.Drawing.FontStyle.Bold);
            title.Appearance.ForeColor = System.Drawing.Color.SeaGreen;
            title.Appearance.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            title.SetBounds(20, 24, 520, 46);
            var receipt = new LabelControl { Text = "Receipt: " + (_receipt == null ? "" : _receipt.ReceiptNumber) };
            receipt.Appearance.Font = new System.Drawing.Font("Segoe UI", 13F);
            receipt.SetBounds(40, 94, 480, 28);
            var total = new LabelControl { Text = "Total: " + Money(_receipt == null ? 0 : _receipt.TotalAmount) };
            total.Appearance.Font = new System.Drawing.Font("Segoe UI", 18F, System.Drawing.FontStyle.Bold);
            total.SetBounds(40, 132, 480, 36);
            var change = new LabelControl { Text = "Change: " + Money(_receipt == null ? 0 : _receipt.Change) };
            change.SetBounds(40, 177, 480, 26);
            var tenders = new MemoEdit { ReadOnly = true };
            tenders.SetBounds(40, 212, 480, 70);
            tenders.Text = _receipt == null ? string.Empty : string.Join(Environment.NewLine,
                _receipt.Payments.Select(x => x.TenderType + ": " + Money(x.Amount)));
            _print.SetBounds(40, 310, 150, 38);
            _newSale.SetBounds(370, 310, 150, 38);
            _printStatus.SetBounds(205, 320, 150, 24);
            Controls.AddRange(new Control[] { title, receipt, total, change, tenders, _print, _newSale, _printStatus });
            _print.Click += async (sender, args) => await PrintAsync();
            _newSale.Click += (sender, args) => { DialogResult = DialogResult.OK; Close(); };
            AcceptButton = _newSale;
        }

        private string Money(decimal amount)
        {
            var places = _receipt == null ? 2 : Math.Max(0, Math.Min(4, _receipt.MoneyDecimalPlaces));
            return string.Format("{0} {1:N" + places + "}", _receipt == null ? "PHP" : _receipt.CurrencyCode, amount);
        }

        private async Task PrintAsync()
        {
            if (_busy || _receiptService == null || _receipt == null) return;
            try
            {
                SetBusy(true, "Printing...");
                await Task.Run(() => _receiptService.Print(_receipt.SaleId, false));
                _printStatus.Text = "Printed";
            }
            catch (Exception exception)
            {
                _printStatus.Text = "Sale remains complete";
                XtraMessageBox.Show("The sale is complete, but the receipt could not be printed.\n\n" +
                    SafeMessage(exception), "Print Receipt", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally { SetBusy(false, _printStatus.Text); }
        }

        private void SetBusy(bool busy, string status)
        {
            _busy = busy;
            _print.Enabled = !busy;
            _newSale.Enabled = !busy;
            _printStatus.Text = status;
            UseWaitCursor = busy;
        }

        private static string SafeMessage(Exception exception)
        {
            return exception is InvalidOperationException || exception is UnauthorizedAccessException
                ? exception.Message : "Check the configured printer and try again.";
        }
    }
}
