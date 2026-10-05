using POS.Forms;
using POS.Forms.Security;
using POS.Forms.Settings;
using POS.Services;
using POS.Services.Security;
using POS.Services.Settings;
using POS.Core.Abstractions;

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

        public ucAudit CreateAuditControl()
        {
            var currentUser = new CurrentUserAccessor();
            var authorization = new ClaimsAuthorizationService(currentUser);
            return new ucAudit(new AuditService(() => new POS.Data.Context.POSContext(), authorization),
                new AuditArchiveService(() => new POS.Data.Context.POSContext(), authorization, new SystemClock(), currentUser));
        }

        private static TForm Own<TForm>(TForm form, System.IDisposable service)
            where TForm : System.Windows.Forms.Form
        {
            form.Disposed += (sender, args) => service.Dispose();
            return form;
        }
    }
}
