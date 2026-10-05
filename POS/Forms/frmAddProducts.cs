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
            btnSave.Enabled = AuthorizationService.HasPermission(POS.Core.Security.ResourceCodes.Products,
                POS.Common.Enumerations.ClaimActionType.Add);
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
    }
}
