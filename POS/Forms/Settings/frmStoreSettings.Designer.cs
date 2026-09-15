namespace POS.Forms.Settings
{
    partial class frmStoreSettings
    {
        private System.ComponentModel.IContainer components = null;
        private DevExpress.XtraLayout.LayoutControl layout;
        private DevExpress.XtraEditors.TextEdit txtStoreName;
        private DevExpress.XtraEditors.MemoEdit memAddress;
        private DevExpress.XtraEditors.TextEdit txtPhone;
        private DevExpress.XtraEditors.TextEdit txtEmail;
        private DevExpress.XtraEditors.TextEdit txtTaxIdentifier;
        private DevExpress.XtraEditors.TextEdit txtCurrencyCode;
        private DevExpress.XtraEditors.TextEdit txtTimeZone;
        private DevExpress.XtraEditors.MemoEdit memReceiptFooter;
        private DevExpress.XtraEditors.CheckEdit chkAllowNegativeStock;
        private DevExpress.XtraEditors.TextEdit txtTaxName;
        private DevExpress.XtraEditors.SpinEdit spnTaxRate;
        private DevExpress.XtraEditors.CheckEdit chkTaxInclusive;
        private DevExpress.XtraEditors.TextEdit txtRegisterCode;
        private DevExpress.XtraEditors.TextEdit txtRegisterName;
        private DevExpress.XtraEditors.ComboBoxEdit txtPrinterName;
        private DevExpress.XtraEditors.SimpleButton btnRefreshPrinters;
        private DevExpress.XtraEditors.SimpleButton btnTestPrinter;
        private DevExpress.XtraEditors.TextEdit txtReceiptPrefix;
        private DevExpress.XtraEditors.SpinEdit spnNextReceiptNumber;
        private DevExpress.XtraEditors.SpinEdit spnMoneyDecimalPlaces;
        private DevExpress.XtraEditors.SimpleButton btnSave;
        private DevExpress.XtraEditors.SimpleButton btnCancel;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.layout = new DevExpress.XtraLayout.LayoutControl();
            this.txtStoreName = new DevExpress.XtraEditors.TextEdit();
            this.memAddress = new DevExpress.XtraEditors.MemoEdit();
            this.txtPhone = new DevExpress.XtraEditors.TextEdit();
            this.txtEmail = new DevExpress.XtraEditors.TextEdit();
            this.txtTaxIdentifier = new DevExpress.XtraEditors.TextEdit();
            this.txtCurrencyCode = new DevExpress.XtraEditors.TextEdit();
            this.txtTimeZone = new DevExpress.XtraEditors.TextEdit();
            this.memReceiptFooter = new DevExpress.XtraEditors.MemoEdit();
            this.chkAllowNegativeStock = new DevExpress.XtraEditors.CheckEdit();
            this.txtTaxName = new DevExpress.XtraEditors.TextEdit();
            this.spnTaxRate = new DevExpress.XtraEditors.SpinEdit();
            this.chkTaxInclusive = new DevExpress.XtraEditors.CheckEdit();
            this.txtRegisterCode = new DevExpress.XtraEditors.TextEdit();
            this.txtRegisterName = new DevExpress.XtraEditors.TextEdit();
            this.txtPrinterName = new DevExpress.XtraEditors.ComboBoxEdit();
            this.btnRefreshPrinters = new DevExpress.XtraEditors.SimpleButton();
            this.btnTestPrinter = new DevExpress.XtraEditors.SimpleButton();
            this.txtReceiptPrefix = new DevExpress.XtraEditors.TextEdit();
            this.spnNextReceiptNumber = new DevExpress.XtraEditors.SpinEdit();
            this.spnMoneyDecimalPlaces = new DevExpress.XtraEditors.SpinEdit();
            this.btnSave = new DevExpress.XtraEditors.SimpleButton();
            this.btnCancel = new DevExpress.XtraEditors.SimpleButton();
            var root = new DevExpress.XtraLayout.LayoutControlGroup();
            var storeGroup = new DevExpress.XtraLayout.LayoutControlGroup();
            var taxGroup = new DevExpress.XtraLayout.LayoutControlGroup();
            var registerGroup = new DevExpress.XtraLayout.LayoutControlGroup();
            ((System.ComponentModel.ISupportInitialize)(this.layout)).BeginInit();
            this.layout.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(root)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(storeGroup)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(taxGroup)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(registerGroup)).BeginInit();
            this.SuspendLayout();

            this.layout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.layout.Controls.AddRange(new System.Windows.Forms.Control[] { this.txtStoreName, this.memAddress, this.txtPhone, this.txtEmail, this.txtTaxIdentifier, this.txtCurrencyCode, this.spnMoneyDecimalPlaces, this.txtTimeZone, this.memReceiptFooter, this.chkAllowNegativeStock, this.txtTaxName, this.spnTaxRate, this.chkTaxInclusive, this.txtRegisterCode, this.txtRegisterName, this.txtPrinterName, this.btnRefreshPrinters, this.btnTestPrinter, this.txtReceiptPrefix, this.spnNextReceiptNumber, this.btnSave, this.btnCancel });
            this.layout.Root = root;
            this.layout.Name = "layout";
            root.EnableIndentsWithoutBorders = DevExpress.Utils.DefaultBoolean.True;
            root.GroupBordersVisible = false;

            storeGroup.Text = "Store and receipt";
            taxGroup.Text = "Tax";
            registerGroup.Text = "Register and printer";
            root.Add(storeGroup);
            root.Add(taxGroup);
            root.Add(registerGroup);

            AddItem(storeGroup, "Store name", this.txtStoreName);
            AddItem(storeGroup, "Address", this.memAddress);
            AddItem(storeGroup, "Phone", this.txtPhone);
            AddItem(storeGroup, "Email", this.txtEmail);
            AddItem(storeGroup, "Tax identifier", this.txtTaxIdentifier);
            AddItem(storeGroup, "Currency code", this.txtCurrencyCode);
            AddItem(storeGroup, "Money decimal places", this.spnMoneyDecimalPlaces);
            this.spnMoneyDecimalPlaces.Properties.IsFloatValue = false;
            this.spnMoneyDecimalPlaces.Properties.MinValue = 0m;
            this.spnMoneyDecimalPlaces.Properties.MaxValue = 4m;
            AddItem(storeGroup, "Time zone", this.txtTimeZone);
            AddItem(storeGroup, "Receipt footer", this.memReceiptFooter);
            AddItem(storeGroup, "", this.chkAllowNegativeStock);
            this.chkAllowNegativeStock.Text = "Allow negative stock";

            AddItem(taxGroup, "Tax name", this.txtTaxName);
            AddItem(taxGroup, "Tax rate (0–1)", this.spnTaxRate);
            this.spnTaxRate.Properties.IsFloatValue = true;
            this.spnTaxRate.Properties.Mask.EditMask = "p2";
            this.spnTaxRate.Properties.MaxValue = 1m;
            this.spnTaxRate.Properties.Increment = 0.01m;
            AddItem(taxGroup, "", this.chkTaxInclusive);
            this.chkTaxInclusive.Text = "Prices include tax";

            AddItem(registerGroup, "Register code", this.txtRegisterCode);
            AddItem(registerGroup, "Register name", this.txtRegisterName);
            AddItem(registerGroup, "Receipt prefix", this.txtReceiptPrefix);
            AddItem(registerGroup, "Next receipt number", this.spnNextReceiptNumber);
            this.spnNextReceiptNumber.Properties.IsFloatValue = false;
            this.spnNextReceiptNumber.Properties.MinValue = 1m;
            this.spnNextReceiptNumber.Properties.MaxValue = 999999999999m;
            AddItem(registerGroup, "Printer name", this.txtPrinterName);
            this.btnRefreshPrinters.Text = "Refresh printers";
            this.btnRefreshPrinters.Click += new System.EventHandler(this.btnRefreshPrinters_Click);
            AddItem(registerGroup, "", this.btnRefreshPrinters);
            this.btnTestPrinter.Text = "Print test page";
            this.btnTestPrinter.Click += new System.EventHandler(this.btnTestPrinter_Click);
            AddItem(registerGroup, "", this.btnTestPrinter);
            var buttons = new DevExpress.XtraLayout.LayoutControlGroup { GroupBordersVisible = false };
            buttons.AddItem("", this.btnSave).TextVisible = false;
            buttons.AddItem("", this.btnCancel).TextVisible = false;
            root.Add(buttons);
            this.btnSave.Text = "Save settings";
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            this.btnCancel.Text = "Revert changes";
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);

            this.ClientSize = new System.Drawing.Size(760, 720);
            this.Controls.Add(this.layout);
            this.Name = "frmStoreSettings";
            this.Text = "Store and POS Settings";
            this.Load += new System.EventHandler(this.frmStoreSettings_Load);
            ((System.ComponentModel.ISupportInitialize)(registerGroup)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(taxGroup)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(storeGroup)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(root)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.layout)).EndInit();
            this.layout.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        private static void AddItem(DevExpress.XtraLayout.LayoutControlGroup group, string caption, System.Windows.Forms.Control control)
        {
            var item = group.AddItem(caption, control);
            item.TextVisible = !string.IsNullOrEmpty(caption);
        }
    }
}
