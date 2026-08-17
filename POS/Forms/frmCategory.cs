using DevExpress.XtraEditors;
using POS.Services;
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
    public partial class frmCategory : DevExpress.XtraEditors.XtraForm
    {
        private readonly InventoryService _inventoryService;
        public frmCategory()
        {
            InitializeComponent();
            _inventoryService = new InventoryService();
        }

        private void frmCategory_Load(object sender, EventArgs e)
        {
            LoadData();
        }

        private void LoadData()
        {
            var categories = _inventoryService.GetAllCategories();
            gcCategory.DataSource = new BindingList<CategoryViewModel>(categories.ToList());
        }

        private void gridCategory_RowUpdated(object sender, DevExpress.XtraGrid.Views.Base.RowObjectEventArgs e)
        {
            var category = e.Row as CategoryViewModel;
            if (category != null)
            {
                if (category.Id == 0)
                {
                    _inventoryService.AddCategory(category);
                }
                else
                {
                    _inventoryService.UpdateCategory(category);
                }
            }
            LoadData();
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            if(MessageBox.Show("Are you sure you want to delete this category?", "Confirm Delete", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                var selectedCategory = gridCategory.GetFocusedRow() as CategoryViewModel;
                if (selectedCategory != null)
                {
                    _inventoryService.DeleteCategory(selectedCategory);
                }
            }
            LoadData();
        }

        private void gcCategory_Click(object sender, EventArgs e)
        {

        }
    }
}