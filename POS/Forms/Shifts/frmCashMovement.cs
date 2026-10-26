using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraEditors.DXErrorProvider;
using DevExpress.XtraLayout;
using POS.Common.Enumerations;
using POS.Core;
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
    public sealed class frmCashMovement : XtraForm
    {
        private readonly CashierShiftService _service;
        private readonly int _cashierShiftId;
        private readonly bool _canPost;
        private CashierShiftDTO _shift;
        private Guid _requestId = Guid.NewGuid();
        private bool _knownRejection;
        private bool _uncertainOutcome;
        private bool _loaded;
        private bool _busy;

        private readonly TextEdit txtRegister = ReadOnlyText();
        private readonly TextEdit txtShift = ReadOnlyText();
        private readonly TextEdit txtExpectedCash = ReadOnlyText();
        private readonly RadioGroup rgMovementType = new RadioGroup();
        private readonly SpinEdit spnAmount = new SpinEdit();
        private readonly MemoEdit memReason = new MemoEdit();
        private readonly LabelControl lblSummary = new LabelControl { AutoSizeMode = LabelAutoSizeMode.None };
        private readonly LabelControl lblState = new LabelControl { AutoSizeMode = LabelAutoSizeMode.None };
        private readonly SimpleButton btnPost = new SimpleButton { Text = "Post Cash Movement" };
        private readonly SimpleButton btnCancel = new SimpleButton { Text = "Cancel" };
        private readonly DXErrorProvider dxError = new DXErrorProvider();

        public frmCashMovement(CashierShiftService service, IAuthorizationService authorization, CashierShiftDTO shift)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            if (authorization == null) throw new ArgumentNullException(nameof(authorization));
            _canPost = authorization.HasPermission(ResourceCodes.Shifts, ClaimActionType.Edit);
            _shift = shift ?? throw new ArgumentNullException(nameof(shift));
            if (shift.Id <= 0) throw new ArgumentException("An existing cashier shift is required.", nameof(shift));
            _cashierShiftId = shift.Id;
            BuildUi();
        }

        public long MovementId { get; private set; }

        private void BuildUi()
        {
            Name = "frmCashMovement";
            Text = "Cash In / Cash Out";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new System.Drawing.Size(560, 410);

            rgMovementType.Properties.Items.AddRange(new[]
            {
                new RadioGroupItem(true, "Cash In"),
                new RadioGroupItem(false, "Cash Out")
            });
            rgMovementType.SelectedIndex = 0;
            spnAmount.Properties.IsFloatValue = true;
            spnAmount.Properties.Mask.EditMask = "n2";
            spnAmount.Properties.Mask.UseMaskAsDisplayFormat = true;
            spnAmount.Properties.MinValue = 0m;
            spnAmount.Properties.MaxValue = 9999999999999999.99m;
            memReason.Properties.MaxLength = 250;
            lblSummary.Text = "Enter an amount and reason to preview the movement.";
            lblState.Text = "Loading the current shift...";

            var layout = new LayoutControl { Dock = DockStyle.Fill };
            Controls.Add(layout);
            layout.Controls.AddRange(new Control[]
            {
                txtRegister, txtShift, txtExpectedCash, rgMovementType,
                spnAmount, memReason, lblSummary, lblState, btnPost, btnCancel
            });
            var root = new LayoutControlGroup { EnableIndentsWithoutBorders = DevExpress.Utils.DefaultBoolean.True };
            layout.Root = root;
            root.AddItem("Register", txtRegister);
            root.AddItem("Shift", txtShift);
            root.AddItem("Expected cash", txtExpectedCash);
            root.AddItem("Movement", rgMovementType);
            root.AddItem("Amount (PHP)", spnAmount);
            var reasonItem = root.AddItem("Reason", memReason);
            reasonItem.MinSize = new System.Drawing.Size(0, 85);
            var summaryItem = root.AddItem(string.Empty, lblSummary);
            summaryItem.TextVisible = false;
            var stateItem = root.AddItem(string.Empty, lblState);
            stateItem.TextVisible = false;
            var buttons = new LayoutControlGroup { GroupBordersVisible = false };
            buttons.AddItem(string.Empty, btnPost).TextVisible = false;
            buttons.AddItem(string.Empty, btnCancel).TextVisible = false;
            root.Add(buttons);

            dxError.ContainerControl = this;
            AcceptButton = btnPost;
            CancelButton = btnCancel;
            btnCancel.DialogResult = DialogResult.Cancel;
            btnPost.ToolTip = _canPost ? string.Empty : "Shifts/Edit permission is required.";
            btnPost.Click += async (sender, args) => await PostAsync();
            rgMovementType.SelectedIndexChanged += (sender, args) => InputChanged();
            spnAmount.EditValueChanged += (sender, args) => InputChanged();
            memReason.EditValueChanged += (sender, args) => InputChanged();
            Shown += async (sender, args) => await ReloadShiftAsync();
        }

        private async Task ReloadShiftAsync()
        {
            if (_busy) return;
            try
            {
                _loaded = false;
                SetBusy(true, "Loading the current shift...");
                _shift = await Task.Run(() => _service.GetDetails(_cashierShiftId));
                if (!string.Equals(_shift.Status, "Open", StringComparison.OrdinalIgnoreCase))
                    throw new ValidationException("Cash movements can only be posted to an open shift.");
                if (!string.Equals(_shift.CashierUserId, CurrentUser.UserId, StringComparison.Ordinal))
                    throw new UnauthorizedAccessException("Cash movements can only be posted to your own open shift.");
                txtRegister.Text = string.Format("{0} — {1}", _shift.RegisterCode, _shift.RegisterName);
                txtShift.Text = string.Format("#{0} • {1}", _shift.Id, _shift.CashierName);
                txtExpectedCash.Text = Money(_shift.ExpectedCash);
                _loaded = true;
                lblState.Text = _canPost
                    ? "Shift loaded. Posting will update its expected cash."
                    : "You can review this shift, but Shifts/Edit permission is required to post cash.";
                UpdateSummary();
            }
            catch (Exception exception)
            {
                _loaded = false;
                btnPost.Enabled = false;
                lblState.Text = SafeMessage(exception);
                ShowError(exception, "Cash Movement");
            }
            finally
            {
                if (!IsDisposed) SetBusy(false, lblState.Text);
            }
        }

        private async Task PostAsync()
        {
            if (_busy || !ValidateInput()) return;
            var request = new PostCashMovementDTO
            {
                RequestId = _requestId,
                CashierShiftId = _shift.Id,
                ShiftRevision = _shift.Revision,
                IsCashIn = IsCashIn,
                Amount = decimal.Round(spnAmount.Value, 2),
                Reason = memReason.Text.Trim()
            };
            var action = request.IsCashIn ? "cash-in" : "cash-out";
            var answer = XtraMessageBox.Show(
                this,
                string.Format("Post {0} of {1}?\n\nReason: {2}", action, Money(request.Amount), request.Reason),
                "Confirm Cash Movement",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (answer != DialogResult.Yes) return;

            try
            {
                SetBusy(true, "Posting cash movement...");
                MovementId = await Task.Run(() => _service.PostCashMovement(request));
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (ValidationException exception)
            {
                _knownRejection = true;
                lblState.Text = exception.Message;
                ShowError(exception, "Post Cash Movement");
            }
            catch (UnauthorizedAccessException exception)
            {
                _knownRejection = true;
                lblState.Text = exception.Message;
                ShowError(exception, "Post Cash Movement");
            }
            catch (Exception exception)
            {
                // The outcome may be uncertain. Keep the request ID so an identical retry is idempotent.
                _uncertainOutcome = true;
                lblState.Text = "The result is uncertain. Retry the same movement, or cancel and reopen the shift before editing.";
                ShowError(exception, "Post Cash Movement");
            }
            finally
            {
                if (!IsDisposed) SetBusy(false, lblState.Text);
            }
        }

        private bool ValidateInput()
        {
            dxError.ClearErrors();
            if (_shift == null || string.IsNullOrWhiteSpace(_shift.Revision))
                dxError.SetError(txtShift, "Reload the shift before posting.");
            if (spnAmount.Value <= 0m)
                dxError.SetError(spnAmount, "Enter a positive amount.");
            if (string.IsNullOrWhiteSpace(memReason.Text))
                dxError.SetError(memReason, "A reason is required.");
            if (!IsCashIn && _shift != null && spnAmount.Value > _shift.ExpectedCash)
                dxError.SetError(spnAmount, "Cash-out cannot exceed expected cash.");
            return !dxError.HasErrors;
        }

        private void InputChanged()
        {
            if (_knownRejection)
            {
                _requestId = Guid.NewGuid();
                _knownRejection = false;
            }
            dxError.ClearErrors();
            UpdateSummary();
        }

        private void UpdateSummary()
        {
            if (_shift == null) return;
            var amount = spnAmount.Value;
            var resulting = IsCashIn ? _shift.ExpectedCash + amount : _shift.ExpectedCash - amount;
            lblSummary.Text = string.Format("{0}: {1}  •  Resulting expected cash: {2}",
                IsCashIn ? "Cash in" : "Cash out", Money(amount), Money(resulting));
        }

        private bool IsCashIn => rgMovementType.EditValue == null || Convert.ToBoolean(rgMovementType.EditValue);

        private void SetBusy(bool busy, string state)
        {
            _busy = busy;
            rgMovementType.Enabled = !busy && !_uncertainOutcome;
            spnAmount.Enabled = !busy && !_uncertainOutcome;
            memReason.Enabled = !busy && !_uncertainOutcome;
            btnPost.Text = _uncertainOutcome ? "Retry Same Movement" : "Post Cash Movement";
            btnPost.Enabled = !busy && _canPost && _loaded && _shift != null && string.Equals(_shift.Status, "Open", StringComparison.OrdinalIgnoreCase)
                && string.Equals(_shift.CashierUserId, CurrentUser.UserId, StringComparison.Ordinal);
            btnCancel.Enabled = !busy;
            lblState.Text = state;
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

        private static string SafeMessage(Exception exception)
        {
            return exception is ValidationException || exception is UnauthorizedAccessException || exception is InvalidOperationException
                ? exception.Message
                : "The cash movement could not be completed. Please try again.";
        }

        private static void ShowError(Exception exception, string caption)
        {
            XtraMessageBox.Show(SafeMessage(exception), caption, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
