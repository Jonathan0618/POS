using DevExpress.XtraEditors;
using DevExpress.XtraEditors.DXErrorProvider;
using DevExpress.XtraLayout;
using POS.Models.Operations;
using POS.Services.Settings;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Windows.Forms;

namespace POS.Forms.Settings
{
    public sealed class frmRegisterEdit : XtraForm
    {
        private readonly RegisterStationService _service;
        private readonly RegisterStationDTO _model;
        private readonly TextEdit txtCode = new TextEdit();
        private readonly TextEdit txtName = new TextEdit();
        private readonly LookUpEdit luePrinterName = new LookUpEdit();
        private readonly LabelControl lblState = new LabelControl { AutoSizeMode = LabelAutoSizeMode.None };
        private readonly SimpleButton btnSave = new SimpleButton { Text = "Save Register" };
        private readonly SimpleButton btnCancel = new SimpleButton { Text = "Cancel" };
        private readonly DXErrorProvider dxError = new DXErrorProvider();
        private bool _busy;

        public frmRegisterEdit(RegisterStationService service, IReadOnlyList<string> printers, RegisterStationDTO model = null)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _model = model ?? new RegisterStationDTO();
            BuildUi(printers ?? new string[0]);
            BindModel();
        }

        private void BuildUi(IReadOnlyList<string> printers)
        {
            Name = "frmRegisterEdit";
            Text = _model.Id == 0 ? "Add Register" : "Edit Register";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false; MaximizeBox = false;
            ClientSize = new System.Drawing.Size(520, 280);
            txtCode.Properties.MaxLength = 30;
            txtName.Properties.MaxLength = 100;
            luePrinterName.Properties.DataSource = printers;
            luePrinterName.Properties.NullText = "No printer selected";
            var layout = new LayoutControl { Dock = DockStyle.Fill };
            Controls.Add(layout);
            layout.Controls.AddRange(new Control[] { txtCode, txtName, luePrinterName, lblState, btnSave, btnCancel });
            var root = new LayoutControlGroup { EnableIndentsWithoutBorders = DevExpress.Utils.DefaultBoolean.True };
            layout.Root = root;
            root.AddItem("Register code", txtCode);
            root.AddItem("Register name", txtName);
            root.AddItem("Receipt printer", luePrinterName);
            var state = root.AddItem(string.Empty, lblState); state.TextVisible = false; state.MinSize = new System.Drawing.Size(0, 35);
            var buttons = new LayoutControlGroup { GroupBordersVisible = false };
            buttons.AddItem(string.Empty, btnSave).TextVisible = false;
            buttons.AddItem(string.Empty, btnCancel).TextVisible = false;
            root.Add(buttons);
            dxError.ContainerControl = this;
            AcceptButton = btnSave; CancelButton = btnCancel; btnCancel.DialogResult = DialogResult.Cancel;
            btnSave.Click += async (sender, args) => await SaveAsync();
        }

        private void BindModel()
        {
            txtCode.Text = _model.Code;
            txtName.Text = _model.Name;
            luePrinterName.EditValue = _model.PrinterName;
            lblState.Text = _model.Id == 0 ? "New registers start active." : "Activation is managed from the register list.";
        }

        private async System.Threading.Tasks.Task SaveAsync()
        {
            dxError.ClearErrors();
            if (string.IsNullOrWhiteSpace(txtCode.Text)) dxError.SetError(txtCode, "Register code is required.");
            if (string.IsNullOrWhiteSpace(txtName.Text)) dxError.SetError(txtName, "Register name is required.");
            if (dxError.HasErrors || _busy) return;
            try
            {
                SetBusy(true, "Saving register...");
                _model.Code = txtCode.Text.Trim();
                _model.Name = txtName.Text.Trim();
                _model.PrinterName = string.IsNullOrWhiteSpace(Convert.ToString(luePrinterName.EditValue)) ? null : Convert.ToString(luePrinterName.EditValue).Trim();
                await System.Threading.Tasks.Task.Run(() => _service.Save(_model));
                DialogResult = DialogResult.OK; Close();
            }
            catch (Exception exception)
            {
                lblState.Text = SafeMessage(exception);
                XtraMessageBox.Show(this, lblState.Text, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally { if (!IsDisposed) SetBusy(false, lblState.Text); }
        }

        private void SetBusy(bool busy, string state)
        {
            _busy = busy; txtCode.Enabled = !busy; txtName.Enabled = !busy; luePrinterName.Enabled = !busy;
            btnSave.Enabled = !busy; btnCancel.Enabled = !busy; lblState.Text = state; UseWaitCursor = busy;
        }
        private static string SafeMessage(Exception exception) => exception is ValidationException || exception is UnauthorizedAccessException || exception is InvalidOperationException
            ? exception.Message : "The register could not be saved. Please try again.";
    }
}
