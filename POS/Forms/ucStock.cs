using POS.Services;
using System.ComponentModel;
using System.Linq;
using static POS.Services.InventoryService;

namespace POS.Forms
{
    public partial class ucStock : DevExpress.XtraEditors.XtraUserControl
    {
        private InventoryService _inventoryService;
        public ucStock()
        {
            InitializeComponent();
            _inventoryService = new InventoryService();
        }

        public void LoadData()
        {
            var stocks = _inventoryService.GetAllStocks();
            gcStocks.DataSource = new BindingList<StockViewModel>(stocks.ToList());
        }

        private void ucStock_Load(object sender, System.EventArgs e)
        {
            LoadData();
        }
    }
}
