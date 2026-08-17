namespace POS.Forms
{
    partial class ucProducts
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
            this.components = new System.ComponentModel.Container();
            DevExpress.XtraGrid.GridFormatRule gridFormatRule1 = new DevExpress.XtraGrid.GridFormatRule();
            DevExpress.XtraEditors.FormatConditionRuleExpression formatConditionRuleExpression1 = new DevExpress.XtraEditors.FormatConditionRuleExpression();
            this.Quantity = new DevExpress.XtraGrid.Columns.GridColumn();
            this.gcProducts = new DevExpress.XtraGrid.GridControl();
            this.gcProduct = new DevExpress.XtraGrid.Views.Grid.GridView();
            this.Id = new DevExpress.XtraGrid.Columns.GridColumn();
            this.Barcode = new DevExpress.XtraGrid.Columns.GridColumn();
            this.ProductName = new DevExpress.XtraGrid.Columns.GridColumn();
            this.Description = new DevExpress.XtraGrid.Columns.GridColumn();
            this.CategoryName = new DevExpress.XtraGrid.Columns.GridColumn();
            this.Price = new DevExpress.XtraGrid.Columns.GridColumn();
            this.CostPrice = new DevExpress.XtraGrid.Columns.GridColumn();
            this.Status = new DevExpress.XtraGrid.Columns.GridColumn();
            this.panelControl1 = new DevExpress.XtraEditors.PanelControl();
            this.labelControl1 = new DevExpress.XtraEditors.LabelControl();
            this.behaviorManager1 = new DevExpress.Utils.Behaviors.BehaviorManager(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.gcProducts)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gcProduct)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.panelControl1)).BeginInit();
            this.panelControl1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.behaviorManager1)).BeginInit();
            this.SuspendLayout();
            // 
            // Quantity
            // 
            this.Quantity.Caption = "Quantity";
            this.Quantity.FieldName = "Quantity";
            this.Quantity.Name = "Quantity";
            this.Quantity.OptionsColumn.AllowEdit = false;
            this.Quantity.OptionsColumn.FixedWidth = true;
            this.Quantity.Visible = true;
            this.Quantity.VisibleIndex = 6;
            this.Quantity.Width = 80;
            // 
            // gcProducts
            // 
            this.gcProducts.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gcProducts.EmbeddedNavigator.Buttons.Append.Enabled = false;
            this.gcProducts.EmbeddedNavigator.Buttons.Append.Visible = false;
            this.gcProducts.EmbeddedNavigator.Buttons.CancelEdit.Visible = false;
            this.gcProducts.EmbeddedNavigator.Buttons.Edit.Visible = false;
            this.gcProducts.EmbeddedNavigator.Buttons.EndEdit.Visible = false;
            this.gcProducts.EmbeddedNavigator.Buttons.Remove.Visible = false;
            this.gcProducts.Location = new System.Drawing.Point(0, 54);
            this.gcProducts.MainView = this.gcProduct;
            this.gcProducts.Name = "gcProducts";
            this.gcProducts.Size = new System.Drawing.Size(1166, 528);
            this.gcProducts.TabIndex = 3;
            this.gcProducts.UseEmbeddedNavigator = true;
            this.gcProducts.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[] {
            this.gcProduct});
            this.gcProducts.Click += new System.EventHandler(this.gcProducts_Click);
            // 
            // gcProduct
            // 
            this.gcProduct.Columns.AddRange(new DevExpress.XtraGrid.Columns.GridColumn[] {
            this.Id,
            this.Barcode,
            this.ProductName,
            this.Description,
            this.CategoryName,
            this.Price,
            this.CostPrice,
            this.Quantity,
            this.Status});
            gridFormatRule1.Column = this.Quantity;
            gridFormatRule1.ColumnApplyTo = this.Quantity;
            gridFormatRule1.Name = "Quantity";
            formatConditionRuleExpression1.Appearance.BackColor = System.Drawing.Color.Tomato;
            formatConditionRuleExpression1.Appearance.Options.HighPriority = true;
            formatConditionRuleExpression1.Appearance.Options.UseBackColor = true;
            formatConditionRuleExpression1.Expression = "[Quantity] <= 10";
            gridFormatRule1.Rule = formatConditionRuleExpression1;
            this.gcProduct.FormatRules.Add(gridFormatRule1);
            this.gcProduct.GridControl = this.gcProducts;
            this.gcProduct.Name = "gcProduct";
            this.gcProduct.OptionsFind.AlwaysVisible = true;
            this.gcProduct.OptionsView.ShowGroupPanel = false;
            // 
            // Id
            // 
            this.Id.Caption = "Id";
            this.Id.Name = "Id";
            this.Id.Width = 33;
            // 
            // Barcode
            // 
            this.Barcode.Caption = "Barcode";
            this.Barcode.Name = "Barcode";
            this.Barcode.Visible = true;
            this.Barcode.VisibleIndex = 0;
            this.Barcode.Width = 94;
            // 
            // ProductName
            // 
            this.ProductName.Caption = "Product Name";
            this.ProductName.FieldName = "Name";
            this.ProductName.Name = "ProductName";
            this.ProductName.Visible = true;
            this.ProductName.VisibleIndex = 1;
            this.ProductName.Width = 187;
            // 
            // Description
            // 
            this.Description.Caption = "Description";
            this.Description.FieldName = "Description";
            this.Description.Name = "Description";
            this.Description.Visible = true;
            this.Description.VisibleIndex = 2;
            this.Description.Width = 355;
            // 
            // CategoryName
            // 
            this.CategoryName.Caption = "CategoryName";
            this.CategoryName.FieldName = "CategoryName";
            this.CategoryName.Name = "CategoryName";
            this.CategoryName.OptionsColumn.AllowEdit = false;
            this.CategoryName.OptionsColumn.FixedWidth = true;
            this.CategoryName.Visible = true;
            this.CategoryName.VisibleIndex = 3;
            this.CategoryName.Width = 174;
            // 
            // Price
            // 
            this.Price.Caption = "Price";
            this.Price.FieldName = "Price";
            this.Price.Name = "Price";
            this.Price.OptionsColumn.AllowEdit = false;
            this.Price.OptionsColumn.FixedWidth = true;
            this.Price.Visible = true;
            this.Price.VisibleIndex = 4;
            this.Price.Width = 88;
            // 
            // CostPrice
            // 
            this.CostPrice.Caption = "CostPrice";
            this.CostPrice.Name = "CostPrice";
            this.CostPrice.OptionsColumn.AllowEdit = false;
            this.CostPrice.OptionsColumn.FixedWidth = true;
            this.CostPrice.Visible = true;
            this.CostPrice.VisibleIndex = 5;
            this.CostPrice.Width = 92;
            // 
            // Status
            // 
            this.Status.Caption = "Status";
            this.Status.Name = "Status";
            this.Status.OptionsColumn.AllowEdit = false;
            this.Status.OptionsColumn.FixedWidth = true;
            this.Status.Visible = true;
            this.Status.VisibleIndex = 7;
            // 
            // panelControl1
            // 
            this.panelControl1.Appearance.BackColor = System.Drawing.Color.SteelBlue;
            this.panelControl1.Appearance.Options.UseBackColor = true;
            this.panelControl1.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder;
            this.panelControl1.Controls.Add(this.labelControl1);
            this.panelControl1.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelControl1.Location = new System.Drawing.Point(0, 0);
            this.panelControl1.Name = "panelControl1";
            this.panelControl1.Size = new System.Drawing.Size(1166, 54);
            this.panelControl1.TabIndex = 2;
            this.panelControl1.Paint += new System.Windows.Forms.PaintEventHandler(this.panelControl1_Paint);
            // 
            // labelControl1
            // 
            this.labelControl1.Appearance.Font = new System.Drawing.Font("Consolas", 15.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelControl1.Appearance.ForeColor = System.Drawing.Color.White;
            this.labelControl1.Appearance.Options.UseFont = true;
            this.labelControl1.Appearance.Options.UseForeColor = true;
            this.labelControl1.Location = new System.Drawing.Point(3, 12);
            this.labelControl1.Name = "labelControl1";
            this.labelControl1.Size = new System.Drawing.Size(96, 24);
            this.labelControl1.TabIndex = 0;
            this.labelControl1.Text = "Products";
            // 
            // ucProducts
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.gcProducts);
            this.Controls.Add(this.panelControl1);
            this.Name = "ucProducts";
            this.Size = new System.Drawing.Size(1166, 582);
            this.Load += new System.EventHandler(this.ucProducts_Load_1);
            ((System.ComponentModel.ISupportInitialize)(this.gcProducts)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.gcProduct)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.panelControl1)).EndInit();
            this.panelControl1.ResumeLayout(false);
            this.panelControl1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.behaviorManager1)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private DevExpress.XtraGrid.GridControl gcProducts;
        private DevExpress.XtraGrid.Views.Grid.GridView gcProduct;
        private DevExpress.XtraGrid.Columns.GridColumn Id;
        private DevExpress.XtraGrid.Columns.GridColumn ProductName;
        private DevExpress.XtraGrid.Columns.GridColumn Description;
        private DevExpress.XtraGrid.Columns.GridColumn Price;
        private DevExpress.XtraGrid.Columns.GridColumn CategoryName;
        private DevExpress.XtraEditors.PanelControl panelControl1;
        private DevExpress.XtraEditors.LabelControl labelControl1;
        private DevExpress.XtraGrid.Columns.GridColumn Barcode;
        private DevExpress.XtraGrid.Columns.GridColumn CostPrice;
        private DevExpress.XtraGrid.Columns.GridColumn Quantity;
        private DevExpress.XtraGrid.Columns.GridColumn Status;
        private DevExpress.Utils.Behaviors.BehaviorManager behaviorManager1;
    }
}
