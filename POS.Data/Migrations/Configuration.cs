namespace POS.Data.Migrations
{
    using Microsoft.AspNet.Identity;
    using Microsoft.AspNet.Identity.EntityFramework;
    using POS.Data.Context;
    using POS.Domains.Operations;
    using POS.Domains.Security;
    using POS.Core.Security;
    using System;
    using System.Data.Entity;
    using System.Data.Entity.Migrations;
    using System.Linq;

    public sealed class Configuration : DbMigrationsConfiguration<POS.Data.Context.POSContext>
    {
        public Configuration()
        {
            AutomaticMigrationsEnabled = true;
            AutomaticMigrationDataLossAllowed = false;
        }

        protected override void Seed(POSContext context)
        {
            var roleStore = new RoleStore<Role>(
                context
            );

            var roleManager = new RoleManager<Role>(roleStore);

            // POSContext uses the application's custom Role type. The short
            // UserStore<User> form assumes IdentityRole, which is not part of
            // this model and makes seeding fail on a brand-new database when
            // AddToRole is called.
            var userStore = new UserStore<User, Role, string, IdentityUserLogin,
                IdentityUserRole, IdentityUserClaim>(context)
            {
                DisposeContext = false
            };

            var userManager = new UserManager<User, string>(userStore);

            const string roleName = "System Administrator";
            const string username = "admin";

            var role = roleManager.FindByName(roleName);

            if (role == null)
            {
                role = new Role
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = roleName
                };

                roleManager.Create(role);
            }

            var user = userManager.FindByName(username);

            if (user == null)
            {
                user = new User
                {
                    Id = Guid.NewGuid().ToString(),
                    UserName = username,
                    Email = "admin@system.admin"
                };

                var result = userManager.Create(user, "Admin@123");

                if (!result.Succeeded)
                {
                    throw new Exception(
                        string.Join(", ", result.Errors)
                    );
                }

                userManager.AddToRole(user.Id, roleName);
            }

            SeedPermissions(context, roleManager, role);
            SeedDefaultPosConfiguration(context);
        }

        private static void SeedDefaultPosConfiguration(POSContext context)
        {
            if (!context.RegisterStations.Any())
            {
                context.RegisterStations.Add(new RegisterStation
                {
                    Code = "REG-01",
                    Name = "Main Register",
                    IsActive = true
                });
            }

            if (!context.StoreSettings.Any())
            {
                var tax = context.TaxRates
                    .Where(x => x.IsActive && !x.EffectiveToUtc.HasValue)
                    .OrderBy(x => x.Id)
                    .FirstOrDefault();
                if (tax == null)
                {
                    tax = new TaxRate
                    {
                        Name = "VAT",
                        Rate = 0.12m,
                        IsInclusive = false,
                        IsActive = true,
                        EffectiveFromUtc = DateTime.UtcNow
                    };
                    context.TaxRates.Add(tax);
                    context.SaveChanges();
                }

                context.StoreSettings.Add(new StoreSetting
                {
                    StoreName = "My Store",
                    CurrencyCode = "PHP",
                    MoneyDecimalPlaces = 2,
                    TimeZoneId = TimeZoneInfo.Local.Id,
                    ReceiptFooter = "Thank you for your purchase.",
                    DefaultTaxRateId = tax.Id,
                    AllowNegativeStock = false
                });
            }

            if (!context.NumberSequences.Any(x => x.DocumentType == "SALE"))
            {
                context.NumberSequences.Add(new NumberSequence
                {
                    DocumentType = "SALE",
                    Prefix = "R",
                    NextNumber = 1
                });
            }

            context.SaveChanges();
        }

        private static void SeedPermissions(POSContext context, RoleManager<Role> roleManager, Role administrator)
        {
            var legacyNames = new[]
            {
                new { Legacy = "UserForm", Code = ResourceCodes.Users },
                new { Legacy = "RoleForm", Code = ResourceCodes.Roles },
                new { Legacy = "ModuleForm", Code = ResourceCodes.Modules },
                new { Legacy = "ProductsForm", Code = ResourceCodes.Products }
            };

            foreach (var mapping in legacyNames)
            {
                var legacy = context.Set<Module>().FirstOrDefault(x => x.Name == mapping.Legacy);
                if (legacy != null) legacy.Name = mapping.Code;
            }

            context.SaveChanges();

            var resourceCodes = new[]
            {
                ResourceCodes.Users, ResourceCodes.Roles, ResourceCodes.Modules,
                ResourceCodes.Settings, ResourceCodes.Products, ResourceCodes.Inventory,
                ResourceCodes.Sales, ResourceCodes.Audit, ResourceCodes.Suppliers,
                ResourceCodes.Purchasing, ResourceCodes.Customers, ResourceCodes.Shifts,
                ResourceCodes.Returns, ResourceCodes.Dashboard, ResourceCodes.Reports,
                ResourceCodes.Maintenance
            };

            foreach (var code in resourceCodes)
            {
                var module = context.Set<Module>().FirstOrDefault(x => x.Name == code);
                if (module == null)
                {
                    module = new Module { Name = code };
                    context.Set<Module>().Add(module);
                }
            }

            context.SaveChanges();
            ConsolidateDuplicateModules(context, resourceCodes);

            foreach (var code in resourceCodes)
            {
                var module = context.Set<Module>().Single(x => x.Name == code);
                var claim = context.Set<RoleClaim>()
                    .FirstOrDefault(x => x.RoleId == administrator.Id && x.ModuleId == module.Id);
                if (claim == null)
                {
                    claim = new RoleClaim
                    {
                        Id = Guid.NewGuid().ToString(),
                        RoleId = administrator.Id,
                        Module = module
                    };
                    context.Set<RoleClaim>().Add(claim);
                }

                claim.CanView = true;
                claim.CanAdd = true;
                claim.CanEdit = true;
                claim.CanDelete = true;
            }

            // Keep the operational roles available on every migration/seed run.
            // Demo user accounts are intentionally not seeded; administrators
            // assign real users to these roles through the user management UI.
            foreach (var baselineRole in new[] { "Manager", "Cashier", "Inventory Clerk" })
                EnsureRole(roleManager, baselineRole);

            // Manager: broad operational access, but no destructive access to
            // users, roles, or maintenance data by default.
            EnsurePermission(context, roleManager, "Manager", ResourceCodes.Users, true, true, true, false);
            EnsurePermission(context, roleManager, "Manager", ResourceCodes.Roles, true, false, false, false);
            EnsurePermission(context, roleManager, "Manager", ResourceCodes.Modules, true, false, false, false);
            EnsurePermission(context, roleManager, "Manager", ResourceCodes.Settings, true, true, true, false);
            EnsurePermission(context, roleManager, "Manager", ResourceCodes.Products, true, true, true, false);
            EnsurePermission(context, roleManager, "Manager", ResourceCodes.Inventory, true, true, true, false);
            EnsurePermission(context, roleManager, "Manager", ResourceCodes.Suppliers, true, true, true, false);
            EnsurePermission(context, roleManager, "Manager", ResourceCodes.Purchasing, true, true, true, false);
            EnsurePermission(context, roleManager, "Manager", ResourceCodes.Customers, true, true, true, false);
            EnsurePermission(context, roleManager, "Manager", ResourceCodes.Shifts, true, true, true, true);
            EnsurePermission(context, roleManager, "Manager", ResourceCodes.Sales, true, true, true, false);
            EnsurePermission(context, roleManager, "Manager", ResourceCodes.Returns, true, true, true, true);
            EnsurePermission(context, roleManager, "Manager", ResourceCodes.Dashboard, true, false, false, false);
            EnsurePermission(context, roleManager, "Manager", ResourceCodes.Reports, true, true, false, false);
            EnsurePermission(context, roleManager, "Manager", ResourceCodes.Audit, true, false, false, false);
            EnsurePermission(context, roleManager, "Manager", ResourceCodes.Maintenance, true, true, true, false);

            // Cashier: selling, customer lookup, returns, and shift/cash
            // operations. Product and inventory data are read-only.
            EnsurePermission(context, roleManager, "Cashier", ResourceCodes.Products, true, false, false, false);
            EnsurePermission(context, roleManager, "Cashier", ResourceCodes.Inventory, true, false, false, false);
            EnsurePermission(context, roleManager, "Cashier", ResourceCodes.Sales, true, true, false, false);
            EnsurePermission(context, roleManager, "Cashier", ResourceCodes.Returns, true, true, false, false);
            EnsurePermission(context, roleManager, "Cashier", ResourceCodes.Customers, true, true, true, false);
            EnsurePermission(context, roleManager, "Cashier", ResourceCodes.Shifts, true, true, true, false);

            // Inventory Clerk: catalog, stock, suppliers, and purchasing.
            // No sales, returns, user administration, or maintenance access.
            EnsurePermission(context, roleManager, "Inventory Clerk", ResourceCodes.Products, true, true, true, false);
            EnsurePermission(context, roleManager, "Inventory Clerk", ResourceCodes.Inventory, true, true, true, false);
            EnsurePermission(context, roleManager, "Inventory Clerk", ResourceCodes.Suppliers, true, false, false, false);
            EnsurePermission(context, roleManager, "Inventory Clerk", ResourceCodes.Purchasing, true, true, true, false);
            EnsurePermission(context, roleManager, "Inventory Clerk", ResourceCodes.Dashboard, true, false, false, false);

            context.SaveChanges();
        }

        private static void ConsolidateDuplicateModules(POSContext context, string[] resourceCodes)
        {
            foreach (var code in resourceCodes)
            {
                var modules = context.Set<Module>()
                    .Where(x => x.Name == code)
                    .OrderBy(x => x.Id)
                    .ToList();
                if (modules.Count < 2) continue;

                var canonical = modules[0];
                foreach (var duplicate in modules.Skip(1))
                {
                    var duplicateClaims = context.Set<RoleClaim>()
                        .Where(x => x.ModuleId == duplicate.Id)
                        .ToList();
                    foreach (var duplicateClaim in duplicateClaims)
                    {
                        var canonicalClaim = context.Set<RoleClaim>().FirstOrDefault(x =>
                            x.ModuleId == canonical.Id && x.RoleId == duplicateClaim.RoleId);
                        if (canonicalClaim == null)
                        {
                            duplicateClaim.ModuleId = canonical.Id;
                        }
                        else
                        {
                            canonicalClaim.CanView |= duplicateClaim.CanView;
                            canonicalClaim.CanAdd |= duplicateClaim.CanAdd;
                            canonicalClaim.CanEdit |= duplicateClaim.CanEdit;
                            canonicalClaim.CanDelete |= duplicateClaim.CanDelete;
                            context.Set<RoleClaim>().Remove(duplicateClaim);
                        }
                    }

                    context.Set<Module>().Remove(duplicate);
                }
            }

            context.SaveChanges();
        }

        private static void EnsurePermission(
            POSContext context,
            RoleManager<Role> roleManager,
            string roleName,
            string resourceCode,
            bool canView,
            bool canAdd,
            bool canEdit,
            bool canDelete)
        {
            var role = roleManager.FindByName(roleName);
            var module = context.Set<Module>().Single(x => x.Name == resourceCode);
            var claim = context.Set<RoleClaim>()
                .FirstOrDefault(x => x.RoleId == role.Id && x.ModuleId == module.Id);
            if (claim == null)
            {
                claim = new RoleClaim
                {
                    Id = Guid.NewGuid().ToString(),
                    RoleId = role.Id,
                    ModuleId = module.Id
                };
                context.Set<RoleClaim>().Add(claim);
            }

            claim.CanView = canView;
            claim.CanAdd = canAdd;
            claim.CanEdit = canEdit;
            claim.CanDelete = canDelete;
        }

        private static void EnsureRole(RoleManager<Role> roleManager, string roleName)
        {
            if (roleManager.FindByName(roleName) != null)
                return;

            var result = roleManager.Create(new Role
            {
                Id = Guid.NewGuid().ToString(),
                Name = roleName
            });

            if (!result.Succeeded)
                throw new InvalidOperationException(
                    $"Unable to seed role '{roleName}': {string.Join(", ", result.Errors)}");
        }
    }
}
