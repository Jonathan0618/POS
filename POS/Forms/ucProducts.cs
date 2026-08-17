using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using POS.Services;
using System;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;

namespace POS.Forms
{
    public partial class ucProducts : DevExpress.XtraEditors.XtraUserControl
    {

        private InventoryService _inventoryService;
        public ucProducts()
        {
            InitializeComponent();
            _inventoryService = new InventoryService();
        }

        public void LoadData()
        {
            var products = _inventoryService.GetAllProducts();
            gcProducts.DataSource = new BindingList<ProductViewModel>(products.ToList());
        }


        private void gcProducts_Click(object sender, EventArgs e)
        {

        }

        private void panelControl1_Paint(object sender, PaintEventArgs e)
        {

        }
        
        private void ucProducts_Load_1(object sender, EventArgs e)
        {

        }

        public void ShowStockAlert()
        {

            var lowStockProducts = _inventoryService.GetLowStockProducts(10);

            var rule = new GridFormatRule();
            rule.Column = gcProduct.Columns["Quantity"];
            if (lowStockProducts.Any())
            {
                MessageBox.Show("The following products are low in stock:\n"
                    + string.Join("\n", lowStockProducts.Select(p => p.Name)), "Low Stock Alert",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            var condition = new FormatConditionRuleValue
            {
                Condition = FormatCondition.Less,
                Value1 = 10,

            };
            rule.Rule = condition;
            gcProduct.FormatRules.Add(rule);

            LoadData();
        }
    }
}
