using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using POS.Core.Abstractions;
using POS.Data.Context;
using POS.Models.Operations;
using POS.Services;
using POS.Services.Inventory;
using System;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;

namespace POS.Forms
{
    public sealed class ucInventory : XtraUserControl
    {
        private readonly InventoryLedgerService _ledger; private readonly InventoryService _products; private readonly TabControl _tabs = new TabControl();
        public ucInventory(InventoryLedgerService ledger, InventoryService products) { _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger)); _products = products ?? throw new ArgumentNullException(nameof(products)); Dock = DockStyle.Fill; BuildUi(); }
        private void BuildUi() { _tabs.Dock = DockStyle.Fill; AddPage("Balances", LoadBalances); AddPage("Alerts", LoadAlerts); AddPage("Movements", LoadMovements); AddPage("Reconciliation", LoadReconciliation); Controls.Add(_tabs); _tabs.SelectedIndexChanged += (s, e) => LoadCurrent(); Load += (s, e) => LoadCurrent(); }
        private void AddPage(string title, Action<GridControl> loader) { var page = new TabPage(title); var grid = new GridControl { Dock = DockStyle.Fill, Tag = loader, MainView = new GridView() }; grid.ViewCollection.Add(grid.MainView); ((GridView)grid.MainView).OptionsBehavior.Editable = false; ((GridView)grid.MainView).OptionsView.ShowGroupPanel = false; page.Controls.Add(grid); _tabs.TabPages.Add(page); }
        private GridControl Current() { return (GridControl)_tabs.SelectedTab.Controls[0]; } private void LoadCurrent() { try { ((Action<GridControl>)Current().Tag)(Current()); } catch (Exception ex) { XtraMessageBox.Show(this, ex.Message, "Inventory", MessageBoxButtons.OK, MessageBoxIcon.Warning); } }
        private static void Bind(GridControl grid, object data) { grid.DataSource = data; var view = (GridView)grid.MainView; view.PopulateColumns(); }
        private void LoadBalances(GridControl g) { var x = _ledger.SearchBalances(new InventoryBalanceSearchDTO { PageNumber = 1, PageSize = 100 }); Bind(g, new BindingList<InventoryBalanceItemDTO>(x.Items.ToList())); }
        private void LoadAlerts(GridControl g) { var x = _ledger.SearchAlerts(new InventoryAlertSearchDTO { Kind = InventoryAlertKind.LowStock, PageNumber = 1, PageSize = 100 }); Bind(g, new BindingList<InventoryAlertItemDTO>(x.Items.ToList())); }
        private void LoadMovements(GridControl g) { var x = _ledger.SearchMovements(new StockMovementSearchDTO { PageNumber = 1, PageSize = 100 }); Bind(g, new BindingList<StockMovementDTO>(x.Items.ToList())); }
        private void LoadReconciliation(GridControl g) { var x = _ledger.GetReconciliation(new InventoryReconciliationSearchDTO { MismatchesOnly = true, PageNumber = 1, PageSize = 100 }); Bind(g, new BindingList<InventoryReconciliationItemDTO>(x.Items.ToList())); }
    }
}
