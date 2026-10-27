namespace POS.Forms
{
    partial class ucPurchaseOrders
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.pnlHeader = new DevExpress.XtraEditors.PanelControl();
            this.lblTitle = new DevExpress.XtraEditors.LabelControl();
            this.btnNewPurchaseOrder = new DevExpress.XtraEditors.SimpleButton();
            this.txtSearch = new DevExpress.XtraEditors.SearchControl();
            this.gridPurchaseOrders = new DevExpress.XtraGrid.GridControl();
            this.gvPurchaseOrders = new DevExpress.XtraGrid.Views.Grid.GridView();
            this.colOrderNumber = new DevExpress.XtraGrid.Columns.GridColumn();
            this.colSupplierCode = new DevExpress.XtraGrid.Columns.GridColumn();
            this.colSupplierName = new DevExpress.XtraGrid.Columns.GridColumn();
            this.colStatus = new DevExpress.XtraGrid.Columns.GridColumn();
            this.colCreatedDate = new DevExpress.XtraGrid.Columns.GridColumn();
            this.colOrderedDate = new DevExpress.XtraGrid.Columns.GridColumn();
            this.colCancelledDate = new DevExpress.XtraGrid.Columns.GridColumn();
            this.colCancellationReason = new DevExpress.XtraGrid.Columns.GridColumn();
            this.colTotal = new DevExpress.XtraGrid.Columns.GridColumn();
            this.colLineCount = new DevExpress.XtraGrid.Columns.GridColumn();
            this.slueSupplier = new DevExpress.XtraEditors.SearchLookUpEdit();
            this.searchLookUpEdit1View = new DevExpress.XtraGrid.Views.Grid.GridView();
            this.cboStatus = new DevExpress.XtraEditors.ComboBoxEdit();
            this.slueProduct = new DevExpress.XtraEditors.SearchLookUpEdit();
            this.gridView1 = new DevExpress.XtraGrid.Views.Grid.GridView();
            this.deFromDate = new DevExpress.XtraEditors.DateEdit();
            this.deToDate = new DevExpress.XtraEditors.DateEdit();
            ((System.ComponentModel.ISupportInitialize)(this.pnlHeader)).BeginInit();
            this.pnlHeader.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.txtSearch.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridPurchaseOrders)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gvPurchaseOrders)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.slueSupplier.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.searchLookUpEdit1View)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.cboStatus.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.slueProduct.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridView1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.deFromDate.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.deFromDate.Properties.CalendarTimeProperties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.deToDate.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.deToDate.Properties.CalendarTimeProperties)).BeginInit();
            this.SuspendLayout();
            // 
            // pnlHeader
            // 
            this.pnlHeader.Appearance.BackColor = System.Drawing.Color.RoyalBlue;
            this.pnlHeader.Appearance.Options.UseBackColor = true;
            this.pnlHeader.Controls.Add(this.lblTitle);
            this.pnlHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlHeader.Location = new System.Drawing.Point(0, 0);
            this.pnlHeader.Name = "pnlHeader";
            this.pnlHeader.Size = new System.Drawing.Size(1432, 100);
            this.pnlHeader.TabIndex = 0;
            // 
            // lblTitle
            // 
            this.lblTitle.Appearance.Font = new System.Drawing.Font("Segoe UI", 18F, System.Drawing.FontStyle.Bold);
            this.lblTitle.Appearance.FontStyleDelta = System.Drawing.FontStyle.Bold;
            this.lblTitle.Appearance.Options.UseFont = true;
            this.lblTitle.Location = new System.Drawing.Point(25, 30);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(273, 48);
            this.lblTitle.TabIndex = 0;
            this.lblTitle.Text = "Purchase Orders";
            // 
            // btnNewPurchaseOrder
            // 
            this.btnNewPurchaseOrder.Location = new System.Drawing.Point(25, 128);
            this.btnNewPurchaseOrder.Name = "btnNewPurchaseOrder";
            this.btnNewPurchaseOrder.Size = new System.Drawing.Size(170, 40);
            this.btnNewPurchaseOrder.TabIndex = 1;
            this.btnNewPurchaseOrder.Text = "New Purchase Order";
            // 
            // txtSearch
            // 
            this.txtSearch.Location = new System.Drawing.Point(1100, 140);
            this.txtSearch.Name = "txtSearch";
            this.txtSearch.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Repository.ClearButton(),
            new DevExpress.XtraEditors.Repository.SearchButton()});
            this.txtSearch.Size = new System.Drawing.Size(307, 28);
            this.txtSearch.TabIndex = 2;
            // 
            // gridPurchaseOrders
            // 
            this.gridPurchaseOrders.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.gridPurchaseOrders.Location = new System.Drawing.Point(25, 180);
            this.gridPurchaseOrders.MainView = this.gvPurchaseOrders;
            this.gridPurchaseOrders.Name = "gridPurchaseOrders";
            this.gridPurchaseOrders.Size = new System.Drawing.Size(1382, 500);
            this.gridPurchaseOrders.TabIndex = 3;
            this.gridPurchaseOrders.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[] {
            this.gvPurchaseOrders});
            this.gridPurchaseOrders.Click += new System.EventHandler(this.gridPurchaseOrders_Click);
            // 
            // gvPurchaseOrders
            // 
            this.gvPurchaseOrders.Columns.AddRange(new DevExpress.XtraGrid.Columns.GridColumn[] {
            this.colOrderNumber,
            this.colSupplierCode,
            this.colSupplierName,
            this.colStatus,
            this.colCreatedDate,
            this.colOrderedDate,
            this.colCancelledDate,
            this.colCancellationReason,
            this.colTotal,
            this.colLineCount});
            this.gvPurchaseOrders.FocusRectStyle = DevExpress.XtraGrid.Views.Grid.DrawFocusRectStyle.RowFocus;
            this.gvPurchaseOrders.GridControl = this.gridPurchaseOrders;
            this.gvPurchaseOrders.Name = "gvPurchaseOrders";
            this.gvPurchaseOrders.OptionsBehavior.Editable = false;
            this.gvPurchaseOrders.OptionsCustomization.AllowColumnMoving = false;
            this.gvPurchaseOrders.OptionsCustomization.AllowColumnResizing = false;
            this.gvPurchaseOrders.OptionsCustomization.AllowFilter = false;
            this.gvPurchaseOrders.OptionsCustomization.AllowSort = false;
            this.gvPurchaseOrders.OptionsMenu.EnableColumnMenu = false;
            this.gvPurchaseOrders.OptionsSelection.EnableAppearanceFocusedCell = false;
            this.gvPurchaseOrders.OptionsView.ShowGroupPanel = false;
            this.gvPurchaseOrders.OptionsView.ShowIndicator = false;
            // 
            // colOrderNumber
            // 
            this.colOrderNumber.Caption = "Order Number";
            this.colOrderNumber.FieldName = "OrderNumber";
            this.colOrderNumber.MinWidth = 30;
            this.colOrderNumber.Name = "colOrderNumber";
            this.colOrderNumber.Visible = true;
            this.colOrderNumber.VisibleIndex = 0;
            this.colOrderNumber.Width = 112;
            // 
            // colSupplierCode
            // 
            this.colSupplierCode.Caption = "Supplier Code";
            this.colSupplierCode.FieldName = "SupplierCode";
            this.colSupplierCode.MinWidth = 30;
            this.colSupplierCode.Name = "colSupplierCode";
            this.colSupplierCode.Visible = true;
            this.colSupplierCode.VisibleIndex = 1;
            this.colSupplierCode.Width = 112;
            // 
            // colSupplierName
            // 
            this.colSupplierName.Caption = "Supplier Name";
            this.colSupplierName.FieldName = "SupplierName";
            this.colSupplierName.MinWidth = 30;
            this.colSupplierName.Name = "colSupplierName";
            this.colSupplierName.Visible = true;
            this.colSupplierName.VisibleIndex = 2;
            this.colSupplierName.Width = 112;
            // 
            // colStatus
            // 
            this.colStatus.Caption = "Status";
            this.colStatus.FieldName = "Status";
            this.colStatus.MinWidth = 30;
            this.colStatus.Name = "colStatus";
            this.colStatus.Visible = true;
            this.colStatus.VisibleIndex = 3;
            this.colStatus.Width = 112;
            // 
            // colCreatedDate
            // 
            this.colCreatedDate.Caption = "Created Date";
            this.colCreatedDate.FieldName = "CreatedDate";
            this.colCreatedDate.MinWidth = 30;
            this.colCreatedDate.Name = "colCreatedDate";
            this.colCreatedDate.Visible = true;
            this.colCreatedDate.VisibleIndex = 4;
            this.colCreatedDate.Width = 112;
            // 
            // colOrderedDate
            // 
            this.colOrderedDate.Caption = "Ordered Date";
            this.colOrderedDate.FieldName = "OrderedDate";
            this.colOrderedDate.MinWidth = 30;
            this.colOrderedDate.Name = "colOrderedDate";
            this.colOrderedDate.Visible = true;
            this.colOrderedDate.VisibleIndex = 5;
            this.colOrderedDate.Width = 112;
            // 
            // colCancelledDate
            // 
            this.colCancelledDate.Caption = "Cancelled Date";
            this.colCancelledDate.FieldName = "CancelledDate";
            this.colCancelledDate.MinWidth = 30;
            this.colCancelledDate.Name = "colCancelledDate";
            this.colCancelledDate.Visible = true;
            this.colCancelledDate.VisibleIndex = 6;
            this.colCancelledDate.Width = 112;
            // 
            // colCancellationReason
            // 
            this.colCancellationReason.Caption = "Cancellation Reason";
            this.colCancellationReason.FieldName = "CancellationReason";
            this.colCancellationReason.MinWidth = 30;
            this.colCancellationReason.Name = "colCancellationReason";
            this.colCancellationReason.Visible = true;
            this.colCancellationReason.VisibleIndex = 7;
            this.colCancellationReason.Width = 112;
            // 
            // colTotal
            // 
            this.colTotal.Caption = "Total";
            this.colTotal.FieldName = "Total";
            this.colTotal.MinWidth = 30;
            this.colTotal.Name = "colTotal";
            this.colTotal.Visible = true;
            this.colTotal.VisibleIndex = 8;
            this.colTotal.Width = 112;
            // 
            // colLineCount
            // 
            this.colLineCount.Caption = "Line Count";
            this.colLineCount.FieldName = "LineCount";
            this.colLineCount.MinWidth = 30;
            this.colLineCount.Name = "colLineCount";
            this.colLineCount.Visible = true;
            this.colLineCount.VisibleIndex = 9;
            this.colLineCount.Width = 112;
            // 
            // slueSupplier
            // 
            this.slueSupplier.Location = new System.Drawing.Point(929, 140);
            this.slueSupplier.Name = "slueSupplier";
            this.slueSupplier.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)});
            this.slueSupplier.Properties.NullText = "All supppliers";
            this.slueSupplier.Properties.PopupView = this.searchLookUpEdit1View;
            this.slueSupplier.Size = new System.Drawing.Size(150, 28);
            this.slueSupplier.TabIndex = 4;
            this.slueSupplier.EditValueChanged += new System.EventHandler(this.slueSupplier_EditValueChanged);
            // 
            // searchLookUpEdit1View
            // 
            this.searchLookUpEdit1View.FocusRectStyle = DevExpress.XtraGrid.Views.Grid.DrawFocusRectStyle.RowFocus;
            this.searchLookUpEdit1View.Name = "searchLookUpEdit1View";
            this.searchLookUpEdit1View.OptionsSelection.EnableAppearanceFocusedCell = false;
            this.searchLookUpEdit1View.OptionsView.ShowGroupPanel = false;
            // 
            // cboStatus
            // 
            this.cboStatus.Location = new System.Drawing.Point(773, 140);
            this.cboStatus.Name = "cboStatus";
            this.cboStatus.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)});
            this.cboStatus.Properties.NullText = "All Statuses";
            this.cboStatus.Size = new System.Drawing.Size(150, 28);
            this.cboStatus.TabIndex = 5;
            this.cboStatus.SelectedIndexChanged += new System.EventHandler(this.cboStatus_SelectedIndexChanged);
            // 
            // slueProduct
            // 
            this.slueProduct.Location = new System.Drawing.Point(617, 140);
            this.slueProduct.Name = "slueProduct";
            this.slueProduct.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)});
            this.slueProduct.Properties.NullText = "All Products";
            this.slueProduct.Properties.PopupView = this.gridView1;
            this.slueProduct.Size = new System.Drawing.Size(150, 28);
            this.slueProduct.TabIndex = 6;
            this.slueProduct.EditValueChanged += new System.EventHandler(this.slueProduct_EditValueChanged);
            // 
            // gridView1
            // 
            this.gridView1.FocusRectStyle = DevExpress.XtraGrid.Views.Grid.DrawFocusRectStyle.RowFocus;
            this.gridView1.Name = "gridView1";
            this.gridView1.OptionsSelection.EnableAppearanceFocusedCell = false;
            this.gridView1.OptionsView.ShowGroupPanel = false;
            // 
            // deFromDate
            // 
            this.deFromDate.EditValue = null;
            this.deFromDate.Location = new System.Drawing.Point(305, 140);
            this.deFromDate.Name = "deFromDate";
            this.deFromDate.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)});
            this.deFromDate.Properties.CalendarTimeProperties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)});
            this.deFromDate.Properties.NullText = "From Date";
            this.deFromDate.Size = new System.Drawing.Size(150, 28);
            this.deFromDate.TabIndex = 7;
            // 
            // deToDate
            // 
            this.deToDate.EditValue = null;
            this.deToDate.Location = new System.Drawing.Point(461, 140);
            this.deToDate.Name = "deToDate";
            this.deToDate.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)});
            this.deToDate.Properties.CalendarTimeProperties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)});
            this.deToDate.Properties.NullText = "To Date";
            this.deToDate.Size = new System.Drawing.Size(150, 28);
            this.deToDate.TabIndex = 8;
            // 
            // ucPurchaseOrders
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 21F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.deToDate);
            this.Controls.Add(this.deFromDate);
            this.Controls.Add(this.slueProduct);
            this.Controls.Add(this.cboStatus);
            this.Controls.Add(this.slueSupplier);
            this.Controls.Add(this.gridPurchaseOrders);
            this.Controls.Add(this.txtSearch);
            this.Controls.Add(this.btnNewPurchaseOrder);
            this.Controls.Add(this.pnlHeader);
            this.Name = "ucPurchaseOrders";
            this.Size = new System.Drawing.Size(1432, 700);
            ((System.ComponentModel.ISupportInitialize)(this.pnlHeader)).EndInit();
            this.pnlHeader.ResumeLayout(false);
            this.pnlHeader.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.txtSearch.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridPurchaseOrders)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.gvPurchaseOrders)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.slueSupplier.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.searchLookUpEdit1View)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.cboStatus.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.slueProduct.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridView1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.deFromDate.Properties.CalendarTimeProperties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.deFromDate.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.deToDate.Properties.CalendarTimeProperties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.deToDate.Properties)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private DevExpress.XtraEditors.PanelControl pnlHeader;
        private DevExpress.XtraEditors.LabelControl lblTitle;
        private DevExpress.XtraEditors.SimpleButton btnNewPurchaseOrder;
        private DevExpress.XtraEditors.SearchControl txtSearch;
        private DevExpress.XtraGrid.GridControl gridPurchaseOrders;
        private DevExpress.XtraGrid.Views.Grid.GridView gvPurchaseOrders;
        private DevExpress.XtraEditors.SearchLookUpEdit slueSupplier;
        private DevExpress.XtraGrid.Views.Grid.GridView searchLookUpEdit1View;
        private DevExpress.XtraEditors.ComboBoxEdit cboStatus;
        private DevExpress.XtraEditors.SearchLookUpEdit slueProduct;
        private DevExpress.XtraGrid.Views.Grid.GridView gridView1;
        private DevExpress.XtraEditors.DateEdit deFromDate;
        private DevExpress.XtraEditors.DateEdit deToDate;
        private DevExpress.XtraGrid.Columns.GridColumn colOrderNumber;
        private DevExpress.XtraGrid.Columns.GridColumn colSupplierCode;
        private DevExpress.XtraGrid.Columns.GridColumn colSupplierName;
        private DevExpress.XtraGrid.Columns.GridColumn colStatus;
        private DevExpress.XtraGrid.Columns.GridColumn colCreatedDate;
        private DevExpress.XtraGrid.Columns.GridColumn colOrderedDate;
        private DevExpress.XtraGrid.Columns.GridColumn colCancelledDate;
        private DevExpress.XtraGrid.Columns.GridColumn colCancellationReason;
        private DevExpress.XtraGrid.Columns.GridColumn colTotal;
        private DevExpress.XtraGrid.Columns.GridColumn colLineCount;
    }
}
