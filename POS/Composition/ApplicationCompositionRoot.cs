using POS.Forms;
using POS.Forms.Security;
using POS.Forms.Settings;
using POS.Forms.Shifts;
using POS.Models.Operations;
using POS.Services;
using POS.Services.Security;
using POS.Services.Settings;
using POS.Services.Shifts;
using POS.Core.Abstractions;
using POS.Services.Reporting;
using POS.Services.Point_of_Sale;
using POS.Data.Context;

namespace POS.Composition
{
    internal sealed class ApplicationCompositionRoot
    {
        public SignIn CreateSignIn()
        {
            var users = new UserService();
            var roles = new RoleService();
            var form = new SignIn(users, roles);
            form.Disposed += (sender, args) =>
            {
                roles.Dispose();
                users.Dispose();
            };
            return form;
        }

        public frmMain CreateMainForm()
        {
            var service = new UserService();
            var authorization = new ClaimsAuthorizationService(new CurrentUserAccessor());
            return Own(new frmMain(this, service, authorization), service);
        }

        public frmCategory CreateCategoryForm()
        {
            var service = new InventoryService();
            return Own(new frmCategory(service), service);
        }

        public frmAddProducts CreateAddProductForm()
        {
            var service = new InventoryService();
            return Own(new frmAddProducts(service), service);
        }

        public ucProducts CreateProductsControl()
        {
            var service = new InventoryService();
            var control = new ucProducts(service, CreateAddProductForm);
            control.Disposed += (sender, args) => service.Dispose();
            return control;
        }

        public ucStock CreateStockControl()
        {
            var service = new InventoryService();
            var control = new ucStock(service);
            control.Disposed += (sender, args) => service.Dispose();
            return control;
        }

        public ucUsers CreateUsersControl()
        {
            var service = new UserService();
            var control = new ucUsers(
                service,
                () => new frmAddUser(service),
                userId => new frmEditUser(userId, service));
            control.Disposed += (sender, args) => service.Dispose();
            return control;
        }

        public frmRoles CreateRolesForm()
        {
            var service = new RoleService();
            return Own(new frmRoles(service), service);
        }

        public ucRolesPermissions CreateRolesControl()
        {
            var service = new RoleService();
            var control = new ucRolesPermissions(service);
            control.Disposed += (sender, args) => service.Dispose();
            return control;
        }

        public frmModules CreateModulesForm()
        {
            var service = new ModuleService();
            return Own(new frmModules(service), service);
        }

        public ucModules CreateModulesControl()
        {
            var service = new ModuleService();
            var control = new ucModules(service);
            control.Disposed += (sender, args) => service.Dispose();
            return control;
        }

        public frmChangePassword CreateChangePasswordForm()
        {
            var service = new UserService();
            return Own(new frmChangePassword(service), service);
        }

        public frmLockScreen CreateLockScreen(string username)
        {
            var users = new UserService();
            var roles = new RoleService();
            var form = new frmLockScreen(users, roles, username);
            form.Disposed += (sender, args) =>
            {
                roles.Dispose();
                users.Dispose();
            };
            return form;
        }

        public frmStoreSettings CreateStoreSettingsForm()
        {
            var service = new StoreSettingsService();
            return Own(new frmStoreSettings(service), service);
        }

        public ucRegisters CreateRegistersControl()
        {
            var service = new UserService();
            var control = new ucRegisters(
                service,
                () => new frmRegisterEdit(service),
                userId => new frmRegisterEdit(service, userId));
            control.Disposed += (sender, args) => service.Dispose();
            return control;
        }

        public frmCashMovement CreateCashMovementForm(CashierShiftDTO shift)
        {
            var context = new POSContext();
            var currentUser = new CurrentUserAccessor();
            var authorization = new ClaimsAuthorizationService(currentUser);
            var service = new CashierShiftService(context, new SystemClock(), currentUser, authorization);
            var form = new frmCashMovement(service, authorization, shift);
            form.Disposed += (sender, args) => ((System.IDisposable)(object)context).Dispose();
            return form;
        }

        public ucAudit CreateAuditControl()
        {
            var currentUser = new CurrentUserAccessor();
            var authorization = new ClaimsAuthorizationService(currentUser);
            return new ucAudit(new AuditService(() => new POS.Data.Context.POSContext(), authorization),
                new AuditArchiveService(() => new POS.Data.Context.POSContext(), authorization, new SystemClock(), currentUser));
        }

        public ucDashboard CreateDashboardControl()
        {
            var service = new ManagementReportService();
            var control = new ucDashboard(service);
            control.Disposed += (sender, args) => service.Dispose();
            return control;
        }

        public ucReports CreateReportsControl()
        {
            var reports = new ManagementReportService();
            var output = new ReportOutputService();
            var control = new ucReports(reports, output);
            control.Disposed += (sender, args) =>
            {
                output.Dispose();
                reports.Dispose();
            };
            return control;
        }

        public ucReturns CreateReturnsControl()
        {
            var context = new POSContext();
            var currentUser = new CurrentUserAccessor();
            var authorization = new ClaimsAuthorizationService(currentUser);
            var clock = new SystemClock();
            var receipts = new SaleReceiptService(context, currentUser, authorization, new WindowsReceiptPrinter(), clock);
            var returns = new SaleReturnService(context, clock, currentUser, authorization);
            var control = new ucReturns(receipts, returns);
            control.Disposed += (sender, args) =>
            {
                returns.Dispose();
                receipts.Dispose();
                ((System.IDisposable)(object)context).Dispose();
            };
            return control;
        }

        private static TForm Own<TForm>(TForm form, System.IDisposable service)
            where TForm : System.Windows.Forms.Form
        {
            form.Disposed += (sender, args) => service.Dispose();
            return form;
        }
    }
}
