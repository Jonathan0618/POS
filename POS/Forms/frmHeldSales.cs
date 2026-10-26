using DevExpress.XtraEditors;
using POS.Models.Operations;
using POS.Services.Point_of_Sale;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace POS.Forms
{
    public partial class frmHeldSales : XtraForm
    {
        private readonly SalesService _service;
        private List<HeldSaleDTO> _all = new List<HeldSaleDTO>();
        private int _pageNumber = 1;
        private bool _busy;

        public frmHeldSales() { InitializeComponent(); ConfigureUi(); }
        public frmHeldSales(SalesService service) : this() { _service = service ?? throw new ArgumentNullException(nameof(service)); }

        public HeldSaleDTO SelectedHeldSale { get; private set; }

        private void ConfigureUi()
        {
            ConfigureResponsiveLayout();
            gridViewHeldSales.OptionsBehavior.Editable = false;
            gridViewHeldSales.OptionsView.ShowGroupPanel = false;
            var fields = new[] { "RowNo", "RegisterCode", "HeldUtc", "CustomerName", "CashierUserId", "ItemCount", "TotalAmount", "Status", "Action" };
            for (var index = 0; index < fields.Length && index < gridViewHeldSales.Columns.Count; index++)
                gridViewHeldSales.Columns[index].FieldName = fields[index];
            lupStatus.Properties.DataSource = new[] { "Held" };
            lupStatus.EditValue = "Held";
            lupPageSize.Properties.DataSource = new[] { 25, 50, 100 };
            lupPageSize.EditValue = 50;
            btnExport.Enabled = false;
            btnExport.ToolTip = "Held-sale export is not part of the service contract.";
            btnRefresh.Click += async (sender, args) => await LoadAsync();
            btnSearch.Click += (sender, args) => { _pageNumber = 1; BindPage(); };
            btnPrevious.Click += (sender, args) => { if (_pageNumber > 1) { _pageNumber--; BindPage(); } };
            btnNext.Click += (sender, args) => { _pageNumber++; BindPage(); };
            gridViewHeldSales.DoubleClick += async (sender, args) => await ResumeAsync();
            var headerResume = windowsUIButtonPanel1.Buttons[0];
            var headerCancel = windowsUIButtonPanel1.Buttons[1];
            headerResume.Properties.Caption = "Resume";
            headerCancel.Properties.Caption = "Cancel";
            windowsUIButtonPanel1.ButtonClick += async (sender, args) =>
            {
                if (ReferenceEquals(args.Button, headerResume)) await ResumeAsync();
                else if (ReferenceEquals(args.Button, headerCancel)) await CancelSelectedAsync();
            };
            var rowResume = repositoryItemButtonEdit1.Buttons[0];
            rowResume.Caption = "Resume";
            var rowCancel = new DevExpress.XtraEditors.Controls.EditorButton { Caption = "Cancel" };
            repositoryItemButtonEdit1.Buttons.Add(rowCancel);
            repositoryItemButtonEdit1.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.HideTextEditor;
            repositoryItemButtonEdit1.ButtonClick += async (sender, args) =>
            {
                if (ReferenceEquals(args.Button, rowResume)) await ResumeAsync();
                else if (ReferenceEquals(args.Button, rowCancel)) await CancelSelectedAsync();
            };
            gridColumn9.Width = 150;
            Shown += async (sender, args) => await LoadAsync();
        }

        private void ConfigureResponsiveLayout()
        {
            SuspendLayout();
            MinimumSize = new System.Drawing.Size(1050, 650);
            var footer = new PanelControl { Dock = DockStyle.Fill, BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder };
            var pageNavigation = new FlowLayoutPanel { Dock = DockStyle.Left, AutoSize = true, WrapContents = false, Padding = new Padding(0, 7, 0, 0) };
            pageNavigation.Controls.AddRange(new Control[] { btnPrevious, btnNext });
            var pageSize = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0, 7, 0, 0)
            };
            pageSize.Controls.AddRange(new Control[] { lblShow, lupPageSize, lblPages });
            footer.Controls.Add(pageSize);
            footer.Controls.Add(pageNavigation);

            pnlHeader.Dock = DockStyle.Fill;
            grpSearchFilter.Dock = DockStyle.Fill;
            grpHeldSalesList.Dock = DockStyle.Fill;
            btnRefresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            var searchBox = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            searchBox.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            searchBox.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 38));
            txtSearch.Dock = DockStyle.Fill;
            btnSearch.Dock = DockStyle.Fill;
            searchBox.Controls.Add(txtSearch, 0, 0);
            searchBox.Controls.Add(btnSearch, 1, 0);
            var filters = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 2, Padding = new Padding(12, 5, 12, 8) };
            foreach (var width in new[] { 20F, 22F, 22F, 18F, 18F })
                filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, width));
            filters.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
            filters.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var labels = new Control[] { lblSearch, lblDateForm, lblDateTo, lblCashier, lblStatus };
            var editors = new Control[] { searchBox, deDataForm, deDateTo, lupCashier, lupStatus };
            for (var column = 0; column < labels.Length; column++)
            {
                labels[column].Dock = DockStyle.Bottom;
                editors[column].Dock = DockStyle.Fill;
                filters.Controls.Add(labels[column], column, 0);
                filters.Controls.Add(editors[column], column, 1);
            }
            grpSearchFilter.Controls.Clear();
            grpSearchFilter.Controls.Add(filters);

            var listHeader = new PanelControl { Dock = DockStyle.Fill, BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder };
            var listActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0, 7, 0, 0)
            };
            listActions.Controls.AddRange(new Control[] { lblTotalHeldSales, btnExport });
            listHeader.Controls.Add(listActions);
            var listLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            listLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
            listLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            gridHeldSales.Dock = DockStyle.Fill;
            grpHeldSalesList.Controls.Clear();
            listLayout.Controls.Add(listHeader, 0, 0);
            listLayout.Controls.Add(gridHeldSales, 0, 1);
            grpHeldSalesList.Controls.Add(listLayout);

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(12) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 140));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            Controls.Clear();
            root.Controls.Add(pnlHeader, 0, 0);
            root.Controls.Add(grpSearchFilter, 0, 1);
            root.Controls.Add(grpHeldSalesList, 0, 2);
            root.Controls.Add(footer, 0, 3);
            Controls.Add(root);
            ResumeLayout(true);
        }

        private async Task LoadAsync()
        {
            if (_busy || _service == null) return;
            try
            {
                SetBusy(true, "Loading held sales...");
                _all = (await Task.Run(() => _service.GetHeldSales(100))).ToList();
                var cashiers = _all.Select(x => x.CashierUserId).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().OrderBy(x => x).ToList();
                lupCashier.Properties.DataSource = cashiers;
                _pageNumber = 1;
                BindPage();
            }
            catch (Exception exception) { ShowError(exception); }
            finally { if (!IsDisposed) SetBusy(false, lblStatus.Text); }
        }

        private IEnumerable<HeldSaleDTO> Filtered()
        {
            IEnumerable<HeldSaleDTO> query = _all;
            var term = txtSearch.Text == null ? null : txtSearch.Text.Trim();
            if (!string.IsNullOrEmpty(term)) query = query.Where(x =>
                (x.RegisterCode ?? string.Empty).IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 ||
                (x.CustomerName ?? string.Empty).IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 ||
                (x.CashierUserId ?? string.Empty).IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
            if (deDataForm.EditValue != null) query = query.Where(x => x.HeldUtc.ToLocalTime().Date >= deDataForm.DateTime.Date);
            if (deDateTo.EditValue != null) query = query.Where(x => x.HeldUtc.ToLocalTime().Date <= deDateTo.DateTime.Date);
            if (lupCashier.EditValue != null) query = query.Where(x => string.Equals(x.CashierUserId, Convert.ToString(lupCashier.EditValue), StringComparison.Ordinal));
            return query.OrderByDescending(x => x.HeldUtc);
        }

        private void BindPage()
        {
            var pageSize = Convert.ToInt32(lupPageSize.EditValue ?? 50);
            var filtered = Filtered().ToList();
            var pages = Math.Max(1, (int)Math.Ceiling(filtered.Count / (double)pageSize));
            _pageNumber = Math.Max(1, Math.Min(_pageNumber, pages));
            var rows = filtered.Skip((_pageNumber - 1) * pageSize).Take(pageSize)
                .Select((item, index) => new HeldSaleRow(item, ((_pageNumber - 1) * pageSize) + index + 1)).ToList();
            gridHeldSales.DataSource = new BindingList<HeldSaleRow>(rows);
            lblTotalHeldSales.Text = string.Format("{0:N0} held sale(s)", filtered.Count);
            lblPages.Text = string.Format("Page {0} of {1}", _pageNumber, pages);
            btnPrevious.Enabled = _pageNumber > 1;
            btnNext.Enabled = _pageNumber < pages;
            lblStatus.Text = filtered.Count == 0 ? "No held sales matched the filters." : "Double-click a held sale to resume it.";
        }

        private HeldSaleDTO Selected => (gridViewHeldSales.GetFocusedRow() as HeldSaleRow)?.Source;

        private async Task ResumeAsync()
        {
            if (_busy || _service == null) return;
            var selected = Selected;
            if (selected == null) { XtraMessageBox.Show("Select a held sale first.", "Held Sales"); return; }
            try
            {
                SetBusy(true, "Loading held sale...");
                SelectedHeldSale = await Task.Run(() => _service.GetHeldSale(selected.Id));
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception exception) { ShowError(exception); }
            finally { if (!IsDisposed) SetBusy(false, lblStatus.Text); }
        }

        private async Task CancelSelectedAsync()
        {
            if (_busy || _service == null) return;
            var selected = Selected;
            if (selected == null) { XtraMessageBox.Show("Select a held sale first.", "Held Sales"); return; }
            if (XtraMessageBox.Show("Cancel the selected held sale? No stock or payment has been posted.", "Held Sales",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try
            {
                SetBusy(true, "Cancelling held sale...");
                await Task.Run(() => _service.CancelHeldSale(selected.Id, selected.Revision));
                _busy = false;
                await LoadAsync();
            }
            catch (Exception exception) { ShowError(exception); }
            finally { if (!IsDisposed) SetBusy(false, lblStatus.Text); }
        }

        private void SetBusy(bool busy, string status)
        {
            _busy = busy; grpSearchFilter.Enabled = !busy; grpHeldSalesList.Enabled = !busy;
            windowsUIButtonPanel1.Enabled = !busy; lblStatus.Text = status; UseWaitCursor = busy;
        }
        private static void ShowError(Exception exception)
        {
            var message = exception is ValidationException || exception is UnauthorizedAccessException || exception is InvalidOperationException
                ? exception.Message : "Held sales could not be loaded or changed. Please try again.";
            XtraMessageBox.Show(message, "Held Sales", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void lblSubtitle_Click(object sender, EventArgs e) { }
        private void groupControl1_Paint_1(object sender, PaintEventArgs e) { }
        private void labelControl1_Click(object sender, EventArgs e) { }

        private sealed class HeldSaleRow
        {
            public HeldSaleRow(HeldSaleDTO source, int rowNo) { Source = source; RowNo = rowNo; }
            [Browsable(false)] public HeldSaleDTO Source { get; }
            public int RowNo { get; }
            public string RegisterCode => Source.RegisterCode;
            public DateTime HeldUtc => Source.HeldUtc.ToLocalTime();
            public string CustomerName => Source.CustomerName ?? "Walk-in";
            public string CashierUserId => Source.CashierUserId;
            public int ItemCount => Source.Items == null ? 0 : Source.Items.Sum(x => x.Quantity);
            public decimal TotalAmount => Source.TotalAmount;
            public string Status => "Held";
            public string Action => "Resume";
        }
    }
}
