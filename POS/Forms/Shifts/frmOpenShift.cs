using DevExpress.XtraEditors;
using DevExpress.XtraEditors.DXErrorProvider;
using DevExpress.XtraGrid.Views.Grid;
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
    public sealed class frmOpenShift : XtraForm
    {
        private readonly CashierShiftService _service;
        private readonly ICurrentUser _currentUser;
        private readonly bool _canOpen;
        private Guid _requestId = Guid.NewGuid();
        private OpenDetails _lastSubmittedDetails;
        private bool _knownRejection;
        private bool _uncertainOutcome;
        private bool _loaded;
        private bool _hasActiveRegisters;
        private bool _busy;

        private readonly SearchLookUpEdit slueRegister = new SearchLookUpEdit { Name = "slueRegister" };
        private readonly SpinEdit spnOpeningCash = new SpinEdit { Name = "spnOpeningCash" };
        private readonly TextEdit txtCurrentUser = ReadOnlyText("txtCurrentUser");
        private readonly TextEdit txtCurrentRole = ReadOnlyText("txtCurrentRole");
        private readonly LabelControl lblState = new LabelControl
        {
            Name = "lblState",
            AutoSizeMode = LabelAutoSizeMode.None
        };
        private readonly SimpleButton btnOpen = new SimpleButton { Name = "btnOpen", Text = "Open Shift" };
        private readonly SimpleButton btnCancel = new SimpleButton { Name = "btnCancel", Text = "Cancel" };
        private readonly DXErrorProvider dxError = new DXErrorProvider();

        public frmOpenShift(CashierShiftService service, ICurrentUser currentUser,
            IAuthorizationService authorization)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            if (authorization == null) throw new ArgumentNullException(nameof(authorization));
            _canOpen = authorization.HasPermission(ResourceCodes.Shifts, ClaimActionType.Add);
            BuildUi();
        }

        public int CashierShiftId { get; private set; }

        private void BuildUi()
        {
            Name = "frmOpenShift";
            Text = "Open Cashier Shift";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new System.Drawing.Size(560, 330);

            var registerView = new GridView();
            registerView.OptionsBehavior.Editable = false;
            registerView.OptionsView.ShowGroupPanel = false;
            registerView.Columns.AddVisible(nameof(RegisterStationDTO.Code), "Code");
            registerView.Columns.AddVisible(nameof(RegisterStationDTO.Name), "Register");
            slueRegister.Properties.PopupView = registerView;
            slueRegister.Properties.DisplayMember = nameof(RegisterStationDTO.Name);
            slueRegister.Properties.ValueMember = nameof(RegisterStationDTO.Id);
            slueRegister.Properties.NullText = "Select an active register";

            spnOpeningCash.Properties.IsFloatValue = true;
            spnOpeningCash.Properties.Mask.EditMask = "n2";
            spnOpeningCash.Properties.Mask.UseMaskAsDisplayFormat = true;
            spnOpeningCash.Properties.MinValue = 0m;
            spnOpeningCash.Properties.MaxValue = 9999999999999999.99m;

            txtCurrentUser.Text = string.IsNullOrWhiteSpace(_currentUser.Username)
                ? "Unavailable"
                : _currentUser.Username;
            txtCurrentRole.Text = string.IsNullOrWhiteSpace(_currentUser.RoleName)
                ? "Unavailable"
                : _currentUser.RoleName;
            lblState.Text = "Loading active registers...";

            var layout = new LayoutControl { Dock = DockStyle.Fill };
            Controls.Add(layout);
            layout.Controls.AddRange(new Control[]
            {
                slueRegister, spnOpeningCash, txtCurrentUser, txtCurrentRole,
                lblState, btnOpen, btnCancel
            });
            var root = new LayoutControlGroup
            {
                EnableIndentsWithoutBorders = DevExpress.Utils.DefaultBoolean.True
            };
            layout.Root = root;
            root.AddItem("Register", slueRegister);
            root.AddItem("Opening cash (PHP)", spnOpeningCash);
            root.AddItem("Current user", txtCurrentUser);
            root.AddItem("Role", txtCurrentRole);
            var stateItem = root.AddItem(string.Empty, lblState);
            stateItem.TextVisible = false;
            stateItem.MinSize = new System.Drawing.Size(0, 48);
            var buttons = new LayoutControlGroup { GroupBordersVisible = false };
            buttons.AddItem(string.Empty, btnOpen).TextVisible = false;
            buttons.AddItem(string.Empty, btnCancel).TextVisible = false;
            root.Add(buttons);

            dxError.ContainerControl = this;
            AcceptButton = btnOpen;
            CancelButton = btnCancel;
            btnCancel.DialogResult = DialogResult.Cancel;
            btnOpen.ToolTip = _canOpen ? string.Empty : "Shifts/Add permission is required.";
            btnOpen.Click += async (sender, args) => await OpenShiftAsync();
            slueRegister.EditValueChanged += (sender, args) => InputChanged();
            spnOpeningCash.EditValueChanged += (sender, args) => InputChanged();
            Shown += async (sender, args) => await LoadRegistersAsync();
            FormClosing += FormClosingHandler;
        }

        private async Task LoadRegistersAsync()
        {
            if (_busy || _loaded) return;
            try
            {
                _hasActiveRegisters = false;
                SetBusy(true, "Loading active registers...");
                var registers = await Task.Run(() => _service.GetActiveRegisters());
                slueRegister.Properties.DataSource = registers;
                _hasActiveRegisters = registers.Count > 0;
                _loaded = true;
                if (registers.Count == 0)
                    lblState.Text = "No active registers are available. Ask an administrator to activate one.";
                else if (!_canOpen)
                    lblState.Text = "You can review this dialog, but Shifts/Add permission is required to open a shift.";
                else
                    lblState.Text = "Select a register and enter the opening cash amount.";
            }
            catch (Exception exception)
            {
                _loaded = false;
                _hasActiveRegisters = false;
                lblState.Text = SafeMessage(exception);
                ShowError(exception, "Open Cashier Shift");
            }
            finally
            {
                if (!IsDisposed) SetBusy(false, lblState.Text);
            }
        }

        private async Task OpenShiftAsync()
        {
            if (_busy || !_canOpen || !ValidateInput()) return;

            var details = CurrentDetails();
            var request = new OpenCashierShiftDTO
            {
                RequestId = _requestId,
                RegisterStationId = details.RegisterStationId,
                OpeningCash = details.OpeningCash
            };
            var register = slueRegister.Properties.View.GetFocusedRow() as RegisterStationDTO;
            var registerName = register == null
                ? "the selected register"
                : string.Format("{0} - {1}", register.Code, register.Name);
            var answer = XtraMessageBox.Show(
                this,
                string.Format("Open a cashier shift on {0} with {1} opening cash?",
                    registerName, Money(request.OpeningCash)),
                "Confirm Open Shift",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (answer != DialogResult.Yes) return;

            _lastSubmittedDetails = details;
            try
            {
                SetBusy(true, "Opening cashier shift...");
                CashierShiftId = await Task.Run(() => _service.Open(request));
                _uncertainOutcome = false;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (ValidationException exception)
            {
                _uncertainOutcome = RequiresIdenticalRetry(exception);
                _knownRejection = !_uncertainOutcome;
                lblState.Text = _uncertainOutcome
                    ? "The request may have committed concurrently. Retry the same shift-opening request to resolve it."
                    : exception.Message;
                ShowError(exception, "Open Cashier Shift");
            }
            catch (UnauthorizedAccessException exception)
            {
                _knownRejection = true;
                _uncertainOutcome = false;
                lblState.Text = exception.Message;
                ShowError(exception, "Open Cashier Shift");
            }
            catch (Exception exception)
            {
                _uncertainOutcome = true;
                lblState.Text = "The result is uncertain. Retry the same shift-opening request to resolve it.";
                ShowError(exception, "Open Cashier Shift");
            }
            finally
            {
                if (!IsDisposed) SetBusy(false, lblState.Text);
            }
        }

        private bool ValidateInput()
        {
            dxError.ClearErrors();
            int registerId;
            if (slueRegister.EditValue == null ||
                !int.TryParse(Convert.ToString(slueRegister.EditValue, CultureInfo.InvariantCulture), out registerId) ||
                registerId <= 0)
                dxError.SetError(slueRegister, "Select an active register.");

            var cash = spnOpeningCash.Value;
            if (cash < 0m || cash > 9999999999999999.99m || decimal.Round(cash, 2) != cash)
                dxError.SetError(spnOpeningCash,
                    "Opening cash must be nonnegative, use at most two decimal places, and be within the supported range.");
            return !dxError.HasErrors;
        }

        private OpenDetails CurrentDetails()
        {
            return new OpenDetails
            {
                RegisterStationId = Convert.ToInt32(slueRegister.EditValue, CultureInfo.InvariantCulture),
                OpeningCash = spnOpeningCash.Value
            };
        }

        private void InputChanged()
        {
            if (_knownRejection && _lastSubmittedDetails != null &&
                !_lastSubmittedDetails.Equals(CurrentDetailsOrDefault()))
            {
                _requestId = Guid.NewGuid();
                _knownRejection = false;
            }
            dxError.ClearErrors();
        }

        private OpenDetails CurrentDetailsOrDefault()
        {
            int registerId;
            int.TryParse(Convert.ToString(slueRegister.EditValue, CultureInfo.InvariantCulture), out registerId);
            return new OpenDetails { RegisterStationId = registerId, OpeningCash = spnOpeningCash.Value };
        }

        private void SetBusy(bool busy, string state)
        {
            _busy = busy;
            slueRegister.Enabled = !busy && !_uncertainOutcome;
            spnOpeningCash.Enabled = !busy && !_uncertainOutcome;
            btnOpen.Text = _uncertainOutcome ? "Retry Same Request" : "Open Shift";
            btnOpen.Enabled = !busy && _loaded && _hasActiveRegisters && _canOpen;
            btnCancel.Enabled = !busy;
            lblState.Text = state;
        }

        private void FormClosingHandler(object sender, FormClosingEventArgs args)
        {
            if (DialogResult == DialogResult.OK) return;
            if (_busy)
            {
                args.Cancel = true;
                return;
            }
            if (_uncertainOutcome)
            {
                args.Cancel = true;
                XtraMessageBox.Show(this,
                    "The shift-opening result is uncertain. Retry the same request before closing this dialog.",
                    "Open Cashier Shift", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static TextEdit ReadOnlyText(string name)
        {
            var edit = new TextEdit { Name = name };
            edit.Properties.ReadOnly = true;
            edit.TabStop = false;
            return edit;
        }

        private static string Money(decimal value)
        {
            return "PHP " + value.ToString("N2", CultureInfo.CurrentCulture);
        }

        private static string SafeMessage(Exception exception)
        {
            return exception is ValidationException || exception is UnauthorizedAccessException
                ? exception.Message
                : "The shift operation could not be completed. Please try again.";
        }

        private static bool RequiresIdenticalRetry(ValidationException exception)
        {
            return exception != null && exception.Message != null &&
                exception.Message.IndexOf("committed concurrently", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void ShowError(Exception exception, string caption)
        {
            XtraMessageBox.Show(SafeMessage(exception), caption,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private sealed class OpenDetails
        {
            public int RegisterStationId { get; set; }
            public decimal OpeningCash { get; set; }

            public override bool Equals(object obj)
            {
                var other = obj as OpenDetails;
                return other != null && other.RegisterStationId == RegisterStationId &&
                    other.OpeningCash == OpeningCash;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (RegisterStationId * 397) ^ OpeningCash.GetHashCode();
                }
            }
        }
    }
}
