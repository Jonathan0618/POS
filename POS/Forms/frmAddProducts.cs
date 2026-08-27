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
        public frmAddProducts()
        {
            _ControlMapper = new ControlMapper<InventoryDTO>();
            _validator = new ModelValidator<InventoryDTO>();
            _inventoryService = new InventoryService();
            InitializeComponent();
        }
        private void frmAddProducts_Load(object sender, EventArgs e)
        {

            var products = _inventoryService.GetAllCategories();
            lueCategoryId.Properties.DataSource = products;

        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            var products = new InventoryDTO();
            _ControlMapper.MapToEntity(products, this);
            var ValidateResult = _validator.Validate(products, dxErrorProvider1, this);

            if (ValidateResult)
            {
                _inventoryService.AddProduct(products);
                XtraMessageBox.Show(
                "Product added successfully.",
                "Success",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            }


        }

        private void lookUpEdit1_EditValueChanged(object sender, EventArgs e)
        {

        }
    }
}