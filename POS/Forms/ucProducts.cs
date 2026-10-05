using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using POS.Services;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using static POS.Services.InventoryService;

namespace POS.Forms
{
    public partial class ucProducts : DevExpress.XtraEditors.XtraUserControl
    {

        private InventoryService _inventoryService;
        private readonly Func<frmAddProducts> _createAddProductForm;

        public ucProducts() : this(new InventoryService(), () => new frmAddProducts())
        {
        }

        public ucProducts(InventoryService inventoryService, Func<frmAddProducts> createAddProductForm)
        {
            InitializeComponent();
            _inventoryService = inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));
            _createAddProductForm = createAddProductForm ?? throw new ArgumentNullException(nameof(createAddProductForm));
            Id.FieldName = nameof(ProductViewModel.Id);
            Barcode.FieldName = nameof(ProductViewModel.Barcode);
            CostPrice.FieldName = nameof(ProductViewModel.CostPrice);
            Status.FieldName = nameof(ProductViewModel.IsActive);
            Status.Caption = "Active";
            gcProduct.OptionsBehavior.Editable = false;
            btnAdd.Enabled = AuthorizationService.HasPermission(POS.Core.Security.ResourceCodes.Products,
                POS.Common.Enumerations.ClaimActionType.Add);
            gcProduct.Columns.AddField(nameof(ProductViewModel.BuyingThreshold));
            gcProduct.Columns.AddVisible(nameof(ProductViewModel.Sku), "SKU");
            gcProduct.Columns.AddVisible(nameof(ProductViewModel.Unit), "Unit");
        }

        public void LoadData()
        {
            try
            {
                UseWaitCursor = true;
                var products = _inventoryService.GetAllProducts();
                gcProducts.DataSource = new BindingList<ProductViewModel>(products.ToList());
            }
            catch (Exception exception)
            {
                gcProducts.DataSource = null;
                XtraMessageBox.Show(this, "Products could not be loaded.\n\n" + exception.Message,
                    "Products", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { UseWaitCursor = false; }
        }


        private void gcProducts_Click(object sender, EventArgs e)
        {

        }

        private void panelControl1_Paint(object sender, PaintEventArgs e)
        {

        }
        
        private void ucProducts_Load_1(object sender, EventArgs e)
        {
            var rule = new GridFormatRule();
            rule.Column = gcProduct.Columns["Quantity"];
            rule.ApplyToRow = true;

            var condition = new FormatConditionRuleExpression
            {
                Expression = "[Quantity] <= [BuyingThreshold]"
            };
            condition.Appearance.BackColor = Color.Tomato;
            condition.Appearance.ForeColor = Color.White;
            rule.Rule = condition;

            gcProduct.FormatRules.Add(rule);

            LoadData();
        }

        public void ShowStockAlert()
        {
            try
            {
            var lowStockProducts = _inventoryService.LowStockProducts();

            if (lowStockProducts.Any())
            {
                XtraMessageBox.Show(
                    "The following products are low in stock:\n" +
                    string.Join("\n", lowStockProducts.Select(p => p.Name)),
                    "Low Stock Alert",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            }
            catch (Exception exception)
            {
                XtraMessageBox.Show(this, exception.Message, "Unable to load stock alerts",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            using (var frmAddProduct = _createAddProductForm())
                frmAddProduct.ShowDialog(this);
            LoadData();
        }
    }
}
