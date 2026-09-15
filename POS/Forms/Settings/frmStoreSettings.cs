using DevExpress.XtraEditors;
using POS.Models.Operations;
using POS.Services.Settings;
using POS.Services;
using POS.Core.Security;
using POS.Common.Enumerations;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Collections.Generic;

namespace POS.Forms.Settings
{
    public partial class frmStoreSettings : XtraForm
    {
        private StoreSettingsDTO _settings;
        private readonly Dictionary<BaseEdit, object> _savedEditorValues = new Dictionary<BaseEdit, object>();
        private readonly StoreSettingsService _service;
        private readonly SimpleButton _previewButton = new SimpleButton { Text = "Preview sample receipt" };
        private readonly ComboBoxEdit _timeZoneEditor = new ComboBoxEdit();
        private readonly DevExpress.XtraGrid.GridControl _taxHistoryGrid =
            new DevExpress.XtraGrid.GridControl();
        private readonly DevExpress.XtraGrid.Views.Grid.GridView _taxHistoryView =
            new DevExpress.XtraGrid.Views.Grid.GridView();

        public frmStoreSettings() : this(new StoreSettingsService())
        {
        }

        public frmStoreSettings(StoreSettingsService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            InitializeComponent();
            var timeZoneItem = layout.GetItemByControl(txtTimeZone);
            layout.Controls.Add(_timeZoneEditor);
            timeZoneItem.Control = _timeZoneEditor;
            layout.Controls.Remove(txtTimeZone);
            txtTimeZone.Dispose();
            _timeZoneEditor.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.DisableTextEditor;
            txtCurrencyCode.Properties.ReadOnly = true;
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            MinimizeBox = false;
            MinimumSize = new System.Drawing.Size(760, 600);
            ClientSize = new System.Drawing.Size(900, 780);
            var closeButton = new SimpleButton
            {
                Text = "Close",
                DialogResult = System.Windows.Forms.DialogResult.Cancel
            };
            layout.Controls.Add(closeButton);
            layout.Root.AddItem("", closeButton).TextVisible = false;
            CancelButton = closeButton;
            layout.Controls.Add(_previewButton);
            layout.Root.AddItem("", _previewButton).TextVisible = false;
            _previewButton.Click += PreviewReceipt_Click;
            UpdateActions();
            var historyGroup = new GroupControl
            {
                Text = "Tax history",
                Dock = System.Windows.Forms.DockStyle.Bottom,
                Height = 190
            };
            _taxHistoryGrid.Dock = System.Windows.Forms.DockStyle.Fill;
            _taxHistoryGrid.MainView = _taxHistoryView;
            _taxHistoryGrid.ViewCollection.Add(_taxHistoryView);
            _taxHistoryView.OptionsBehavior.Editable = false;
            _taxHistoryView.OptionsView.ShowGroupPanel = false;
            historyGroup.Controls.Add(_taxHistoryGrid);
            Controls.Add(historyGroup);
            foreach (var editor in layout.Controls.OfType<BaseEdit>())
                editor.EditValueChanged += (sender, args) => UpdateTitle();
        }

        private bool HasUnsavedChanges => _savedEditorValues.Any(pair => !Equals(pair.Key.EditValue, pair.Value));

        private void RememberSavedValues()
        {
            _savedEditorValues.Clear();
            foreach (var editor in layout.Controls.OfType<BaseEdit>())
                _savedEditorValues.Add(editor, editor.EditValue);
            UpdateTitle();
        }

        private void UpdateTitle()
        {
            Text = "Store and POS Settings" + (HasUnsavedChanges ? " - Unsaved changes" : string.Empty);
        }

        protected override void OnFormClosing(System.Windows.Forms.FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (e.Cancel || !HasUnsavedChanges) return;
            var canSave = _settings != null && AuthorizationService.HasPermission(ResourceCodes.Settings, ClaimActionType.Edit);
            var choice = XtraMessageBox.Show(this,
                canSave ? "Save your settings changes before closing?\nYes: Save\nNo: Discard\nCancel: Keep editing"
                    : "Discard your unsaved settings changes and close?",
                "Unsaved settings",
                canSave ? System.Windows.Forms.MessageBoxButtons.YesNoCancel : System.Windows.Forms.MessageBoxButtons.YesNo,
                System.Windows.Forms.MessageBoxIcon.Question,
                canSave ? System.Windows.Forms.MessageBoxDefaultButton.Button3 : System.Windows.Forms.MessageBoxDefaultButton.Button2);
            e.Cancel = canSave
                ? choice == System.Windows.Forms.DialogResult.Cancel ||
                    (choice == System.Windows.Forms.DialogResult.Yes && !SaveSettings(false))
                : choice != System.Windows.Forms.DialogResult.Yes;
            if (e.Cancel) DialogResult = System.Windows.Forms.DialogResult.None;
        }

        private void frmStoreSettings_Load(object sender, EventArgs e)
        {
            try
            {
                _timeZoneEditor.Properties.Items.AddRange(TimeZoneInfo.GetSystemTimeZones()
                    .Select(zone => zone.Id).ToArray());
                _settings = _service.GetSettings();
                BindSettings();
                btnRefreshPrinters_Click(this, EventArgs.Empty);
                RefreshTaxHistory();
            }
            catch (Exception exception)
            {
                XtraMessageBox.Show(exception.Message, "Unable to load settings", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
            finally { UpdateActions(); }
        }

        private void UpdateActions()
        {
            var loaded = _settings != null;
            var canEdit = loaded && AuthorizationService.HasPermission(ResourceCodes.Settings, ClaimActionType.Edit);
            btnSave.Enabled = canEdit;
            btnTestPrinter.Enabled = canEdit;
            _previewButton.Enabled = loaded;
            btnCancel.Text = loaded ? "Revert changes" : "Reload settings";
        }

        private void PreviewReceipt_Click(object sender, EventArgs e)
        {
            try
            {
                if (_settings == null)
                    throw new InvalidOperationException("Load store settings before previewing a receipt.");
                ReadSettings();
                var content = _service.BuildReceiptPreview(_settings);
                using (var report = new POS.Reports.SettingsReceiptPreview(content))
                using (var viewer = new POS.Reports.frmReportViewer(report))
                {
                    viewer.Text = "Sample receipt preview - current settings";
                    viewer.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
                    viewer.ShowDialog(this);
                }
            }
            catch (Exception exception)
            {
                XtraMessageBox.Show(exception.Message, "Unable to preview receipt",
                    System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
            }
        }

        private void LoadPrinters()
        {
            var selected = txtPrinterName.Text;
            txtPrinterName.Properties.Items.Clear();
            txtPrinterName.Properties.Items.AddRange(_service.GetInstalledPrinters().ToArray());
            txtPrinterName.Text = selected;
        }

        private void btnRefreshPrinters_Click(object sender, EventArgs e)
        {
            try { LoadPrinters(); }
            catch (Exception exception)
            {
                XtraMessageBox.Show(exception.Message, "Printers", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
            }
        }

        private void btnTestPrinter_Click(object sender, EventArgs e)
        {
            try
            {
                ReadSettings();
                var result = _service.PrintTestPage(_settings);
                XtraMessageBox.Show(
                    result.Succeeded ? "Test page sent to the printer." : string.Join(Environment.NewLine, result.Errors),
                    "Printer test",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    result.Succeeded ? System.Windows.Forms.MessageBoxIcon.Information : System.Windows.Forms.MessageBoxIcon.Warning);
            }
            catch (Exception exception)
            {
                XtraMessageBox.Show(exception.Message, "Unable to print test page",
                    System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            SaveSettings(true);
        }

        private bool SaveSettings(bool showSuccess)
        {
            try
            {
                ReadSettings();
                _service.SaveSettings(_settings);
            }
            catch (ValidationException exception)
            {
                XtraMessageBox.Show(exception.Message, "Check the settings", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                return false;
            }
            catch (Exception exception)
            {
                XtraMessageBox.Show(exception.Message, "Unable to save settings", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return false;
            }

            RememberSavedValues();
            try
            {
                _settings = _service.GetSettings();
                BindSettings();
                RefreshTaxHistory();
                if (showSuccess)
                    XtraMessageBox.Show("Store settings saved.", "Settings", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
            }
            catch (Exception exception)
            {
                // The commit succeeded; require fresh generated IDs before another save.
                _settings = null;
                XtraMessageBox.Show("Settings were saved, but could not be reloaded. Reload settings before making further changes.\n\n" + exception.Message,
                    "Settings saved - reload required", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
            }
            finally { UpdateActions(); }
            return true;
        }

        private void RefreshTaxHistory()
        {
            try { LoadTaxHistory(); }
            catch (Exception exception)
            {
                _taxHistoryGrid.DataSource = null;
                XtraMessageBox.Show(exception.Message, "Unable to load tax history",
                    System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
            }
        }

        private void LoadTaxHistory()
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(_settings.TimeZoneId);
            _taxHistoryGrid.DataSource = _service.GetTaxHistory().Select(rate => new
            {
                rate.Name, rate.Rate, rate.IsInclusive, rate.IsActive,
                EffectiveFrom = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(rate.EffectiveFromUtc, DateTimeKind.Utc), zone),
                EffectiveTo = rate.EffectiveToUtc.HasValue
                    ? (DateTime?)TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(rate.EffectiveToUtc.Value, DateTimeKind.Utc), zone)
                    : null
            }).ToList();
            _taxHistoryView.BestFitColumns();
            foreach (var field in new[] { "EffectiveFrom", "EffectiveTo" })
            {
                var column = _taxHistoryView.Columns.ColumnByFieldName(field);
                if (column == null) continue;
                column.Caption = (field == "EffectiveFrom" ? "Effective from" : "Effective to") + " (" + zone.Id + ")";
                column.DisplayFormat.FormatType = DevExpress.Utils.FormatType.DateTime;
                column.DisplayFormat.FormatString = "g";
            }
        }

        private void BindSettings()
        {
            txtStoreName.Text = _settings.StoreName;
            memAddress.Text = _settings.Address;
            txtPhone.Text = _settings.Phone;
            txtEmail.Text = _settings.Email;
            txtTaxIdentifier.Text = _settings.TaxIdentifier;
            txtCurrencyCode.Text = "PHP";
            spnMoneyDecimalPlaces.Value = _settings.MoneyDecimalPlaces;
            _timeZoneEditor.Text = _settings.TimeZoneId;
            memReceiptFooter.Text = _settings.ReceiptFooter;
            chkAllowNegativeStock.Checked = _settings.AllowNegativeStock;
            txtTaxName.Text = _settings.TaxName;
            spnTaxRate.Value = _settings.TaxRate;
            chkTaxInclusive.Checked = _settings.TaxInclusive;
            txtRegisterCode.Text = _settings.RegisterCode;
            txtRegisterName.Text = _settings.RegisterName;
            txtPrinterName.Text = _settings.PrinterName;
            txtReceiptPrefix.Text = _settings.ReceiptPrefix;
            spnNextReceiptNumber.Value = _settings.NextReceiptNumber;
            RememberSavedValues();
        }

        private void ReadSettings()
        {
            if (_settings == null)
                throw new InvalidOperationException("Reload store settings before continuing.");
            _settings.StoreName = txtStoreName.Text;
            _settings.Address = memAddress.Text;
            _settings.Phone = txtPhone.Text;
            _settings.Email = txtEmail.Text;
            _settings.TaxIdentifier = txtTaxIdentifier.Text;
            _settings.CurrencyCode = txtCurrencyCode.Text;
            _settings.MoneyDecimalPlaces = decimal.ToInt32(spnMoneyDecimalPlaces.Value);
            _settings.TimeZoneId = _timeZoneEditor.Text;
            _settings.ReceiptFooter = memReceiptFooter.Text;
            _settings.AllowNegativeStock = chkAllowNegativeStock.Checked;
            _settings.TaxName = txtTaxName.Text;
            _settings.TaxRate = spnTaxRate.Value;
            _settings.TaxInclusive = chkTaxInclusive.Checked;
            _settings.RegisterCode = txtRegisterCode.Text;
            _settings.RegisterName = txtRegisterName.Text;
            _settings.PrinterName = txtPrinterName.Text;
            _settings.ReceiptPrefix = txtReceiptPrefix.Text;
            _settings.NextReceiptNumber = decimal.ToInt64(spnNextReceiptNumber.Value);
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            if (HasUnsavedChanges && XtraMessageBox.Show(this,
                "Discard your unsaved changes and reload saved settings?", "Revert settings",
                System.Windows.Forms.MessageBoxButtons.YesNo, System.Windows.Forms.MessageBoxIcon.Question,
                System.Windows.Forms.MessageBoxDefaultButton.Button2) != System.Windows.Forms.DialogResult.Yes)
                return;
            try
            {
                _settings = _service.GetSettings();
                BindSettings();
                RefreshTaxHistory();
            }
            catch (Exception exception)
            {
                XtraMessageBox.Show(exception.Message, "Unable to reload settings",
                    System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
            finally { UpdateActions(); }
        }
    }
}
