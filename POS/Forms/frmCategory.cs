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
using static POS.Services.InventoryService;

namespace POS.Forms
{
    public partial class frmCategory : DevExpress.XtraEditors.XtraForm
    {
        private readonly InventoryService _inventoryService;
        public frmCategory() : this(new InventoryService())
        {
        }

        public frmCategory(InventoryService inventoryService)
        {
            InitializeComponent();
            var canAdd = AuthorizationService.HasPermission(POS.Core.Security.ResourceCodes.Products,
                POS.Common.Enumerations.ClaimActionType.Add);
            var canEdit = AuthorizationService.HasPermission(POS.Core.Security.ResourceCodes.Products,
                POS.Common.Enumerations.ClaimActionType.Edit);
            btnDelete.Enabled = AuthorizationService.HasPermission(POS.Core.Security.ResourceCodes.Products,
                POS.Common.Enumerations.ClaimActionType.Delete);
            gridCategory.OptionsView.NewItemRowPosition = canAdd
                ? DevExpress.XtraGrid.Views.Grid.NewItemRowPosition.Top
                : DevExpress.XtraGrid.Views.Grid.NewItemRowPosition.None;
            gridCategory.OptionsBehavior.Editable = canAdd || canEdit;
            gridCategory.ShowingEditor += (sender, args) =>
                args.Cancel = gridCategory.IsNewItemRow(gridCategory.FocusedRowHandle) ? !canAdd : !canEdit;
            _inventoryService = inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));
        }

        private void frmCategory_Load(object sender, EventArgs e)
        {
            LoadData();
        }

        private void LoadData()
        {
            try
            {
            var categories = _inventoryService.GetAllCategories();
            gcCategory.DataSource = new BindingList<CategoryViewModel>(categories.ToList());
            }
            catch (Exception exception)
            {
                gcCategory.DataSource = null;
                XtraMessageBox.Show(this, exception.Message, "Unable to load categories",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void gridCategory_RowUpdated(object sender, DevExpress.XtraGrid.Views.Base.RowObjectEventArgs e)
        {
            try
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
            }
            catch (Exception exception)
            {
                XtraMessageBox.Show(this, exception.Message, "Unable to save category",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            LoadData();
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            try
            {
            if(XtraMessageBox.Show(this, "Are you sure you want to delete this category?", "Confirm Delete", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                var selectedCategory = gridCategory.GetFocusedRow() as CategoryViewModel;
                if (selectedCategory != null)
                {
                    _inventoryService.DeleteCategory(selectedCategory);
                }
            }
            }
            catch (Exception exception)
            {
                XtraMessageBox.Show(this, exception.Message, "Unable to delete category",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            LoadData();
        }

        private void gcCategory_Click(object sender, EventArgs e)
        {

        }
    }
}
