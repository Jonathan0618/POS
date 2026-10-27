using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using POS.Core;
using POS.Models.Operations;
using POS.Services.Customers;
using POS.Services.Point_of_Sale;
using POS.Services.Shifts;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace POS.Forms
{
    public sealed class ucSalesRegister : XtraUserControl
    {
        private const int PageSize = 50;
        private readonly SalesCartService _cartService;
        private readonly SalesService _salesService;
        private readonly CashierShiftService _shiftService;
        private readonly CustomerService _customerService;
        private readonly SaleReceiptService _receiptService;
        private readonly Func<frmHeldSales> _createHeldSales;
        private readonly TextEdit _barcode = new TextEdit { Name = "txtBarcode" };
        private readonly TextEdit _search = new TextEdit { Name = "txtProductSearch" };
        private readonly SimpleButton _searchButton = new SimpleButton { Name = "btnSearchProducts", Text = "Search" };
        private readonly LookUpEdit _customer = new LookUpEdit { Name = "slueCustomer" };
        private readonly GridControl _products = new GridControl { Name = "gcSaleProducts" };
        private readonly GridView _productView = new GridView { Name = "gvSaleProducts" };
        private readonly GridControl _cartGrid = new GridControl { Name = "gcCart" };
        private readonly GridView _cartView = new GridView { Name = "gvCart" };
        private readonly SimpleButton _addProduct = new SimpleButton { Name = "btnAddProduct", Text = "ADD SELECTED" };
        private readonly SimpleButton _productPrevious = new SimpleButton { Text = "Previous" };
        private readonly SimpleButton _productNext = new SimpleButton { Text = "Next" };
        private readonly SimpleButton _remove = new SimpleButton { Text = "Remove selected" };
        private readonly SimpleButton _hold = new SimpleButton { Name = "btnHoldSale", Text = "Hold" };
        private readonly SimpleButton _resume = new SimpleButton { Name = "btnResumeHeldSale", Text = "Resume" };
        private readonly SimpleButton _clear = new SimpleButton { Name = "btnClearCart", Text = "Clear" };
        private readonly SimpleButton _payment = new SimpleButton { Name = "btnPayment", Text = "PAY" };
        private readonly LabelControl _status = new LabelControl();
        private readonly LabelControl _subtotal = new LabelControl { Name = "lblSubtotal" };
        private readonly LabelControl _tax = new LabelControl { Name = "lblTax" };
        private readonly LabelControl _discount = new LabelControl { Name = "lblDiscount" };
        private readonly LabelControl _total = new LabelControl { Name = "lblTotal" };
        private readonly LabelControl _registerContext = new LabelControl();
        private readonly LabelControl _shiftContext = new LabelControl();
        private readonly LabelControl _cashierContext = new LabelControl();
        private readonly LabelControl _itemCount = new LabelControl { Name = "lblItemCount" };
        private SaleCartDTO _cart;
        private CashierShiftDTO _shift;
        private HeldSaleDTO _heldSale;
        private SaleCartProductPageDTO _productPage;
        private int _productPageNumber = 1;
        private bool _busy;
        private bool _changingCustomer;

        public ucSalesRegister() { BuildUi(); }

        public ucSalesRegister(SalesCartService cartService, SalesService salesService,
            CashierShiftService shiftService, CustomerService customerService,
            SaleReceiptService receiptService, Func<frmHeldSales> createHeldSales) : this()
        {
            _cartService = cartService ?? throw new ArgumentNullException(nameof(cartService));
            _salesService = salesService ?? throw new ArgumentNullException(nameof(salesService));
            _shiftService = shiftService ?? throw new ArgumentNullException(nameof(shiftService));
            _customerService = customerService ?? throw new ArgumentNullException(nameof(customerService));
            _receiptService = receiptService ?? throw new ArgumentNullException(nameof(receiptService));
            _createHeldSales = createHeldSales ?? throw new ArgumentNullException(nameof(createHeldSales));
            Load += async (sender, args) => await InitializeAsync();
        }

        private void BuildUi()
        {
            Dock = DockStyle.Fill;
            BackColor = System.Drawing.Color.FromArgb(243, 246, 250);
            var header = BuildOperationalHeader();
            var workArea = new SplitContainer
            {
                Dock = DockStyle.Fill,
                IsSplitterFixed = true,
                SplitterDistance = 460
            };
            workArea.Resize += (sender, args) =>
            {
                var split = (SplitContainer)sender;
                if (split.Width > split.SplitterWidth)
                    split.SplitterDistance = (split.Width - split.SplitterWidth) / 2;
            };
            workArea.BackColor = System.Drawing.Color.FromArgb(243, 246, 250);
            workArea.Panel1.Padding = new Padding(10, 10, 5, 10);
            workArea.Panel2.Padding = new Padding(5, 10, 10, 10);
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.Controls.Add(header, 0, 0);
            root.Controls.Add(workArea, 0, 1);
            Controls.Add(root);
            BuildProducts(workArea.Panel1);
            BuildCart(workArea.Panel2);
            UpdateOperationalHeader();
            ApplyControlState();
        }

        private Control BuildOperationalHeader()
        {
            var header = new PanelControl { Dock = DockStyle.Fill, Padding = new Padding(14, 8, 14, 8) };
            header.Appearance.BackColor = System.Drawing.Color.FromArgb(31, 41, 55);
            header.Appearance.Options.UseBackColor = true;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 2 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 26));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 31));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
            var title = new LabelControl { Text = "SALES REGISTER", Dock = DockStyle.Fill };
            title.Appearance.Font = new System.Drawing.Font("Segoe UI", 17F, System.Drawing.FontStyle.Bold);
            title.Appearance.ForeColor = System.Drawing.Color.White;
            title.Appearance.Options.UseFont = true;
            title.Appearance.Options.UseForeColor = true;
            layout.Controls.Add(title, 0, 0);
            layout.Controls.Add(HeaderValue(_registerContext), 1, 0);
            layout.Controls.Add(HeaderValue(_cashierContext), 2, 0);
            layout.Controls.Add(HeaderValue(_itemCount), 3, 0);
            layout.Controls.Add(HeaderValue(_shiftContext), 0, 1);
            layout.SetColumnSpan(_shiftContext, 4);
            header.Controls.Add(layout);
            return header;
        }

        private static Control HeaderValue(LabelControl label)
        {
            label.Dock = DockStyle.Fill;
            label.Appearance.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            label.Appearance.ForeColor = System.Drawing.Color.White;
            label.Appearance.Options.UseFont = true;
            label.Appearance.Options.UseForeColor = true;
            return label;
        }

        private void BuildProducts(Control parent)
        {
            var sectionHeader = SectionHeader("PRODUCT CATALOG", "Scan a barcode or search the catalog");
            var filters = new TableLayoutPanel { Dock = DockStyle.Top, Height = 116, ColumnCount = 3, RowCount = 3, Padding = new Padding(10, 7, 10, 7) };
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            filters.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            filters.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            filters.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            var scanLabel = new LabelControl { Text = "SCAN BARCODE" };
            scanLabel.Appearance.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            filters.Controls.Add(scanLabel, 0, 0);
            filters.Controls.Add(new LabelControl { Text = "Customer (optional)" }, 2, 0);
            filters.Controls.Add(_barcode, 0, 1);
            filters.Controls.Add(_customer, 2, 1);
            filters.Controls.Add(_search, 0, 2);
            filters.Controls.Add(_searchButton, 1, 2);
            filters.SetColumnSpan(_barcode, 2);
            _barcode.Dock = DockStyle.Fill;
            _barcode.Properties.NullValuePrompt = "Scan or type barcode, then press Enter";
            _barcode.Properties.NullValuePromptShowForEmptyValue = true;
            _barcode.Properties.Appearance.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold);
            _search.Dock = DockStyle.Fill;
            _searchButton.Dock = DockStyle.Fill;
            _customer.Dock = DockStyle.Fill;
            _customer.Properties.DisplayMember = nameof(CustomerChoice.Display);
            _customer.Properties.ValueMember = nameof(CustomerChoice.Id);
            _customer.Properties.NullText = "Walk-in";

            _products.Dock = DockStyle.Fill;
            _products.MainView = _productView;
            _products.ViewCollection.Add(_productView);
            _productView.OptionsBehavior.Editable = false;
            _productView.OptionsView.ShowGroupPanel = false;
            StyleGrid(_productView);
            ProductColumn(nameof(SaleCartLineDTO.ProductName), "Product");
            ProductColumn(nameof(SaleCartLineDTO.Sku), "SKU");
            ProductColumn(nameof(SaleCartLineDTO.Barcode), "Barcode");
            ProductColumn(nameof(SaleCartLineDTO.QuantityAvailable), "Available");
            ProductColumn(nameof(SaleCartLineDTO.UnitPrice), "Unit price", "n2");
            var pager = new PanelControl { Dock = DockStyle.Bottom, Height = 48, BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder };
            _productPrevious.SetBounds(10, 8, 90, 28);
            _productNext.SetBounds(106, 8, 90, 28);
            _addProduct.Dock = DockStyle.Right;
            _addProduct.Width = 155;
            ConfigureSecondaryButton(_productPrevious);
            ConfigureSecondaryButton(_productNext);
            ConfigurePrimaryButton(_addProduct, System.Drawing.Color.FromArgb(37, 99, 235));
            pager.Controls.AddRange(new Control[] { _productPrevious, _productNext, _addProduct });
            parent.Controls.Add(_products);
            parent.Controls.Add(pager);
            parent.Controls.Add(filters);
            parent.Controls.Add(sectionHeader);

            _barcode.KeyDown += async (sender, args) => { if (args.KeyCode == Keys.Enter) { args.SuppressKeyPress = true; await ScanAsync(); } };
            _search.KeyDown += async (sender, args) => { if (args.KeyCode == Keys.Enter) { args.SuppressKeyPress = true; _productPageNumber = 1; await SearchAsync(); } };
            _searchButton.Click += async (sender, args) => { _productPageNumber = 1; await SearchAsync(); };
            _productPrevious.Click += async (sender, args) => { if (_productPageNumber > 1) { _productPageNumber--; await SearchAsync(); } };
            _productNext.Click += async (sender, args) => { _productPageNumber++; await SearchAsync(); };
            _addProduct.Click += async (sender, args) => await AddSelectedProductAsync();
            _productView.DoubleClick += async (sender, args) => await AddSelectedProductAsync();
            _productView.FocusedRowChanged += (sender, args) => ApplyControlState();
            _customer.EditValueChanged += async (sender, args) => await CustomerChangedAsync();
        }

        private void BuildCart(Control parent)
        {
            var sectionHeader = SectionHeader("CURRENT CART", "Review items, customer, and payment total");
            _cartGrid.Dock = DockStyle.Fill;
            _cartGrid.MainView = _cartView;
            _cartGrid.ViewCollection.Add(_cartView);
            _cartView.OptionsBehavior.Editable = false;
            _cartView.OptionsView.ShowGroupPanel = false;
            StyleGrid(_cartView);
            CartColumn(nameof(SaleCartLineDTO.ProductName), "Product");
            CartColumn(nameof(SaleCartLineDTO.Sku), "SKU");
            CartColumn(nameof(SaleCartLineDTO.Quantity), "Qty");
            CartColumn(nameof(SaleCartLineDTO.QuantityAvailable), "Available");
            CartColumn(nameof(SaleCartLineDTO.UnitPrice), "Price", "n2");
            CartColumn(nameof(SaleCartLineDTO.TaxAmount), "Tax", "n2");
            CartColumn(nameof(SaleCartLineDTO.LineTotal), "Total", "n2");

            var summary = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 164, ColumnCount = 4, RowCount = 3, Padding = new Padding(10, 7, 10, 8), BackColor = System.Drawing.Color.White };
            for (var i = 0; i < 4; i++) summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            summary.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            summary.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            summary.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            summary.Controls.Add(Summary("Subtotal", _subtotal), 0, 0);
            summary.Controls.Add(Summary("Tax", _tax), 1, 0);
            summary.Controls.Add(Summary("Discount", _discount), 2, 0);
            summary.Controls.Add(Summary("TOTAL", _total), 3, 0);
            _remove.Dock = DockStyle.Fill;
            _hold.Dock = DockStyle.Fill;
            _resume.Dock = DockStyle.Fill;
            _clear.Dock = DockStyle.Fill;
            _payment.Dock = DockStyle.Fill;
            _payment.Appearance.Font = new System.Drawing.Font("Segoe UI", 14F, System.Drawing.FontStyle.Bold);
            _payment.Appearance.BackColor = System.Drawing.Color.FromArgb(37, 99, 235);
            _payment.Appearance.ForeColor = System.Drawing.Color.White;
            _payment.Appearance.Options.UseBackColor = true;
            _payment.Appearance.Options.UseForeColor = true;
            _payment.Appearance.Options.UseFont = true;
            _payment.AppearanceDisabled.BackColor = System.Drawing.Color.FromArgb(148, 163, 184);
            _payment.AppearanceDisabled.ForeColor = System.Drawing.Color.White;
            _payment.AppearanceDisabled.Options.UseBackColor = true;
            _payment.AppearanceDisabled.Options.UseForeColor = true;
            ConfigureSecondaryButton(_remove);
            ConfigureSecondaryButton(_hold);
            ConfigureSecondaryButton(_resume);
            ConfigureSecondaryButton(_clear);
            summary.Controls.Add(_remove, 0, 1);
            summary.Controls.Add(_hold, 1, 1);
            summary.Controls.Add(_resume, 2, 1);
            summary.Controls.Add(_clear, 3, 1);
            summary.Controls.Add(_payment, 0, 2);
            summary.SetColumnSpan(_payment, 4);
            _status.Dock = DockStyle.Top;
            _status.AutoSizeMode = LabelAutoSizeMode.None;
            _status.Height = 32;
            _status.Padding = new Padding(10, 7, 10, 0);
            _status.Appearance.ForeColor = System.Drawing.Color.FromArgb(71, 85, 105);
            _status.Appearance.Options.UseForeColor = true;
            parent.Controls.Add(_cartGrid);
            parent.Controls.Add(summary);
            parent.Controls.Add(_status);
            parent.Controls.Add(sectionHeader);
            _cartView.DoubleClick += async (sender, args) => await ChangeQuantityAsync();
            _remove.Click += async (sender, args) => await RemoveSelectedAsync();
            _hold.Click += async (sender, args) => await HoldAsync();
            _resume.Click += async (sender, args) => await ResumeAsync();
            _clear.Click += async (sender, args) => await ClearAsync();
            _payment.Click += async (sender, args) => await PaymentAsync();
        }

        private void ProductColumn(string field, string caption, string format = null)
        {
            var column = _productView.Columns.AddVisible(field, caption);
            Format(column, format);
        }

        private void CartColumn(string field, string caption, string format = null)
        {
            var column = _cartView.Columns.AddVisible(field, caption);
            Format(column, format);
        }

        private static void Format(DevExpress.XtraGrid.Columns.GridColumn column, string format)
        {
            if (string.IsNullOrEmpty(format)) return;
            column.DisplayFormat.FormatType = DevExpress.Utils.FormatType.Numeric;
            column.DisplayFormat.FormatString = format;
        }

        private static Control Summary(string caption, LabelControl value)
        {
            var panel = new PanelControl { Dock = DockStyle.Fill, BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder, Padding = new Padding(2, 1, 2, 1) };
            var label = new LabelControl { Text = caption, Dock = DockStyle.Top, Height = 21 };
            label.Appearance.ForeColor = System.Drawing.Color.FromArgb(71, 85, 105);
            label.Appearance.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            label.Appearance.Options.UseForeColor = true;
            label.Appearance.Options.UseFont = true;
            value.Appearance.Font = new System.Drawing.Font("Segoe UI", caption == "TOTAL" ? 16F : 12F, System.Drawing.FontStyle.Bold);
            value.Appearance.ForeColor = caption == "TOTAL"
                ? System.Drawing.Color.FromArgb(30, 64, 175)
                : System.Drawing.Color.FromArgb(15, 23, 42);
            value.Appearance.Options.UseForeColor = true;
            value.Dock = DockStyle.Fill;
            value.AutoSizeMode = LabelAutoSizeMode.None;
            value.Padding = new Padding(0, 0, 0, 2);
            panel.Controls.Add(value);
            panel.Controls.Add(label);
            return panel;
        }

        private static Control SectionHeader(string title, string subtitle)
        {
            var panel = new PanelControl
            {
                Dock = DockStyle.Top,
                Height = 45,
                Padding = new Padding(10, 5, 10, 3),
                BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder
            };
            var heading = new LabelControl { Text = title, Dock = DockStyle.Top, Height = 20 };
            heading.Appearance.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            heading.Appearance.ForeColor = System.Drawing.Color.FromArgb(15, 23, 42);
            heading.Appearance.Options.UseFont = true;
            heading.Appearance.Options.UseForeColor = true;
            var detail = new LabelControl { Text = subtitle, Dock = DockStyle.Fill };
            detail.Appearance.Font = new System.Drawing.Font("Segoe UI", 8.5F);
            detail.Appearance.ForeColor = System.Drawing.Color.FromArgb(71, 85, 105);
            detail.Appearance.Options.UseFont = true;
            detail.Appearance.Options.UseForeColor = true;
            panel.Controls.Add(detail);
            panel.Controls.Add(heading);
            return panel;
        }

        private static void StyleGrid(GridView view)
        {
            view.RowHeight = 30;
            view.OptionsView.EnableAppearanceEvenRow = true;
            view.OptionsView.ShowIndicator = false;
            view.Appearance.HeaderPanel.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            view.Appearance.HeaderPanel.ForeColor = System.Drawing.Color.FromArgb(51, 65, 85);
            view.Appearance.HeaderPanel.BackColor = System.Drawing.Color.FromArgb(226, 232, 240);
            view.Appearance.HeaderPanel.Options.UseFont = true;
            view.Appearance.HeaderPanel.Options.UseForeColor = true;
            view.Appearance.HeaderPanel.Options.UseBackColor = true;
            view.Appearance.Row.Font = new System.Drawing.Font("Segoe UI", 9F);
            view.Appearance.Row.ForeColor = System.Drawing.Color.FromArgb(30, 41, 59);
            view.Appearance.Row.Options.UseFont = true;
            view.Appearance.Row.Options.UseForeColor = true;
            view.Appearance.EvenRow.BackColor = System.Drawing.Color.FromArgb(248, 250, 252);
            view.Appearance.EvenRow.Options.UseBackColor = true;
            view.Appearance.FocusedRow.BackColor = System.Drawing.Color.FromArgb(219, 234, 254);
            view.Appearance.FocusedRow.Options.UseBackColor = true;
        }

        private static void ConfigureSecondaryButton(SimpleButton button)
        {
            button.Appearance.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            button.Appearance.BackColor = System.Drawing.Color.FromArgb(51, 65, 85);
            button.Appearance.ForeColor = System.Drawing.Color.White;
            button.Appearance.Options.UseFont = true;
            button.Appearance.Options.UseBackColor = true;
            button.Appearance.Options.UseForeColor = true;
            button.AppearanceDisabled.BackColor = System.Drawing.Color.FromArgb(203, 213, 225);
            button.AppearanceDisabled.ForeColor = System.Drawing.Color.FromArgb(71, 85, 105);
            button.AppearanceDisabled.Options.UseBackColor = true;
            button.AppearanceDisabled.Options.UseForeColor = true;
            button.MinimumSize = new System.Drawing.Size(0, 36);
        }

        private static void ConfigurePrimaryButton(SimpleButton button, System.Drawing.Color color)
        {
            button.Appearance.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            button.Appearance.BackColor = color;
            button.Appearance.ForeColor = System.Drawing.Color.White;
            button.Appearance.Options.UseFont = true;
            button.Appearance.Options.UseBackColor = true;
            button.Appearance.Options.UseForeColor = true;
        }

        private async Task InitializeAsync()
        {
            if (_busy) return;
            try
            {
                SetBusy(true, "Resolving the current cashier shift...");
                var state = await Task.Run(() =>
                {
                    var shifts = _shiftService.Search(new CashierShiftSearchDTO
                    {
                        CashierUserId = CurrentUser.UserId,
                        Status = "Open",
                        PageNumber = 1,
                        PageSize = 50
                    });
                    if (shifts.TotalCount == 0)
                    {
                        var recent = _shiftService.Search(new CashierShiftSearchDTO
                        {
                            CashierUserId = CurrentUser.UserId,
                            PageNumber = 1,
                            PageSize = 1
                        });
                        throw new InvalidOperationException(recent.Items.Count == 1 &&
                            string.Equals(recent.Items[0].Status, "Closed", StringComparison.OrdinalIgnoreCase)
                            ? "The latest cashier shift is closed. Open a new shift before scanning or searching for products."
                            : "No open cashier shift was found. Open a shift before scanning or searching for products.");
                    }
                    if (shifts.TotalCount != 1 || shifts.Items.Count != 1)
                        throw new InvalidOperationException("More than one open shift was found for this cashier. Resolve the shift state before selling.");
                    var shift = shifts.Items.Single();
                    var cart = _cartService.Create(shift.RegisterStationId, null);
                    return new InitialState(shift, cart, new List<CustomerDTO>());
                });
                _shift = state.Shift;
                _cart = state.Cart;
                try
                {
                    var customers = await Task.Run(() => _customerService.Search(null, true, 1, 200));
                    state.Customers = customers.Items;
                }
                catch (UnauthorizedAccessException)
                {
                    state.Customers = new List<CustomerDTO>();
                }
                _changingCustomer = true;
                _customer.Properties.DataSource = new[] { new CustomerChoice(null, "Walk-in") }
                    .Concat(state.Customers.Select(x => new CustomerChoice(x.Id, x.Code + " - " + x.Name))).ToList();
                _customer.EditValue = null;
                _changingCustomer = false;
                BindCart();
                _productPageNumber = 1;
                _busy = false;
                await SearchAsync();
            }
            catch (Exception exception)
            {
                _shift = null;
                _cart = null;
                _heldSale = null;
                _productPage = null;
                _products.DataSource = new BindingList<SaleCartLineDTO>();
                BindCart();
                var message = SafeMessage(exception, "The sales register could not be opened. Resolve the shift state and reopen the register.");
                _status.Text = message;
                ShowError(exception, "The sales register could not be opened. Resolve the shift state and reopen the register.");
            }
            finally
            {
                SetBusy(false, _shift == null ? _status.Text : ReadyStatus());
                UpdateOperationalHeader();
                FocusBarcode();
            }
        }

        private async Task SearchAsync()
        {
            if (_busy || !RegisterReady || _cartService == null) return;
            try
            {
                SetBusy(true, "Searching products...");
                _productPage = await Task.Run(() => _cartService.SearchProducts(_search.Text, _productPageNumber, PageSize));
                _productPageNumber = _productPage.PageNumber;
                _products.DataSource = new BindingList<SaleCartLineDTO>(_productPage.Items);
                _productPrevious.Enabled = _productPageNumber > 1;
                _productNext.Enabled = _productPageNumber < _productPage.TotalPages;
                _status.Text = _productPage.TotalCount == 0
                    ? "No products matched the search. Check the barcode, SKU, or product name."
                    : string.Format("Products page {0} of {1} - {2:N0} result(s). Select a row and choose ADD SELECTED.",
                        _productPageNumber, _productPage.TotalPages, _productPage.TotalCount);
            }
            catch (Exception exception)
            {
                _status.Text = "Product search failed. Check the connection and try again.";
                ShowError(exception, "Products could not be searched.");
            }
            finally { SetBusy(false, _status.Text); }
        }

        private async Task ScanAsync()
        {
            var code = _barcode.Text;
            if (_busy || string.IsNullOrWhiteSpace(code) || !RegisterReady) return;
            if (await MutateCartAsync(() => _cartService.AddByBarcode(_cart, code), "Adding scanned product..."))
                _barcode.Text = string.Empty;
            FocusBarcode();
        }

        private async Task AddSelectedProductAsync()
        {
            var selected = _productView.GetFocusedRow() as SaleCartLineDTO;
            if (_busy || selected == null || !RegisterReady) return;
            await MutateCartAsync(() => _cartService.AddProduct(_cart, selected.ProductId), "Adding product...");
            FocusBarcode();
        }

        private async Task ChangeQuantityAsync()
        {
            var selected = _cartView.GetFocusedRow() as SaleCartLineDTO;
            if (_busy || selected == null || !RegisterReady) return;
            var text = XtraInputBox.Show("Enter a positive quantity.", "Cart quantity", selected.Quantity.ToString());
            int quantity;
            if (!int.TryParse(text, out quantity) || quantity <= 0) { FocusBarcode(); return; }
            await MutateCartAsync(() => _cartService.UpdateQuantity(_cart, selected.ProductId, quantity), "Updating quantity...");
            FocusBarcode();
        }

        private async Task RemoveSelectedAsync()
        {
            var selected = _cartView.GetFocusedRow() as SaleCartLineDTO;
            if (_busy || selected == null || !RegisterReady) return;
            await MutateCartAsync(() => _cartService.RemoveProduct(_cart, selected.ProductId), "Removing product...");
            FocusBarcode();
        }

        private async Task CustomerChangedAsync()
        {
            if (_changingCustomer || _busy || !RegisterReady) return;
            var id = _customer.EditValue == null ? (int?)null : Convert.ToInt32(_customer.EditValue);
            await MutateCartAsync(() =>
            {
                _cart.CustomerId = id;
                return _cartService.Recalculate(_cart);
            }, "Updating customer...");
            FocusBarcode();
        }

        private async Task<bool> MutateCartAsync(Func<SaleCartDTO> action, string status)
        {
            if (_busy || !RegisterReady) return false;
            try
            {
                SetBusy(true, status);
                _cart = await Task.Run(action);
                BindCart();
                return true;
            }
            catch (Exception exception)
            {
                ShowError(exception, "The cart could not be changed.");
                return false;
            }
            finally { SetBusy(false, RegisterReady ? ReadyStatus() : "Register unavailable. Open or resolve the cashier shift."); }
        }

        private async Task HoldAsync()
        {
            if (_busy || !RegisterReady || _cart.Items.Count == 0) return;
            try
            {
                SetBusy(true, "Holding sale...");
                var request = BuildHeldSaleRequest();
                await Task.Run(() => _salesService.SaveHeldSale(request));
                _heldSale = null;
                _cart = await Task.Run(() => _cartService.Create(_shift.RegisterStationId, null));
                SelectCustomer(null);
                BindCart();
            }
            catch (Exception exception) { ShowError(exception, "The sale could not be held."); }
            finally { SetBusy(false, RegisterReady ? ReadyStatus() : "Register unavailable. Open or resolve the cashier shift."); FocusBarcode(); }
        }

        private HeldSaleSaveDTO BuildHeldSaleRequest()
        {
            return new HeldSaleSaveDTO
            {
                Id = _heldSale == null ? (int?)null : _heldSale.Id,
                RequestId = _heldSale == null ? Guid.NewGuid() : _heldSale.RequestId,
                Revision = _heldSale == null ? null : _heldSale.Revision,
                RegisterStationId = _cart.RegisterStationId,
                CustomerId = _cart.CustomerId,
                DiscountAmount = _cart.DiscountAmount,
                Items = _cart.Items.Select(x => new SaleCheckoutLineDTO { ProductId = x.ProductId, Quantity = x.Quantity }).ToList()
            };
        }

        private async Task ResumeAsync()
        {
            if (_busy || !RegisterReady || _createHeldSales == null) return;
            HeldSaleDTO held;
            using (var form = _createHeldSales())
            {
                if (form.ShowDialog(FindForm()) != DialogResult.OK || form.SelectedHeldSale == null) { FocusBarcode(); return; }
                held = form.SelectedHeldSale;
            }
            if (_heldSale != null && _heldSale.Id == held.Id)
            {
                XtraMessageBox.Show("That held sale is already the active cart.", "Resume Held Sale",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                FocusBarcode();
                return;
            }
            var currentChoice = ResumeCurrentCartChoice.Discard;
            if (_cart.Items.Count > 0)
            {
                using (var decision = new ResumeCartDecisionForm())
                {
                    if (decision.ShowDialog(FindForm()) != DialogResult.OK) { FocusBarcode(); return; }
                    currentChoice = decision.Choice;
                }
            }
            try
            {
                SetBusy(true, "Resuming held sale...");
                if (held.RegisterStationId != _shift.RegisterStationId)
                    throw new InvalidOperationException("The held sale belongs to a different register.");
                var resumed = await Task.Run(() =>
                {
                    var cart = _cartService.Create(held.RegisterStationId, held.CustomerId);
                    foreach (var line in held.Items) cart = _cartService.AddProduct(cart, line.ProductId, line.Quantity);
                    cart.DiscountAmount = held.DiscountAmount;
                    return _cartService.Recalculate(cart);
                });
                if (currentChoice == ResumeCurrentCartChoice.Hold)
                {
                    SetBusy(true, "Holding the current sale before resume...");
                    var currentRequest = BuildHeldSaleRequest();
                    await Task.Run(() => _salesService.SaveHeldSale(currentRequest));
                }
                _heldSale = held;
                _cart = resumed;
                SelectCustomer(held.CustomerId);
                BindCart();
            }
            catch (Exception exception) { ShowError(exception, "The held sale could not be resumed."); }
            finally { SetBusy(false, RegisterReady ? ReadyStatus() : "Register unavailable. Open or resolve the cashier shift."); FocusBarcode(); }
        }

        private async Task ClearAsync()
        {
            if (_busy || !RegisterReady) return;
            if (_cart != null && _cart.Items.Count > 0 && XtraMessageBox.Show("Clear the current cart?", "Sales Register",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) { FocusBarcode(); return; }
            try
            {
                SetBusy(true, "Clearing cart...");
                _cart = await Task.Run(() => _cartService.Create(_shift.RegisterStationId, null));
                _heldSale = null;
                SelectCustomer(null);
                BindCart();
            }
            catch (Exception exception) { ShowError(exception, "The cart could not be cleared."); }
            finally { SetBusy(false, RegisterReady ? ReadyStatus() : "Register unavailable. Open or resolve the cashier shift."); FocusBarcode(); }
        }

        private async Task PaymentAsync()
        {
            if (_busy || !RegisterReady || _cart.Items.Count == 0) return;
            SetBusy(true, "Opening payment...");
            try
            {
                using (var form = new frmPayment(_salesService, _receiptService, _cart, _shift, _heldSale))
                {
                    form.ShowDialog(FindForm());
                    if (!form.CheckoutConfirmed) return;
                }
                _cart = await Task.Run(() => _cartService.Create(_shift.RegisterStationId, null));
                _heldSale = null;
                SelectCustomer(null);
                BindCart();
            }
            catch (Exception exception) { ShowError(exception, "Payment could not be opened."); }
            finally { SetBusy(false, RegisterReady ? ReadyStatus() : "Register unavailable. Open or resolve the cashier shift."); FocusBarcode(); }
        }

        private void SelectCustomer(int? id)
        {
            _changingCustomer = true;
            _customer.EditValue = id.HasValue ? (object)id.Value : null;
            _changingCustomer = false;
        }

        private void BindCart()
        {
            _cartGrid.DataSource = new BindingList<SaleCartLineDTO>(_cart == null ? new List<SaleCartLineDTO>() : _cart.Items);
            ApplyMoneyFormats();
            _subtotal.Text = Money(_cart == null ? 0 : _cart.Subtotal);
            _tax.Text = Money(_cart == null ? 0 : _cart.TaxAmount);
            _discount.Text = Money(_cart == null ? 0 : _cart.DiscountAmount);
            _total.Text = Money(_cart == null ? 0 : _cart.TotalAmount);
            _itemCount.Text = string.Format("ITEMS  {0:N0}", _cart == null ? 0 : _cart.Items.Sum(x => x.Quantity));
            UpdateOperationalHeader();
            ApplyControlState();
        }

        private void ApplyMoneyFormats()
        {
            var format = "n" + (_cart == null ? 2 : Math.Max(0, Math.Min(4, _cart.MoneyDecimalPlaces)));
            foreach (var field in new[] { nameof(SaleCartLineDTO.UnitPrice), nameof(SaleCartLineDTO.TaxAmount), nameof(SaleCartLineDTO.LineTotal) })
            {
                var productColumn = _productView.Columns.ColumnByFieldName(field);
                if (productColumn != null) productColumn.DisplayFormat.FormatString = format;
                var cartColumn = _cartView.Columns.ColumnByFieldName(field);
                if (cartColumn != null) cartColumn.DisplayFormat.FormatString = format;
            }
        }

        private string Money(decimal value)
        {
            var places = _cart == null ? 2 : Math.Max(0, Math.Min(4, _cart.MoneyDecimalPlaces));
            return string.Format("{0} {1:N" + places + "}",
                _cart == null || string.IsNullOrEmpty(_cart.CurrencyCode) ? "PHP" : _cart.CurrencyCode, value);
        }

        private void SetBusy(bool busy, string status)
        {
            _busy = busy;
            _status.Text = status;
            UseWaitCursor = busy;
            ApplyControlState();
        }

        private bool RegisterReady
        {
            get { return _shift != null && _cart != null && string.Equals(_shift.Status, "Open", StringComparison.OrdinalIgnoreCase); }
        }

        private void ApplyControlState()
        {
            var usable = !_busy && RegisterReady;
            var hasItems = usable && _cart.Items.Count > 0;
            _barcode.Enabled = usable;
            _search.Enabled = usable;
            _searchButton.Enabled = usable;
            _customer.Enabled = usable;
            _products.Enabled = usable;
            _addProduct.Enabled = usable && _productView.GetFocusedRow() != null;
            _cartGrid.Enabled = usable;
            _remove.Enabled = hasItems;
            _hold.Enabled = hasItems;
            _resume.Enabled = usable;
            _clear.Enabled = hasItems;
            _payment.Enabled = hasItems;
            _productPrevious.Enabled = usable && _productPageNumber > 1;
            _productNext.Enabled = usable && _productPage != null && _productPageNumber < _productPage.TotalPages;
        }

        private void UpdateOperationalHeader()
        {
            _registerContext.Text = _shift == null ? "REGISTER  -" : "REGISTER  " + _shift.RegisterCode;
            _cashierContext.Text = "CASHIER  " + (string.IsNullOrWhiteSpace(_shift == null ? null : _shift.CashierName)
                ? (CurrentUser.Fullname ?? CurrentUser.Username ?? CurrentUser.UserId ?? "—") : _shift.CashierName);
            _shiftContext.Text = _shift == null
                ? "SHIFT UNAVAILABLE  -  Open or resolve the cashier shift to enable this register."
                : string.Format("SHIFT #{0}  |  {1}  |  Opened {2:g}", _shift.Id, _shift.Status.ToUpperInvariant(), _shift.OpenedUtc.ToLocalTime());
            if (_cart == null) _itemCount.Text = "ITEMS  0";
        }

        private string ReadyStatus()
        {
            return "Ready for barcode scan - " + _shift.RegisterCode;
        }

        private void FocusBarcode()
        {
            if (!RegisterReady || _busy || IsDisposed || Disposing || !_barcode.CanFocus) return;
            _barcode.SelectAll();
            _barcode.Focus();
        }

        private static void ShowError(Exception exception, string fallback)
        {
            XtraMessageBox.Show(SafeMessage(exception, fallback), "Sales Register", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private static string SafeMessage(Exception exception, string fallback)
        {
            var safe = exception is ValidationException || exception is UnauthorizedAccessException || exception is InvalidOperationException || exception is ArgumentException;
            return safe ? exception.Message : fallback;
        }

        private enum ResumeCurrentCartChoice
        {
            Hold,
            Discard
        }

        private sealed class ResumeCartDecisionForm : XtraForm
        {
            public ResumeCartDecisionForm()
            {
                Text = "Resume Held Sale";
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;
                ShowInTaskbar = false;
                ClientSize = new System.Drawing.Size(520, 178);

                var title = new LabelControl
                {
                    Text = "The active cart contains items.",
                    AutoSizeMode = LabelAutoSizeMode.None
                };
                title.Appearance.Font = new System.Drawing.Font("Segoe UI", 13F, System.Drawing.FontStyle.Bold);
                title.SetBounds(20, 18, 480, 30);
                var explanation = new LabelControl
                {
                    Text = "Choose what to do with it before resuming the held sale.",
                    AutoSizeMode = LabelAutoSizeMode.None
                };
                explanation.SetBounds(20, 53, 480, 25);
                var hold = new SimpleButton { Text = "Hold current" };
                var discard = new SimpleButton { Text = "Discard current" };
                var cancel = new SimpleButton { Text = "Cancel", DialogResult = DialogResult.Cancel };
                hold.SetBounds(20, 112, 145, 38);
                discard.SetBounds(185, 112, 145, 38);
                cancel.SetBounds(350, 112, 145, 38);
                hold.Click += (sender, args) => { Choice = ResumeCurrentCartChoice.Hold; DialogResult = DialogResult.OK; Close(); };
                discard.Click += (sender, args) => { Choice = ResumeCurrentCartChoice.Discard; DialogResult = DialogResult.OK; Close(); };
                Controls.AddRange(new Control[] { title, explanation, hold, discard, cancel });
                CancelButton = cancel;
            }

            public ResumeCurrentCartChoice Choice { get; private set; }
        }

        private sealed class InitialState
        {
            public InitialState(CashierShiftDTO shift, SaleCartDTO cart, IList<CustomerDTO> customers)
            { Shift = shift; Cart = cart; Customers = customers; }
            public CashierShiftDTO Shift { get; private set; }
            public SaleCartDTO Cart { get; private set; }
            public IList<CustomerDTO> Customers { get; set; }
        }

        private sealed class CustomerChoice
        {
            public CustomerChoice(int? id, string display) { Id = id; Display = display; }
            public int? Id { get; private set; }
            public string Display { get; private set; }
        }
    }
}
