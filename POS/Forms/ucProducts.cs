using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using POS.Services;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using static POS.Services.InventoryService;
using POS.Core.Abstractions;
using POS.Core.Security;
using POS.Common.Enumerations;
using POS.Models.Operations;

namespace POS.Forms
{
    public partial class ucProducts : DevExpress.XtraEditors.XtraUserControl
    {
        private InventoryService _inventoryService;
        private readonly Func<frmAddProducts> _createAddProductForm;
        private readonly Func<int, string, frmProductPriceHistory> _createPriceHistoryForm;
        private readonly IAuthorizationService _authorization;
        private const int PageSize = 200;
        private int _pageNumber = 1;
        private int _totalPages = 1;
        private readonly SimpleButton _priceHistoryButton = new SimpleButton { Name = "btnPriceHistory", Text = "Price History" };
        private readonly SimpleButton _previousPageButton = new SimpleButton { Name = "btnPreviousPage", Text = "Previous" };
        private readonly SimpleButton _nextPageButton = new SimpleButton { Name = "btnNextPage", Text = "Next" };
        private readonly LabelControl _pageLabel = new LabelControl { Name = "lblProductPage", Appearance = { ForeColor = Color.White } };

        public ucProducts() : this(new InventoryService(), () => new frmAddProducts()) { }
        public ucProducts(InventoryService inventoryService, Func<frmAddProducts> createAddProductForm) : this(inventoryService, createAddProductForm, null, null) { }
        public ucProducts(InventoryService inventoryService, Func<frmAddProducts> createAddProductForm, Func<int, string, frmProductPriceHistory> createPriceHistoryForm, IAuthorizationService authorization)
        {
            InitializeComponent(); _inventoryService = inventoryService ?? throw new ArgumentNullException(nameof(inventoryService)); _createAddProductForm = createAddProductForm ?? throw new ArgumentNullException(nameof(createAddProductForm)); _createPriceHistoryForm = createPriceHistoryForm; _authorization = authorization;
            Id.FieldName = nameof(ProductSummaryDTO.Id); Barcode.FieldName = nameof(ProductSummaryDTO.Barcode); Price.FieldName = nameof(ProductSummaryDTO.SellingPrice); CostPrice.FieldName = nameof(ProductSummaryDTO.CostPrice); Status.FieldName = nameof(ProductSummaryDTO.IsActive); Status.Caption = "Active"; gcProduct.OptionsBehavior.Editable = false;
            btnAdd.Enabled = AuthorizationService.HasPermission(ResourceCodes.Products, ClaimActionType.Add); gcProduct.Columns.AddVisible(nameof(ProductSummaryDTO.BuyingThreshold), "Low-stock threshold"); gcProduct.Columns.AddVisible(nameof(ProductSummaryDTO.Sku), "SKU"); gcProduct.Columns.AddVisible(nameof(ProductSummaryDTO.Unit), "Unit");
            gcProduct.CustomColumnDisplayText += (sender, args) => { if (args.Column == Quantity && args.Value == null) args.DisplayText = "Missing"; }; ConfigurePaging(); ConfigurePriceHistory();
        }
        private void ConfigurePaging() { _previousPageButton.SetBounds(125, 12, 72, 30); _nextPageButton.SetBounds(203, 12, 56, 30); _pageLabel.Location = new Point(270, 19); panelControl1.Controls.AddRange(new Control[] { _previousPageButton, _nextPageButton, _pageLabel }); _previousPageButton.Click += (s, e) => { if (_pageNumber > 1) { _pageNumber--; LoadData(); } }; _nextPageButton.Click += (s, e) => { if (_pageNumber < _totalPages) { _pageNumber++; LoadData(); } }; UpdatePagingControls(0); }
        private void UpdatePagingControls(int totalCount) { _previousPageButton.Enabled = _pageNumber > 1; _nextPageButton.Enabled = _pageNumber < _totalPages; _pageLabel.Text = string.Format("Page {0} of {1} ({2:N0} products)", _pageNumber, _totalPages, totalCount); }
        private void ConfigurePriceHistory() { _priceHistoryButton.Anchor = AnchorStyles.Top | AnchorStyles.Right; _priceHistoryButton.SetBounds(btnAdd.Left - 112, btnAdd.Top, 104, btnAdd.Height); panelControl1.Controls.Add(_priceHistoryButton); _priceHistoryButton.BringToFront(); var permitted = _authorization != null && _authorization.HasPermission(ResourceCodes.Products, ClaimActionType.View) && _authorization.HasPermission(ResourceCodes.Audit, ClaimActionType.View); _priceHistoryButton.Enabled = permitted && _createPriceHistoryForm != null; _priceHistoryButton.ToolTip = permitted ? "View immutable price changes" : "Products View and Audit View permissions are required."; _priceHistoryButton.Click += (s, e) => ShowPriceHistory(); gcProduct.DoubleClick += (s, e) => { if (_priceHistoryButton.Enabled) ShowPriceHistory(); }; }
        private void ShowPriceHistory() { var selected = gcProduct.GetFocusedRow() as ProductSummaryDTO; if (selected == null) { XtraMessageBox.Show(this, "Select a product first.", "Price History"); return; } using (var form = _createPriceHistoryForm(selected.Id, selected.Name)) form.ShowDialog(FindForm()); }
        public void LoadData() { try { UseWaitCursor = true; var page = _inventoryService.SearchProducts(new ProductSearchDTO { PageNumber = _pageNumber, PageSize = PageSize }); _pageNumber = page.PageNumber; _totalPages = page.TotalPages; gcProducts.DataSource = new BindingList<ProductSummaryDTO>(page.Items.ToList()); UpdatePagingControls(page.TotalCount); } catch (Exception exception) { gcProducts.DataSource = null; _totalPages = 1; _pageNumber = 1; UpdatePagingControls(0); XtraMessageBox.Show(this, "Products could not be loaded.\n\n" + exception.Message, "Products", MessageBoxButtons.OK, MessageBoxIcon.Error); } finally { UseWaitCursor = false; } }
        private void gcProducts_Click(object sender, EventArgs e) { }
        private void panelControl1_Paint(object sender, PaintEventArgs e) { }
        private void ucProducts_Load_1(object sender, EventArgs e) { var rule = new GridFormatRule(); rule.Column = gcProduct.Columns["Quantity"]; rule.ApplyToRow = true; var condition = new FormatConditionRuleExpression { Expression = "[IsActive] = True And Not IsNull([Quantity]) And [Quantity] <= [BuyingThreshold]" }; condition.Appearance.BackColor = Color.Tomato; condition.Appearance.ForeColor = Color.White; rule.Rule = condition; gcProduct.FormatRules.Add(rule); LoadData(); }
        public void ShowStockAlert() { try { var lowStockProducts = _inventoryService.LowStockProducts(); if (lowStockProducts.Any()) XtraMessageBox.Show("The following products are low in stock:\n" + string.Join("\n", lowStockProducts.Select(p => p.Name)), "Low Stock Alert", MessageBoxButtons.OK, MessageBoxIcon.Warning); } catch (Exception exception) { XtraMessageBox.Show(this, exception.Message, "Unable to load stock alerts", MessageBoxButtons.OK, MessageBoxIcon.Warning); } }
        private void btnAdd_Click(object sender, EventArgs e) { using (var frmAddProduct = _createAddProductForm()) frmAddProduct.ShowDialog(this); LoadData(); }
    }
}
