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

namespace POS.Inventory
{
    public partial class AddProducts : DevExpress.XtraEditors.XtraForm
    {
        private readonly ControlMapper<InventoryDTO> _controlMapper;
        private readonly ModelValidator<InventoryDTO> _validator;
        private readonly InventoryService _inventoryService;

        public AddProducts()
        {
            InitializeComponent();
            _controlMapper = new ControlMapper<InventoryDTO>();
            _validator = new ModelValidator<InventoryDTO>();
            _inventoryService = new InventoryService();
        }

        private void AddProducts_Load(object sender, EventArgs e)
        {
            var categories = _inventoryService.GetAllCategories();
            lueCategoryId.Properties.DataSource = categories;
            lueCategoryId.Properties.DisplayMember = "Name";
            lueCategoryId.Properties.ValueMember = "Id";
        }

        private void labelControl5_Click(object sender, EventArgs e)
        {

        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            var product = new InventoryDTO();
            _controlMapper.MapToEntity(product, this);
            var validationResults = _validator.Validate(product, dxErrorProvider1, this);

            if(!validationResults)
            {
                _inventoryService.AddProduct(product);
                XtraMessageBox.Show("Product added successfully.", "Success",
                MessageBoxButtons.OK, 
                MessageBoxIcon.Information);
                
            }

        }
    }
}