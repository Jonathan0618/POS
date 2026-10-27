using DevExpress.XtraEditors;
using POS.Services.Purchasing;
using POS.Models.Operations;
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
    public partial class ucPurchaseOrders : DevExpress.XtraEditors.XtraUserControl
    {
        private PurchaseOrderService _purchaseOrderService;
        public ucPurchaseOrders(PurchaseOrderService purchaseOrderService)
        {
            _purchaseOrderService = purchaseOrderService;
            InitializeComponent();

            cboStatus.Properties.Items.Clear();
            cboStatus.Properties.Items.Add("All Statuses");
            cboStatus.Properties.Items.Add("Draft");
            cboStatus.Properties.Items.Add("Ordered");
            cboStatus.Properties.Items.Add("Partially Received");
            cboStatus.Properties.Items.Add("Cancelled");

            cboStatus.SelectedIndex = 0;
        }

        private void cboStatus_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void slueSupplier_EditValueChanged(object sender, EventArgs e)
        {

        }

        private void slueProduct_EditValueChanged(object sender, EventArgs e)
        {

        }

        private void gridPurchaseOrders_Click(object sender, EventArgs e)
        {

        }
    }
}
