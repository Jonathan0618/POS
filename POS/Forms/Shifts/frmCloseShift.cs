using DevExpress.XtraEditors;
using DevExpress.XtraEditors.DXErrorProvider;
using DevExpress.XtraLayout;
using POS.Common.Enumerations;
using POS.Core.Abstractions;
using POS.Core.Security;
using POS.Models.Operations;
using POS.Services.Shifts;
using System;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace POS.Forms.Shifts
{
    public sealed class frmCloseShift : AuthorizedForm
    {
        protected override string AuthorizationResource => ResourceCodes.Shifts;

        private readonly CashierShiftService _service;
        private readonly int _cashierShiftId;
        private readonly bool _canClose;
        private readonly bool _canApproveVariance;
        private CashierShiftDTO _shift;
        private Guid _requestId = Guid.NewGuid();
        private bool _knownRejection;
        private bool _uncertainOutcome;
        private bool _loaded;
        private bool _busy;

        private readonly TextEdit txtRegister = ReadOnlyText();
        private readonly TextEdit txtShift = ReadOnlyText();
        private readonly TextEdit txtOpeningCash = ReadOnlyText();
        private readonly TextEdit txtExpectedCash = ReadOnlyText();
        private readonly SpinEdit spnCountedCash = new SpinEdit();
        private readonly LabelControl lblVariance = new LabelControl { AutoSizeMode = LabelAutoSizeMode.None };
        private readonly LabelControl lblState = new LabelControl { AutoSizeMode = LabelAutoSizeMode.None };
        private readonly SimpleButton btnCloseShift = new SimpleButton { Text = "Close Shift" };
        private readonly SimpleButton btnCancel = new SimpleButton { Text = "Cancel" };
        private readonly DXErrorProvider dxError = new DXErrorProvider();

        public frmCloseShift(CashierShiftService service, IAuthorizationService authorization, CashierShiftDTO shift)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            if (authorization == null) throw new ArgumentNullException(nameof(authorization));
            _canClose = authorization.HasPermission(ResourceCodes.Shifts, ClaimActionType.Edit);
            _canApproveVariance = authorization.HasPermission(ResourceCodes.Shifts, ClaimActionType.Delete);
            _shift = shift ?? throw new ArgumentNullException(nameof(shift));
            if (shift.Id <= 0) throw new ArgumentException("An existing cashier shift is required.", nameof(shift));
            _cashierShiftId = shift.Id;
            BuildUi();
        }

        public int CashierShiftId => _cashierShiftId;

        private void BuildUi()
        {
            Name = "frmCloseShift";
            Text = "Close Cashier Shift";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new System.Drawing.Size(560, 385);

            spnCountedCash.Properties.IsFloatValue = true;
            spnCountedCash.Properties.Mask.EditMask = "n2";
            spnCountedCash.Properties.Mask.UseMaskAsDisplayFormat = true;
            spnCountedCash.Properties.MinValue = 0m;
            spnCountedCash.Properties.MaxValue = 9999999999999999.99m;
            lblVariance.Text = "Count the drawer to preview the variance.";
            lblState.Text = "Loading the current shift...";

            var layout = new LayoutControl { Dock = DockStyle.Fill };
            Controls.Add(layout);
            layout.Controls.AddRange(new Control[]
            {
                txtRegister, txtShift, txtOpeningCash, txtExpectedCash, spnCountedCash,
                lblVariance, lblState, btnCloseShift, btnCancel
            });
            var root = new LayoutControlGroup { EnableIndentsWithoutBorders = DevExpress.Utils.DefaultBoolean.True };
            layout.Root = root;
            root.AddItem("Register", txtRegister);
            root.AddItem("Shift", txtShift);
            root.AddItem("Opening cash", txtOpeningCash);
            root.AddItem("Expected cash", txtExpectedCash);
            root.AddItem("Counted cash (PHP)", spnCountedCash);
            var varianceItem = root.AddItem(string.Empty, lblVariance);
            varianceItem.TextVisible = false;
            var stateItem = root.AddItem(string.Empty, lblState);
            stateItem.TextVisible = false;
            var buttons = new LayoutControlGroup { GroupBordersVisible = false };
            buttons.AddItem(string.Empty, btnCloseShift).TextVisible = false;
            buttons.AddItem(string.Empty, btnCancel).TextVisible = false;
            root.Add(buttons);

            dxError.ContainerControl = this;
            AcceptButton = btnCloseShift;
            CancelButton = btnCancel;
            btnCancel.DialogResult = DialogResult.Cancel;
            btnCloseShift.Click += async (sender, args) => await CloseShiftAsync();
            spnCountedCash.EditValueChanged += (sender, args) => CountChanged();
            Shown += async (sender, args) => await ReloadShiftAsync(false);
            FormClosing += FormIsClosing;
        }

        private async Task ReloadShiftAsync(bool resetCount, bool showError = true, string refreshFailureMessage = null)
        {
            if (_busy || _uncertainOutcome) return;
            try
            {
                _loaded = false;
                if (resetCount)
                {
                    _requestId = Guid.NewGuid();
                    _knownRejection = false;
                }
                SetBusy(true, "Loading the current shift...");
                _shift = await Task.Run(() => _service.GetDetails(_cashierShiftId));
                txtRegister.Text = string.Format("{0} - {1}", _shift.RegisterCode, _shift.RegisterName);
                txtShift.Text = string.Format("#{0} - {1}", _shift.Id, _shift.CashierName);
                txtOpeningCash.Text = Money(_shift.OpeningCash);
                txtExpectedCash.Text = Money(_shift.ExpectedCash);
                if (resetCount) spnCountedCash.Value = 0m;

                if (!string.Equals(_shift.Status, "Open", StringComparison.OrdinalIgnoreCase))
                {
                    _loaded = false;
                    lblVariance.Text = "The drawer count is no longer editable.";
                    lblState.Text = "This cashier shift is already closed.";
                    return;
                }
                if (string.IsNullOrWhiteSpace(_shift.Revision))
                    throw new ValidationException("Reload the shift before closing it.");

                _requestId = Guid.NewGuid();
                _knownRejection = false;
                _loaded = true;
                lblState.Text = _canClose
                    ? "Shift loaded. Confirm the physical drawer count before closing."
                    : "You can review this shift, but Shifts/Edit permission is required to close it.";
                UpdateVariance();
            }
            catch (Exception exception)
            {
                _loaded = false;
                btnCloseShift.Enabled = false;
                lblState.Text = string.IsNullOrWhiteSpace(refreshFailureMessage)
                    ? SafeMessage(exception)
                    : refreshFailureMessage;
                if (showError) ShowError(exception, "Close Shift");
            }
            finally
            {
                if (!IsDisposed) SetBusy(false, lblState.Text);
            }
        }

        private async Task CloseShiftAsync()
        {
            if (_busy || !ValidateInput()) return;
            var request = new CloseCashierShiftDTO
            {
                RequestId = _requestId,
                CashierShiftId = _shift.Id,
                ShiftRevision = _shift.Revision,
                CountedCash = decimal.Round(spnCountedCash.Value, 2)
            };
            var variance = request.CountedCash - _shift.ExpectedCash;
            if (!_uncertainOutcome)
            {
                var answer = XtraMessageBox.Show(
                    this,
                    string.Format("Close shift #{0}?\n\nExpected: {1}\nCounted: {2}\nVariance: {3}",
                        request.CashierShiftId, Money(_shift.ExpectedCash), Money(request.CountedCash), SignedMoney(variance)),
                    "Confirm Shift Close",
                    MessageBoxButtons.YesNo,
                    variance == 0m ? MessageBoxIcon.Question : MessageBoxIcon.Warning);
                if (answer != DialogResult.Yes) return;
            }

            var reloadAfterConflict = false;
            var terminalConflict = false;
            string terminalConflictMessage = null;
            try
            {
                SetBusy(true, _uncertainOutcome ? "Retrying the same close request..." : "Closing cashier shift...");
                await Task.Run(() => _service.Close(request));
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (ValidationException exception)
            {
                var message = exception.Message ?? string.Empty;
                if (message.IndexOf("committed concurrently", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    SetUncertain(exception);
                }
                else if (message.IndexOf("changed after review", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         message.IndexOf("changed concurrently", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _knownRejection = true;
                    lblState.Text = "The shift changed. Its latest totals will be loaded; recount before closing.";
                    ShowError(exception, "Close Shift");
                    reloadAfterConflict = true;
                }
                else if (message.IndexOf("Only an open cashier shift can be closed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         message.IndexOf("cashier shift no longer exists", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _knownRejection = true;
                    terminalConflict = true;
                    terminalConflictMessage = SafeMessage(exception);
                    lblState.Text = terminalConflictMessage;
                    ShowError(exception, "Close Shift");
                    reloadAfterConflict = true;
                }
                else
                {
                    _knownRejection = true;
                    lblState.Text = SafeMessage(exception);
                    ShowError(exception, "Close Shift");
                }
            }
            catch (UnauthorizedAccessException exception)
            {
                _knownRejection = true;
                lblState.Text = SafeMessage(exception);
                ShowError(exception, "Close Shift");
            }
            catch (Exception exception)
            {
                SetUncertain(exception);
            }
            finally
            {
                if (!IsDisposed) SetBusy(false, lblState.Text);
            }

            if (reloadAfterConflict && !IsDisposed)
                await ReloadShiftAsync(true, !terminalConflict, terminalConflictMessage);
        }

        private bool ValidateInput()
        {
            dxError.ClearErrors();
            if (_shift == null || !_loaded || string.IsNullOrWhiteSpace(_shift.Revision))
                dxError.SetError(txtShift, "Reload the shift before closing.");
            if (spnCountedCash.Value < 0m || spnCountedCash.Value > 9999999999999999.99m ||
                decimal.Round(spnCountedCash.Value, 2) != spnCountedCash.Value)
                dxError.SetError(spnCountedCash, "Enter a nonnegative cash count with at most two decimal places.");
            if (_shift != null && spnCountedCash.Value != _shift.ExpectedCash && !_canApproveVariance)
                dxError.SetError(spnCountedCash, "Shifts/Delete permission is required to approve a nonzero variance.");
            return !dxError.HasErrors;
        }

        private void CountChanged()
        {
            if (_knownRejection)
            {
                _requestId = Guid.NewGuid();
                _knownRejection = false;
            }
            dxError.ClearErrors();
            UpdateVariance();
            SetActionAvailability();
        }

        private void UpdateVariance()
        {
            if (_shift == null) return;
            var variance = spnCountedCash.Value - _shift.ExpectedCash;
            if (variance == 0m)
                lblVariance.Text = "Balanced - no cash variance.";
            else if (variance > 0m)
                lblVariance.Text = string.Format("Over by {0}.", Money(variance));
            else
                lblVariance.Text = string.Format("Short by {0}.", Money(Math.Abs(variance)));

            if (variance != 0m && !_canApproveVariance)
                lblState.Text = "A nonzero variance requires Shifts/Delete approval permission.";
            else if (_loaded && !_busy && !_uncertainOutcome)
                lblState.Text = _canClose
                    ? "Shift loaded. Confirm the physical drawer count before closing."
                    : "You can review this shift, but Shifts/Edit permission is required to close it.";
        }

        private void SetUncertain(Exception exception)
        {
            _uncertainOutcome = true;
            lblState.Text = "The result is uncertain. Retry this identical close request; inputs and dismissal are locked.";
            ShowError(exception, "Close Shift");
        }

        private void SetBusy(bool busy, string state)
        {
            _busy = busy;
            spnCountedCash.Enabled = !busy && !_uncertainOutcome;
            btnCloseShift.Text = _uncertainOutcome ? "Retry Same Close" : "Close Shift";
            SetActionAvailability();
            btnCancel.Enabled = !busy && !_uncertainOutcome;
            ControlBox = !_uncertainOutcome;
            lblState.Text = state;
        }

        private void SetActionAvailability()
        {
            var hasVariance = _shift != null && spnCountedCash.Value != _shift.ExpectedCash;
            btnCloseShift.Enabled = !_busy && _canClose && _loaded && _shift != null &&
                string.Equals(_shift.Status, "Open", StringComparison.OrdinalIgnoreCase) &&
                (!hasVariance || _canApproveVariance);
            btnCloseShift.ToolTip = !_canClose
                ? "Shifts/Edit permission is required."
                : hasVariance && !_canApproveVariance
                    ? "Shifts/Delete permission is required to approve a nonzero variance."
                    : string.Empty;
        }

        private void FormIsClosing(object sender, FormClosingEventArgs e)
        {
            if (!_uncertainOutcome || DialogResult == DialogResult.OK) return;
            e.Cancel = true;
            XtraMessageBox.Show(
                this,
                "The close result is uncertain. Retry the identical request before dismissing this window.",
                "Close Shift",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        private static TextEdit ReadOnlyText()
        {
            var edit = new TextEdit();
            edit.Properties.ReadOnly = true;
            return edit;
        }

        private static string Money(decimal value)
        {
            return "PHP " + value.ToString("N2", CultureInfo.CurrentCulture);
        }

        private static string SignedMoney(decimal value)
        {
            if (value == 0m) return Money(0m);
            return (value > 0m ? "+" : "-") + Money(Math.Abs(value));
        }

        private static string SafeMessage(Exception exception)
        {
            return exception is ValidationException || exception is UnauthorizedAccessException || exception is InvalidOperationException
                ? exception.Message
                : "The cashier shift could not be closed. Please try again.";
        }

        private static void ShowError(Exception exception, string caption)
        {
            XtraMessageBox.Show(SafeMessage(exception), caption, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
