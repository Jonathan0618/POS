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
using POS.Services.Purchasing;
using POS.Services.Customers;
using POS.Services.Inventory;
using POS.Services.Maintenance;

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

        public frmStockAdjustment CreateStockAdjustmentForm()
        {
            var context = new POSContext(); var user = new CurrentUserAccessor(); var auth = new ClaimsAuthorizationService(user);
            var products = new InventoryService(context, auth); var ledger = new InventoryLedgerService(context, new SystemClock(), user, auth);
            var form = new frmStockAdjustment(products, ledger); form.Disposed += (s, e) => ((System.IDisposable)context).Dispose(); return form;
        }

        public ucInventory CreateInventoryControl()
        {
            var context = new POSContext(); var user = new CurrentUserAccessor(); var auth = new ClaimsAuthorizationService(user);
            var ledger = new InventoryLedgerService(context, new SystemClock(), user, auth); var products = new InventoryService(context, auth);
            var control = new ucInventory(ledger, products); control.Disposed += (s, e) => ((System.IDisposable)context).Dispose(); return control;
        }

        public ucStockCounts CreateStockCountsControl()
        {
            var context = new POSContext(); var user = new CurrentUserAccessor(); var auth = new ClaimsAuthorizationService(user); var clock = new SystemClock();
            var service = new StockCountService(context, clock, user, auth); var products = new InventoryService(context, auth); var ledger = new InventoryLedgerService(context, clock, user, auth);
            var control = new ucStockCounts(service, products, ledger); control.Disposed += (s, e) => ((System.IDisposable)context).Dispose(); return control;
        }

        public ucSalesRegister CreateSalesRegisterControl()
        {
            var currentUser = new CurrentUserAccessor();
            var clock = new SystemClock();
            var authorization = new ClaimsAuthorizationService(currentUser);
            var context = new POSContext("POSConnection", clock, currentUser);
            var cart = new SalesCartService(context, authorization);
            var sales = new SalesService(context, clock, currentUser, authorization);
            var shifts = new CashierShiftService(context, clock, currentUser, authorization);
            var customers = new CustomerService(context, authorization);
            var receipts = new SaleReceiptService(context, currentUser, authorization,
                new WindowsReceiptPrinter(), clock);
            var control = new ucSalesRegister(cart, sales, shifts, customers, receipts,
                () => new frmHeldSales(sales));
            control.Disposed += (sender, args) =>
            {
                receipts.Dispose();
                sales.Dispose();
                cart.Dispose();
                ((System.IDisposable)context).Dispose();
            };
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
            var context = new POSContext();
            var currentUser = new CurrentUserAccessor();
            var authorization = new ClaimsAuthorizationService(currentUser);
            var service = new RegisterStationService(context, authorization);
            var printer = new WindowsReceiptPrinter();
            var control = new ucRegisters(service,
                model => new frmRegisterEdit(service, printer.GetInstalledPrinters(), model));
            control.Disposed += (sender, args) => ((System.IDisposable)context).Dispose();
            return control;
        }

        public frmHeldSales CreateHeldSalesForm()
        {
            var context = new POSContext();
            var currentUser = new CurrentUserAccessor();
            var authorization = new ClaimsAuthorizationService(currentUser);
            var service = new SalesService(context, new SystemClock(), currentUser, authorization);
            var form = new frmHeldSales(service);
            form.Disposed += (sender, args) =>
            {
                service.Dispose();
                ((System.IDisposable)context).Dispose();
            };
            return form;
        }

        public frmReceivePurchaseOrder CreateReceivePurchaseOrderForm()
        {
            var context = new POSContext();
            var currentUser = new CurrentUserAccessor();
            var authorization = new ClaimsAuthorizationService(currentUser);
            var clock = new SystemClock();
            var form = new frmReceivePurchaseOrder(
                new PurchaseOrderService(context, clock, authorization),
                new GoodsReceiptService(context, clock, currentUser, authorization));
            form.Disposed += (sender, args) => ((System.IDisposable)context).Dispose();
            return form;
        }

        public ucGoodsReceiving CreateGoodsReceivingControl()
        {
            return new ucGoodsReceiving(CreateReceivePurchaseOrderForm);
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

        public frmCloseShift CreateCloseShiftForm(CashierShiftDTO shift)
        {
            var context = new POSContext();
            var currentUser = new CurrentUserAccessor();
            var authorization = new ClaimsAuthorizationService(currentUser);
            var service = new CashierShiftService(context, new SystemClock(), currentUser, authorization);
            var form = new frmCloseShift(service, authorization, shift);
            form.Disposed += (sender, args) => ((System.IDisposable)context).Dispose();
            return form;
        }

        public frmOpenShift CreateOpenShiftForm()
        {
            var context = new POSContext();
            var currentUser = new CurrentUserAccessor();
            var authorization = new ClaimsAuthorizationService(currentUser);
            var service = new CashierShiftService(context, new SystemClock(), currentUser, authorization);
            var form = new frmOpenShift(service, currentUser, authorization);
            form.Disposed += (sender, args) => ((System.IDisposable)context).Dispose();
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
            var shifts = new CashierShiftService(context, clock, currentUser, authorization);
            var control = new ucReturns(receipts, returns);
            control.ReturnRequested += (sender, args) =>
            {
                var eligibility = returns.GetEligibility(args.SaleId);
                using (var form = new frmSaleReturn(returns, shifts, eligibility))
                {
                    if (form.ShowDialog(control.FindForm()) == System.Windows.Forms.DialogResult.OK)
                        control.RefreshEligibility();
                }
            };
            control.Disposed += (sender, args) =>
            {
                returns.Dispose();
                receipts.Dispose();
                ((System.IDisposable)(object)context).Dispose();
            };
            return control;
        }

        public ucReceipts CreateReceiptsControl()
        {
            var context = new POSContext();
            var currentUser = new CurrentUserAccessor();
            var authorization = new ClaimsAuthorizationService(currentUser);
            var service = new SaleReceiptService(context, currentUser, authorization, new WindowsReceiptPrinter(), new SystemClock());
            var control = new ucReceipts(service);
            control.Disposed += (sender, args) =>
            {
                service.Dispose();
                ((System.IDisposable)context).Dispose();
            };
            return control;
        }

        public ucMaintenance CreateMaintenanceControl()
        {
            var context = new POSContext();
            var currentUser = new CurrentUserAccessor();
            var authorization = new ClaimsAuthorizationService(currentUser);
            var service = new DatabaseMaintenanceService(context, authorization, new SystemClock(), currentUser, new DiagnosticFileLogger());
            var control = new ucMaintenance(service);
            control.Disposed += (sender, args) => ((System.IDisposable)context).Dispose();
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
