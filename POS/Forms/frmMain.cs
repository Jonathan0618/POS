using DevExpress.XtraBars;
using POS.Reports;
using POS.Services;
using POS.Services.Security;
using System;
using System.ComponentModel;
using System.Linq;

namespace POS.Forms
{
    public partial class frmMain : DevExpress.XtraBars.Ribbon.RibbonForm
    {
        private InventoryService _inventoryService;
        public frmMain()
        {
            InitializeComponent();
            _inventoryService = new InventoryService();
        }

        private void frmMain_Load(object sender, EventArgs e)
        {

        }

        private void barButtonItem13_ItemClick(object sender, ItemClickEventArgs e)
        {
            var frmCategory = new frmCategory();
            frmCategory.ShowDialog();
        }

        private void barButtonItem12_ItemClick(object sender, ItemClickEventArgs e)
        {
            pnlMain.Controls.Clear();

            var uc = new ucProducts();
            uc.Dock = System.Windows.Forms.DockStyle.Fill;
            pnlMain.Controls.Add(uc);
            uc.ShowStockAlert();
        }

        private void barButtonItem19_ItemClick(object sender, ItemClickEventArgs e)
        {

            pnlMain.Controls.Clear();

            var uc = new ucProducts();
            uc.Dock = System.Windows.Forms.DockStyle.Fill;
            pnlMain.Controls.Add(uc);
            uc.ShowStockAlert();
        }

        private void barButtonItem4_ItemClick_1(object sender, ItemClickEventArgs e)
        {
            var rpt = new rptReceipt();
            var frm = new frmReportViewer(rpt);
            frm.ShowDialog();
        }

        private void barButtonItem22_ItemClick(object sender, ItemClickEventArgs e)
        {
            var frmCategory = new frmCategory();
            frmCategory.ShowDialog();
        }

        private void barButtonItem12_ItemClick_1(object sender, ItemClickEventArgs e)
        {

            pnlMain.Controls.Clear();

            var uc = new ucProducts();
            uc.Dock = System.Windows.Forms.DockStyle.Fill;
            pnlMain.Controls.Add(uc);
            uc.ShowStockAlert();
        }

        private void barButtonItem13_ItemClick_1(object sender, ItemClickEventArgs e)
        {
            var frmCategory = new frmCategory();
            frmCategory.ShowDialog();
        }
    }
}