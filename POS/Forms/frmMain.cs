using DevExpress.XtraBars;
using DevExpress.XtraBars.Ribbon;
using DevExpress.XtraEditors;
using POS.Core;
using POS.Composition;
using POS.Forms.Security;
using POS.Forms.Settings;
using POS.Reports;
using POS.Services;
using POS.Services.Security;
using POS.Core.Abstractions;
using POS.Core.Security;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Security.Claims;

namespace POS.Forms
{
    public partial class frmMain : DevExpress.XtraBars.Ribbon.RibbonForm
    {
        private readonly ApplicationCompositionRoot _composition;
        private readonly UserService _userService;
        private readonly NavigationPermission _navigationPermission;
        private BarButtonItem _settingsButton;
        private BarButtonItem _registersButton;
        private BarButtonItem _auditButton;
        private BarButtonItem _logoutButton;
        private BarButtonItem _changePasswordButton;
        private BarButtonItem _lockButton;
        private RibbonPageGroup _auditNavigationGroup;
        private RibbonPageGroup _sessionNavigationGroup;
        private BarButtonItem _reportsButton;
        public bool LogoutRequested { get; private set; }
        public frmMain() : this(
            new ApplicationCompositionRoot(),
            new UserService(),
            new ClaimsAuthorizationService(new CurrentUserAccessor()))
        {
        }

        internal frmMain(
            ApplicationCompositionRoot composition,
            UserService userService,
            IAuthorizationService authorization)
        {
            _composition = composition ?? throw new ArgumentNullException(nameof(composition));
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _navigationPermission = new NavigationPermission(
                authorization ?? throw new ArgumentNullException(nameof(authorization)));
            InitializeComponent();
            ConfigureSettingsNavigation();
            AddSettingsNavigation();
            AddAuditNavigation();
            AddSessionNavigation();
            AddAssignedNavigation();
            btnRolePermissions.ItemClick += btnRoles_ItemClick;
            ApplyNavigationPermissions();
        }

        private void ConfigureSettingsNavigation()
        {
            ribbonPage7.Name = "rpSettings";
            ribbonPage7.Text = "SETTINGS";

            ribbonPageGroup10.Name = "rpgConfiguration";
            ribbonPageGroup10.Text = "Configuration";

            _auditNavigationGroup = new RibbonPageGroup
            {
                Name = "rpgAudit",
                Text = "Audit"
            };
            _sessionNavigationGroup = new RibbonPageGroup
            {
                Name = "rpgSession",
                Text = "Session"
            };

            ribbonPage7.Groups.Add(_auditNavigationGroup);
            ribbonPage7.Groups.Add(_sessionNavigationGroup);
        }

        private void ApplyNavigationPermissions()
        {
            SetVisible(ResourceCodes.Users, barButtonItem6, barButtonItem24, btnUsers, _registersButton);
            SetVisible(ResourceCodes.Roles, btnRoles, btnRolePermissions);
            SetVisible(ResourceCodes.Modules, btnModules);
            SetVisible(ResourceCodes.Settings, barButtonItem5, _settingsButton);
            SetVisible(ResourceCodes.Audit, _auditButton);
            SetVisible(ResourceCodes.Dashboard, barButtonItem1);
            SetVisible(ResourceCodes.Returns, barButtonItem10);
            SetVisible(ResourceCodes.Reports, _reportsButton);
            SetVisible(ResourceCodes.Products,
                barButtonItem12, barButtonItem13, barButtonItem19, barButtonItem22);
            SetVisible(ResourceCodes.Inventory,
                barButtonItem3, barButtonItem14, barButtonItem15, barButtonItem16,
                barButtonItem17, barButtonItem18, barButtonItem20, barButtonItem21,
                barButtonItem23);
            SetVisible(ResourceCodes.Sales,
                barButtonItem4, barButtonItem7, barButtonItem8, barButtonItem9,
                barButtonItem10, barButtonItem11);
        }

        private void AddAssignedNavigation()
        {
            ribbonPageGroup4.Name = "rpgOverview";
            ribbonPageGroup4.Text = "Overview";
            ribbonPageGroup5.Name = "rpgAfterSale";
            ribbonPageGroup5.Text = "Returns";
            ribbonPageGroup8.Name = "rpgReports";
            ribbonPageGroup8.Text = "Management Reports";

            barButtonItem1.Name = "bbiDashboard";
            barButtonItem1.Caption = "Dashboard";
            barButtonItem10.Name = "bbiReturns";
            barButtonItem10.Caption = "Returns / Refunds";
            barButtonItem1.ItemClick += (sender, args) => ExecuteNavigation(() => ShowWorkspace(_composition.CreateDashboardControl()));
            barButtonItem10.ItemClick += (sender, args) => ExecuteNavigation(() => ShowWorkspace(_composition.CreateReturnsControl()));

            _reportsButton = new BarButtonItem { Caption = "Reports", Id = 1102, Name = "bbiReports" };
            _reportsButton.ItemClick += (sender, args) => ExecuteNavigation(() => ShowWorkspace(_composition.CreateReportsControl()));
            ribbon.Items.Add(_reportsButton);
            ribbonPageGroup8.ItemLinks.Add(_reportsButton);
        }

        private void SetVisible(string resource, params BarItem[] items)
        {
            var visibility = _navigationPermission.CanOpen(resource)
                ? BarItemVisibility.Always
                : BarItemVisibility.Never;
            foreach (var item in items.Where(x => x != null))
                item.Visibility = visibility;
        }

        private void AddSessionNavigation()
        {
            _lockButton = new BarButtonItem
            {
                Caption = "Lock Register",
                Id = 1003,
                Name = "btnLockRegister"
            };
            _lockButton.ItemClick += (sender, args) =>
            {
                ExecuteNavigation(() =>
                {
                    var username = CurrentUser.Username;
                    _userService.LockSession();
                    using (var form = _composition.CreateLockScreen(username))
                    {
                        if (form.ShowDialog(this) == System.Windows.Forms.DialogResult.OK)
                            return;
                    }

                    LogoutRequested = true;
                    Close();
                });
            };

            _changePasswordButton = new BarButtonItem
            {
                Caption = "Change Password",
                Id = 1002,
                Name = "btnChangePassword"
            };
            _changePasswordButton.ItemClick += (sender, args) =>
            {
                ExecuteNavigation(() =>
                    ShowOwnedDialog(_composition.CreateChangePasswordForm()));
            };

            _logoutButton = new BarButtonItem
            {
                Caption = "Log Out",
                Id = 1001,
                Name = "btnLogout"
            };
            _logoutButton.ItemClick += (sender, args) =>
            {
                var answer = System.Windows.Forms.MessageBox.Show(
                    "Log out of the current session?",
                    "Log Out",
                    System.Windows.Forms.MessageBoxButtons.YesNo,
                    System.Windows.Forms.MessageBoxIcon.Question);
                if (answer != System.Windows.Forms.DialogResult.Yes)
                    return;

                _userService.Logout();
                LogoutRequested = true;
                Close();
            };
            ribbon.Items.Add(_lockButton);
            ribbon.Items.Add(_changePasswordButton);
            ribbon.Items.Add(_logoutButton);
            _sessionNavigationGroup.ItemLinks.Add(_lockButton);
            _sessionNavigationGroup.ItemLinks.Add(_changePasswordButton);
            _sessionNavigationGroup.ItemLinks.Add(_logoutButton);
        }

        private void AddAuditNavigation()
        {
            _auditButton = new BarButtonItem { Caption = "Audit Log", Id = 1004, Name = "btnAuditLog" };
            _auditButton.ItemClick += (sender, args) =>
            {
                ExecuteNavigation(() => ShowWorkspace(_composition.CreateAuditControl()));
            };
            ribbon.Items.Add(_auditButton);
            _auditNavigationGroup.ItemLinks.Add(_auditButton);
        }

        private void AddSettingsNavigation()
        {
            _settingsButton = new BarButtonItem
            {
                Caption = "Store Settings",
                Id = 1000,
                Name = "btnStoreSettings"
            };
            _settingsButton.ItemClick += (sender, args) =>
            {
                ExecuteNavigation(() =>
                    ShowOwnedDialog(_composition.CreateStoreSettingsForm()));
            };
            _registersButton = new BarButtonItem
            {
                Caption = "Register Employees",
                Id = 1103,
                Name = "bbiRegisters"
            };
            _registersButton.ItemClick += (sender, args) =>
            {
                ExecuteNavigation(() => ShowWorkspace(_composition.CreateRegistersControl()));
            };
            ribbon.Items.Add(_settingsButton);
            ribbon.Items.Add(_registersButton);
            ribbonPageGroup10.ItemLinks.Add(_settingsButton);
            ribbonPageGroup9.ItemLinks.Add(_registersButton);
        }

        private void frmMain_Load(object sender, EventArgs e)
        {
        }

        private void barButtonItem13_ItemClick(object sender, ItemClickEventArgs e)
        {
            var frmCategory = _composition.CreateCategoryForm();
            ShowOwnedDialog(frmCategory);
        }

        private void barButtonItem12_ItemClick(object sender, ItemClickEventArgs e)
        {
            ClearMainPanel();

            var uc = _composition.CreateProductsControl();
            uc.Dock = System.Windows.Forms.DockStyle.Fill;
            pnlMain.Controls.Add(uc);
            uc.ShowStockAlert();
        }

        private void barButtonItem19_ItemClick(object sender, ItemClickEventArgs e)
        {

            ClearMainPanel();

            var uc = _composition.CreateProductsControl();
            uc.Dock = System.Windows.Forms.DockStyle.Fill;
            pnlMain.Controls.Add(uc);
            uc.ShowStockAlert();
        }

        private void barButtonItem4_ItemClick_1(object sender, ItemClickEventArgs e)
        {
            var rpt = new rptReceipt();
            var frm = new frmReportViewer(rpt);
            frm.ShowDialog();
        }

        private void barButtonItem22_ItemClick(object sender, ItemClickEventArgs e)
        {
            var frmCategory = _composition.CreateCategoryForm();
            ShowOwnedDialog(frmCategory);
        }

        private void barButtonItem12_ItemClick_1(object sender, ItemClickEventArgs e)
        {

            ClearMainPanel();

            var uc = _composition.CreateProductsControl();
            uc.Dock = System.Windows.Forms.DockStyle.Fill;
            pnlMain.Controls.Add(uc);
            uc.ShowStockAlert();
        }

        private void barButtonItem13_ItemClick_1(object sender, ItemClickEventArgs e)
        {
            var frmCategory = _composition.CreateCategoryForm();
            ShowOwnedDialog(frmCategory);
        }

        private void btnUsers_ItemClick(object sender, ItemClickEventArgs e)
        {
            ShowUsersControl();
        }

        private void btnRoles_ItemClick(object sender, ItemClickEventArgs e)
        {
            ExecuteNavigation(() => ShowWorkspace(_composition.CreateRolesControl()));
        }

        private void btnModules_ItemClick(object sender, ItemClickEventArgs e)
        {
            ExecuteNavigation(() => ShowWorkspace(_composition.CreateModulesControl()));
        }

        private void barButtonItem24_ItemClick(object sender, ItemClickEventArgs e)
        {
            ShowUsersControl();
        }

        private void barButtonItem17_ItemClick(object sender, ItemClickEventArgs e)
        {
            ClearMainPanel();

            var uc = _composition.CreateStockControl();
            uc.Dock = System.Windows.Forms.DockStyle.Fill;
            pnlMain.Controls.Add(uc);
        }

        private void ClearMainPanel()
        {
            while (pnlMain.Controls.Count > 0)
            {
                var control = pnlMain.Controls[0];
                pnlMain.Controls.RemoveAt(0);
                control.Dispose();
            }
        }

        private void ShowUsersControl()
        {
            ShowWorkspace(_composition.CreateUsersControl());
        }

        private void ShowWorkspace(System.Windows.Forms.Control control)
        {
            if (control == null)
                throw new InvalidOperationException("The requested workspace is unavailable.");

            ClearMainPanel();
            control.Dock = System.Windows.Forms.DockStyle.Fill;
            pnlMain.Controls.Add(control);
        }

        private void ShowOwnedDialog(System.Windows.Forms.Form form)
        {
            if (form == null)
                throw new InvalidOperationException("The requested window is unavailable.");

            using (form) form.ShowDialog(this);
        }

        private void ExecuteNavigation(Action action)
        {
            try
            {
                action();
            }
            catch (Exception)
            {
                XtraMessageBox.Show(
                    this,
                    "The requested screen could not be opened. Please try again.",
                    "Navigation",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Error);
            }
        }
    }
}
