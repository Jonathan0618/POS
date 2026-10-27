namespace POS.Data.Migrations
{
    using Microsoft.AspNet.Identity;
    using Microsoft.AspNet.Identity.EntityFramework;
    using POS.Common.Enumerations;
    using POS.Data.Context;
    using POS.Domains.BusinessObjects;
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
            SeedDemoUsers(userManager, roleManager);
            SeedDefaultPosConfiguration(context);
            SeedThreeMonthSales(context, user);
        }

        private static void SeedDemoUsers(
            UserManager<User, string> userManager,
            RoleManager<Role> roleManager)
        {
            // Development/demo accounts only. These credentials must be
            // changed or disabled before a production deployment.
            EnsureDemoUser(userManager, roleManager,
                "cashier.demo", "Cashier@123", "Demo", "Cashier", "Cashier");
            EnsureDemoUser(userManager, roleManager,
                "inventory.demo", "Inventory@123", "Demo", "Inventory Clerk", "Inventory Clerk");
            EnsureDemoUser(userManager, roleManager,
                "manager.demo", "Manager@123", "Demo", "Manager", "Manager");
        }

        private static void EnsureDemoUser(
            UserManager<User, string> userManager,
            RoleManager<Role> roleManager,
            string username,
            string password,
            string firstName,
            string lastName,
            string roleName)
        {
            var user = userManager.FindByName(username);
            if (user == null)
            {
                user = new User
                {
                    Id = Guid.NewGuid().ToString(),
                    UserName = username,
                    Email = username + "@demo.pos.local",
                    EmailConfirmed = true,
                    FirstName = firstName,
                    LastName = lastName,
                    LockoutEnabled = true
                };

                var createResult = userManager.Create(user, password);
                if (!createResult.Succeeded)
                    throw new InvalidOperationException(
                        $"Unable to seed demo user '{username}': " +
                        string.Join(", ", createResult.Errors));
            }

            var role = roleManager.FindByName(roleName);
            if (role == null)
                throw new InvalidOperationException(
                    $"Unable to assign demo user '{username}'; role '{roleName}' was not found.");

            var roles = userManager.GetRoles(user.Id);
            // Demo identities must have exactly one baseline role. This keeps
            // their permission behavior deterministic if a previous seed run
            // or manual test assigned an additional role.
            foreach (var existingRole in roles
                .Where(x => !x.Equals(roleName, StringComparison.OrdinalIgnoreCase))
                .ToList())
            {
                var removeResult = userManager.RemoveFromRole(user.Id, existingRole);
                if (!removeResult.Succeeded)
                    throw new InvalidOperationException(
                        $"Unable to remove role '{existingRole}' from demo user '{username}': " +
                        string.Join(", ", removeResult.Errors));
            }

            if (!roles.Any(x => x.Equals(roleName, StringComparison.OrdinalIgnoreCase)))
            {
                var roleResult = userManager.AddToRole(user.Id, roleName);
                if (!roleResult.Succeeded)
                    throw new InvalidOperationException(
                        $"Unable to assign demo user '{username}' to '{roleName}': " +
                        string.Join(", ", roleResult.Errors));
            }
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

        private static void SeedThreeMonthSales(POSContext context, User cashierUser)
        {
            // The marker makes this seed safe to run repeatedly during future
            // migration updates and prevents duplicate financial history.
            if (context.Sales.Any(x => x.ReceiptNumber.StartsWith("SEED-")))
                return;

            var register = context.RegisterStations.Where(x => x.IsActive).OrderBy(x => x.Id).FirstOrDefault();
            var settings = context.StoreSettings.OrderBy(x => x.Id).FirstOrDefault();
            var tax = settings == null ? null : context.TaxRates.SingleOrDefault(x => x.Id == settings.DefaultTaxRateId);
            if (register == null || settings == null || tax == null || cashierUser == null)
                return;

            var category = context.Set<Category>().SingleOrDefault(x => x.Name == "Seed Grocery");
            if (category == null)
            {
                category = new Category
                {
                    Name = "Seed Grocery",
                    Description = "Sample products created for demonstrations and reports.",
                    IsActive = true
                };
                context.Set<Category>().Add(category);
                context.SaveChanges();
            }

            var products = new[]
            {
                EnsureSeedProduct(context, category, "SEED-001", "Sample Rice 5kg", 899m, 750m, "480100000001"),
                EnsureSeedProduct(context, category, "SEED-002", "Sample Cooking Oil 1L", 189m, 150m, "480100000002"),
                EnsureSeedProduct(context, category, "SEED-003", "Sample Coffee 200g", 249m, 190m, "480100000003")
            };

            const int openingQuantity = 10000;
            foreach (var product in products)
            {
                var balance = context.InventoryBalances.SingleOrDefault(x => x.ProductId == product.Id);
                if (balance == null)
                {
                    context.InventoryBalances.Add(new InventoryBalance
                    {
                        ProductId = product.Id,
                        QuantityOnHand = openingQuantity
                    });
                    context.StockMovements.Add(new StockMovement
                    {
                        ProductId = product.Id,
                        MovementType = StockMovementType.OpeningBalance,
                        QuantityDelta = openingQuantity,
                        ReferenceType = "Seed",
                        ReferenceId = "SEED-OPEN-" + product.Sku,
                        Reason = "Opening balance for three-month sample sales",
                        UserId = cashierUser.Id,
                        CreatedUtc = DateTime.UtcNow.AddDays(-90)
                    });
                }
                product.Quantity = openingQuantity;
            }

            var customer = context.Customers.SingleOrDefault(x => x.Code == "SEED-CUSTOMER-001");
            if (customer == null)
            {
                customer = new Customer
                {
                    Code = "SEED-CUSTOMER-001",
                    Name = "Sample Walk-in Customer",
                    IsActive = true
                };
                context.Customers.Add(customer);
            }

            context.SaveChanges();

            var balances = products.ToDictionary(product => product.Id,
                product => context.InventoryBalances.Single(x => x.ProductId == product.Id));
            var startDate = DateTime.UtcNow.Date.AddDays(-89);
            var taxRate = tax.Rate;
            var saleNumber = 1;

            for (var day = 0; day < 90; day++)
            {
                var openedUtc = startDate.AddDays(day).AddHours(8);
                var shift = new CashierShift
                {
                    RequestId = Guid.NewGuid(),
                    RegisterStationId = register.Id,
                    CashierUserId = cashierUser.Id,
                    Status = ShiftStatus.Closed,
                    OpenedUtc = openedUtc,
                    ClosedUtc = openedUtc.AddHours(10),
                    OpeningCash = 500m,
                    CountedCash = 500m,
                    Variance = 0m
                };
                shift.CashMovements.Add(new CashMovement
                {
                    RequestId = Guid.NewGuid(),
                    MovementType = CashMovementType.Opening,
                    Amount = 500m,
                    Reason = "Seeded opening float",
                    UserId = cashierUser.Id,
                    CreatedUtc = openedUtc,
                    CashierShift = shift
                });
                context.CashierShifts.Add(shift);

                var dailyCashSales = 0m;
                for (var saleOfDay = 0; saleOfDay < 3; saleOfDay++)
                {
                    var product = products[(day + saleOfDay) % products.Length];
                    var quantity = 1 + ((day + saleOfDay) % 3);
                    var subtotal = decimal.Round(product.Price * quantity, 2);
                    var saleTax = decimal.Round(subtotal * taxRate, 2);
                    var total = subtotal + saleTax;
                    var saleDate = openedUtc.AddHours(1 + saleOfDay * 3).AddMinutes(day % 20);
                    var receiptNumber = string.Format("SEED-{0:yyyyMMdd}-{1:000}", saleDate, saleNumber++);
                    var cashReceived = decimal.Ceiling(total / 100m) * 100m;
                    var sale = new Sale
                    {
                        RequestId = Guid.NewGuid(),
                        RequestHash = "SEED-SALE-" + receiptNumber,
                        SaleDate = saleDate,
                        ReceiptNumber = receiptNumber,
                        Status = DocumentStatus.Completed,
                        RegisterStationId = register.Id,
                        CashierShiftId = shift.Id,
                        CustomerId = customer.Id,
                        CashierUserId = cashierUser.Id,
                        Subtotal = subtotal,
                        TaxAmount = saleTax,
                        TotalAmount = total,
                        CashReceived = cashReceived,
                        Change = cashReceived - total,
                        CurrencyCode = settings.CurrencyCode,
                        TaxName = tax.Name,
                        TaxInclusive = tax.IsInclusive,
                        MoneyDecimalPlaces = settings.MoneyDecimalPlaces,
                        RoundingMethod = "HalfUp",
                        StoreName = settings.StoreName,
                        StoreAddress = settings.Address,
                        StoreTaxIdentifier = settings.TaxIdentifier,
                        ReceiptFooter = settings.ReceiptFooter,
                        RegisterCode = register.Code,
                        RegisterName = register.Name,
                        CashierName = cashierUser.UserName,
                        CustomerCode = customer.Code,
                        CustomerName = customer.Name,
                        CashierShift = shift,
                        Customer = customer,
                        RegisterStation = register
                    };
                    sale.SaleItems.Add(new SaleItem
                    {
                        Product = product,
                        ProductId = product.Id,
                        Quantity = quantity,
                        ProductName = product.Name,
                        Sku = product.Sku,
                        Barcode = product.Barcode,
                        UnitPrice = product.Price,
                        Subtotal = subtotal,
                        CostPrice = product.CostPrice,
                        TaxRate = taxRate,
                        TaxAmount = saleTax
                    });
                    sale.Payments.Add(new Payment
                    {
                        TenderType = TenderType.Cash,
                        Status = PaymentStatus.Completed,
                        Amount = total,
                        ExternalReference = "SEED-CASH-" + saleNumber,
                        CreatedUtc = saleDate,
                        Sale = sale
                    });
                    context.Sales.Add(sale);
                    context.StockMovements.Add(new StockMovement
                    {
                        ProductId = product.Id,
                        MovementType = StockMovementType.Sale,
                        QuantityDelta = -quantity,
                        ReferenceType = "Sale",
                        ReferenceId = receiptNumber,
                        Reason = "Seeded sample sale",
                        UserId = cashierUser.Id,
                        CreatedUtc = saleDate,
                        Product = product
                    });
                    balances[product.Id].QuantityOnHand -= quantity;
                    dailyCashSales += total;
                }

                shift.ExpectedCash = shift.OpeningCash + dailyCashSales;
                shift.CountedCash = shift.ExpectedCash;
            }

            foreach (var balance in balances.Values)
                balance.Product.Quantity = balance.QuantityOnHand;
            context.SaveChanges();
        }

        private static Product EnsureSeedProduct(POSContext context, Category category,
            string sku, string name, decimal price, decimal cost, string barcode)
        {
            var product = context.Set<Product>().SingleOrDefault(x => x.Sku == sku);
            if (product != null)
                return product;

            product = new Product
            {
                Name = name,
                Sku = sku,
                Description = "Sample product for seeded retail history.",
                Price = price,
                CostPrice = cost,
                CategoryId = category.Id,
                Category = category,
                Barcode = barcode,
                Unit = "piece",
                IsActive = true,
                BuyingThreshold = 10,
                Quantity = 0
            };
            context.Set<Product>().Add(product);
            context.SaveChanges();
            return product;
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
