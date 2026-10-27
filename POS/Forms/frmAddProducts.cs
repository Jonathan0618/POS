using DevExpress.Office.Design.Internal;
using DevExpress.XtraEditors;
using POS.Models.Security;
using POS.Services;
using POS.Utility;
using POS.Validators;
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
    public partial class frmAddProducts : AuthorizedForm
    {
        private readonly ControlMapper<InventoryDTO> _ControlMapper;
        private readonly ModelValidator<InventoryDTO> _validator;
        private readonly InventoryService _inventoryService;
        public frmAddProducts() : this(new InventoryService())
        {
        }

        public frmAddProducts(InventoryService inventoryService)
        {
            _ControlMapper = new ControlMapper<InventoryDTO>();
            _validator = new ModelValidator<InventoryDTO>();
            _inventoryService = inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));
            InitializeComponent();
            ConfigureUnitLookup();
            ConfigureResponsiveLayout();
            btnSave.Enabled = AuthorizationService.HasPermission(POS.Core.Security.ResourceCodes.Products,
                POS.Common.Enumerations.ClaimActionType.Add);
        }

        private void ConfigureResponsiveLayout()
        {
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            MinimumSize = Size;
            panelControl1.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtProductName.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtBarcode.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            lueCategoryId.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            lueUnit.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtSellingPrice.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtCostPrice.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtDescription.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            spnBuyingThreshold.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            btnSave.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        }

        private void ConfigureUnitLookup()
        {
            var units = new[]
            {
                new UnitOption("piece", "Piece"),
                new UnitOption("bottle", "Bottle"),
                new UnitOption("can", "Can"),
                new UnitOption("bag", "Bag"),
                new UnitOption("box", "Box"),
                new UnitOption("pack", "Pack"),
                new UnitOption("carton", "Carton"),
                new UnitOption("jar", "Jar"),
                new UnitOption("kg", "Kilogram (kg)"),
                new UnitOption("g", "Gram (g)"),
                new UnitOption("litre", "Litre (L)"),
                new UnitOption("ml", "Millilitre (ml)")
            };

            lueUnit.Properties.DataSource = units;
            lueUnit.Properties.DisplayMember = nameof(UnitOption.DisplayName);
            lueUnit.Properties.ValueMember = nameof(UnitOption.Value);
            lueUnit.Properties.NullText = "Select a unit";
            lueUnit.Properties.ShowHeader = false;
        }
        private void frmAddProducts_Load(object sender, EventArgs e)
        {
            try
            {
            var products = _inventoryService.GetAllCategories();
            lueCategoryId.Properties.DataSource = products;
            }
            catch (Exception exception)
            {
                btnSave.Enabled = false;
                XtraMessageBox.Show(this, exception.Message, "Unable to load categories",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            try
            {
            var products = new InventoryDTO();
            _ControlMapper.MapToEntity(products, this);
            products.BuyingThreshold = Convert.ToInt32(spnBuyingThreshold.Text);

            var ValidateResult = _validator.Validate(products, dxErrorProvider1, this);

            if (ValidateResult)
            {
                _inventoryService.AddProduct(products);
                XtraMessageBox.Show(
                "Product added successfully.",
                "Success",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
                DialogResult = DialogResult.OK;
                Close();
            }
            }
            catch (Exception exception)
            {
                XtraMessageBox.Show(this, exception.Message, "Unable to add product",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void lookUpEdit1_EditValueChanged(object sender, EventArgs e)
        {

        }

        private void labelControl9_Click(object sender, EventArgs e)
        {

        }

        private sealed class UnitOption
        {
            public UnitOption(string value, string displayName)
            {
                Value = value;
                DisplayName = displayName;
            }

            public string Value { get; private set; }
            public string DisplayName { get; private set; }
        }
    }
}
