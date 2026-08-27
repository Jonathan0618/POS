namespace POS.Forms
{
    partial class ucStock
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
            this.labelControl1 = new DevExpress.XtraEditors.LabelControl();
            this.panelControl1 = new DevExpress.XtraEditors.PanelControl();
            this.gcStocks = new DevExpress.XtraGrid.GridControl();
            this.gcStock = new DevExpress.XtraGrid.Views.Grid.GridView();
            this.Products = new DevExpress.XtraGrid.Columns.GridColumn();
            this.Unit = new DevExpress.XtraGrid.Columns.GridColumn();
            this.Quantity = new DevExpress.XtraGrid.Columns.GridColumn();
            this.ReorderLevel = new DevExpress.XtraGrid.Columns.GridColumn();
            ((System.ComponentModel.ISupportInitialize)(this.panelControl1)).BeginInit();
            this.panelControl1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gcStocks)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gcStock)).BeginInit();
            this.SuspendLayout();
            // 
            // labelControl1
            // 
            this.labelControl1.Appearance.Font = new System.Drawing.Font("Consolas", 14.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelControl1.Appearance.ForeColor = System.Drawing.Color.White;
            this.labelControl1.Appearance.Options.UseFont = true;
            this.labelControl1.Appearance.Options.UseForeColor = true;
            this.labelControl1.Location = new System.Drawing.Point(20, 16);
            this.labelControl1.Name = "labelControl1";
            this.labelControl1.Size = new System.Drawing.Size(60, 22);
            this.labelControl1.TabIndex = 0;
            this.labelControl1.Text = "STOCKS";
            // 
            // panelControl1
            // 
            this.panelControl1.Appearance.BackColor = System.Drawing.Color.RoyalBlue;
            this.panelControl1.Appearance.Options.UseBackColor = true;
            this.panelControl1.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder;
            this.panelControl1.Controls.Add(this.labelControl1);
            this.panelControl1.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelControl1.Location = new System.Drawing.Point(0, 0);
            this.panelControl1.Name = "panelControl1";
            this.panelControl1.Size = new System.Drawing.Size(868, 51);
            this.panelControl1.TabIndex = 4;
            // 
            // gcStocks
            // 
            this.gcStocks.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gcStocks.Location = new System.Drawing.Point(0, 51);
            this.gcStocks.MainView = this.gcStock;
            this.gcStocks.Name = "gcStocks";
            this.gcStocks.Size = new System.Drawing.Size(868, 428);
            this.gcStocks.TabIndex = 5;
            this.gcStocks.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[] {
            this.gcStock});
            // 
            // gcStock
            // 
            this.gcStock.Columns.AddRange(new DevExpress.XtraGrid.Columns.GridColumn[] {
            this.Products,
            this.Unit,
            this.Quantity,
            this.ReorderLevel});
            this.gcStock.GridControl = this.gcStocks;
            this.gcStock.Name = "gcStock";
            this.gcStock.OptionsView.ShowGroupPanel = false;
            // 
            // Products
            // 
            this.Products.Caption = "Products";
            this.Products.FieldName = "ProductId";
            this.Products.Name = "Products";
            this.Products.Visible = true;
            this.Products.VisibleIndex = 0;
            // 
            // Unit
            // 
            this.Unit.Caption = "Unit";
            this.Unit.FieldName = "Unit";
            this.Unit.Name = "Unit";
            this.Unit.Visible = true;
            this.Unit.VisibleIndex = 1;
            // 
            // Quantity
            // 
            this.Quantity.Caption = "Quantity";
            this.Quantity.FieldName = "Quantity";
            this.Quantity.Name = "Quantity";
            this.Quantity.Visible = true;
            this.Quantity.VisibleIndex = 2;
            // 
            // ReorderLevel
            // 
            this.ReorderLevel.Caption = "ReaorderLevel";
            this.ReorderLevel.FieldName = "ReorderLevel";
            this.ReorderLevel.Name = "ReorderLevel";
            this.ReorderLevel.Visible = true;
            this.ReorderLevel.VisibleIndex = 3;
            // 
            // ucStock
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.gcStocks);
            this.Controls.Add(this.panelControl1);
            this.Name = "ucStock";
            this.Size = new System.Drawing.Size(868, 479);
            this.Load += new System.EventHandler(this.ucStock_Load);
            ((System.ComponentModel.ISupportInitialize)(this.panelControl1)).EndInit();
            this.panelControl1.ResumeLayout(false);
            this.panelControl1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gcStocks)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.gcStock)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private DevExpress.XtraEditors.LabelControl labelControl1;
        private DevExpress.XtraEditors.PanelControl panelControl1;
        private DevExpress.XtraGrid.GridControl gcStocks;
        private DevExpress.XtraGrid.Views.Grid.GridView gcStock;
        private DevExpress.XtraGrid.Columns.GridColumn Products;
        private DevExpress.XtraGrid.Columns.GridColumn Unit;
        private DevExpress.XtraGrid.Columns.GridColumn Quantity;
        private DevExpress.XtraGrid.Columns.GridColumn ReorderLevel;
    }
}
