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
    public partial class ucPurchaseOrders : DevExpress.XtraEditors.XtraUserControl
    {
        public ucPurchaseOrders()
        {
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
    }
}
