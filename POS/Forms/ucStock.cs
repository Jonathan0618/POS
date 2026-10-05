using POS.Services;
using System.ComponentModel;
using System;
using System.Linq;
using static POS.Services.InventoryService;

namespace POS.Forms
{
    public partial class ucStock : DevExpress.XtraEditors.XtraUserControl
    {
        private InventoryService _inventoryService;
        public ucStock() : this(new InventoryService())
        {
        }

        public ucStock(InventoryService inventoryService)
        {
            InitializeComponent();
            _inventoryService = inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));
        }

        public void LoadData()
        {
            try
            {
            var stocks = _inventoryService.GetAllStocks();
            gcStocks.DataSource = new BindingList<StockViewModel>(stocks.ToList());
            }
            catch (Exception exception)
            {
                gcStocks.DataSource = null;
                DevExpress.XtraEditors.XtraMessageBox.Show(this, exception.Message, "Unable to load stock",
                    System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
            }
        }

        private void ucStock_Load(object sender, System.EventArgs e)
        {
            LoadData();
        }
    }
}
