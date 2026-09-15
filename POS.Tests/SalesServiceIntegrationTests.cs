using POS.Data.Context;
using POS.Domains.BusinessObjects;
using POS.Services.Point_of_Sale;
using POS.Services.Security;
using POS.Models.Security;
using System;
using System.Data.SqlClient;
using System.Data.Entity;
using System.Linq;
using System.Security.Claims;
using POS.Core;
using POS.Core.Abstractions;
using POS.Common.Enumerations;
using POS.Domains.Security;
using POS.Domains.Operations;
using POS.Models.Operations;
using POS.Services.Settings;
using POS.Services.Purchasing;
using POS.Services.Inventory;
using POS.Services.Reporting;
using POS.Services.Shifts;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;

namespace POS.Tests
{
    internal static class SalesServiceIntegrationTests
    {
        private static readonly string DatabaseName =
            "POS_SalesServiceIntegrationTests_" + Guid.NewGuid().ToString("N");
        private static readonly string ConnectionString =
            "Data Source=(LocalDB)\\MSSQLLocalDB;Initial Catalog=" + DatabaseName +
            ";Integrated Security=True;MultipleActiveResultSets=True";

        public static void RunAll()
        {
            Database.SetInitializer(new DropCreateDatabaseAlways<POSContext>());

            try
            {
                FailedCheckoutDoesNotDecrementStock();
                CompletedSalesReceiveSequentialReceiptNumbers();
                StaleSettingsCannotRewindReceiptNumbers();
                ConcurrentCheckoutsReceiveUniqueReceiptNumbers();
                AuditRecordsCannotBeEditedOrDeleted();
                AuditSearchFiltersAndPagesInStableOrder();
                AutomaticAuditPersistsOnlyApprovedChanges();
                RepeatedAuthenticationFailuresLockAccount();
                LegacyAccountEnablesLockoutDuringSignIn();
                PasswordChangeAndAdministrativeResetReplaceCredentials();
                LogoutClearsSessionAndWritesAudit();
                LockLifecycleClearsSessionAndWritesAudit();
                DisabledAccountCannotAuthenticate();
                AdministratorSafetyRulesAreEnforced();
                ModuleHierarchyAndDeletionRulesAreEnforced();
                UserSearchAndPaginationAreAppliedByTheService();
                PermissionMatrixIsRoleScopedAndReadOnlyOnLoad();
                RoleLifecycleSafetyIsEnforced();
                TaxChangesCreateEffectiveDatedHistory();
                SupplierMaintenanceRejectsStaleRevisions();
                PurchaseOrderLifecycleRejectsStaleRevisions();
                PartialGoodsReceiptIsIdempotentAndUpdatesInventory();
                PurchaseReturnIsIdempotentAndReversesInventory();
                ManualAdjustmentIsIdempotentAndRequiresReviewedQuantity();
                CheckoutReplayDoesNotDuplicateSaleOrStockMovement();
                ReturnPostingIsIdempotentAndRestocksInventory();
                ManagementReportsReconcileWithPersistedFacts();
                CashierShiftLifecycleIsIdempotentAndDerived();
            }
            finally
            {
                using (var cleanupContext = new POSContext(ConnectionString))
                {
                    cleanupContext.Database.Delete();
                }
            }
        }

        private static void FailedCheckoutDoesNotDecrementStock()
        {
            int productId;

            using (var arrangeContext = new POSContext(ConnectionString))
            {
                arrangeContext.Database.Initialize(true);
                EnsureAuditTableExists(arrangeContext);
                using (var settings = new StoreSettingsService(
                    arrangeContext,
                    new SystemClock(),
                    new AllowAllAuthorizationService()))
                {
                    var loadedSettings = settings.GetSettings();
                    loadedSettings.StoreName = "Integration Store";
                    loadedSettings.CurrencyCode = "PHP";
                    loadedSettings.TimeZoneId = TimeZoneInfo.Local.Id;
                    loadedSettings.TaxName = "VAT";
                    loadedSettings.TaxRate = 0.12m;
                    loadedSettings.RegisterCode = "TEST-REG";
                    loadedSettings.RegisterName = "Integration Register";
                    settings.SaveSettings(loadedSettings);
                }

                var testRegisterId = arrangeContext.RegisterStations.Select(x => x.Id).Single();
                arrangeContext.CashierShifts.Add(new POS.Domains.Operations.CashierShift
                {
                    RequestId = Guid.NewGuid(),
                    RegisterStationId = testRegisterId,
                    CashierUserId = "integration-user-id",
                    Status = ShiftStatus.Open,
                    OpenedUtc = DateTime.UtcNow
                });
                arrangeContext.SaveChanges();

                var categoryId = arrangeContext.Database.SqlQuery<int>(
                    "INSERT INTO dbo.Categories (Name, Description) " +
                    "VALUES (@name, NULL); SELECT CAST(SCOPE_IDENTITY() AS int);",
                    new SqlParameter("@name", "Integration Test")).Single();

                productId = arrangeContext.Database.SqlQuery<int>(
                    "INSERT INTO dbo.Products " +
                    "(Name, Sku, Description, Price, CostPrice, CategoryId, Quantity, " +
                    "Barcode, Unit, ExpiryDate, IsActive, BuyingThreshold) " +
                    "VALUES (@name, @sku, NULL, 100, 50, @categoryId, 5, @barcode, " +
                    "@unit, NULL, 1, 0); SELECT CAST(SCOPE_IDENTITY() AS int);",
                    new SqlParameter("@name", "Rollback Product"),
                    new SqlParameter("@sku", "ROLLBACK-001"),
                    new SqlParameter("@categoryId", categoryId),
                    new SqlParameter("@barcode", "TEST-ROLLBACK-001"),
                    new SqlParameter("@unit", "Each")).Single();

                arrangeContext.InventoryBalances.Add(new InventoryBalance
                {
                    ProductId = productId,
                    QuantityOnHand = 5
                });
                arrangeContext.StockMovements.Add(new StockMovement
                {
                    ProductId = productId,
                    MovementType = StockMovementType.OpeningBalance,
                    QuantityDelta = 5,
                    ReferenceType = "IntegrationSetup",
                    ReferenceId = "ROLLBACK-001",
                    Reason = "Integration test opening balance",
                    UserId = "integration-user-id",
                    CreatedUtc = DateTime.UtcNow
                });
                arrangeContext.SaveChanges();
            }

            using (var checkoutContext = new POSContext(ConnectionString))
            using (var service = CreateSalesService(checkoutContext))
            {
                var sale = CreateTestSale(checkoutContext, 0m);
                sale.SaleItems.Add(new SaleItem { ProductId = productId, Quantity = 2 });

                AssertThrows<InvalidOperationException>(
                    () => service.CreateSale(sale),
                    "Failed checkout validation");

                var trackedQuantity = checkoutContext.Set<Product>().Find(productId).Quantity;
                AssertEqual(5, trackedQuantity, "Tracked stock after rollback");
            }

            using (var assertContext = new POSContext(ConnectionString))
            {
                AssertEqual(
                    5,
                    assertContext.Set<Product>().Single(x => x.Id == productId).Quantity,
                    "Persisted stock after rollback");
                AssertEqual(0, assertContext.Sales.Count(), "Persisted sales after rollback");
            }
        }

        private static void CompletedSalesReceiveSequentialReceiptNumbers()
        {
            using (var context = new POSContext(ConnectionString))
            using (var sequences = new NumberSequenceService(
                context,
                new AllowAllAuthorizationService()))
            using (var sales = CreateSalesService(context))
            {
                var configured = sequences.ConfigureReceipt("POS-", 41);
                if (!configured.Succeeded)
                    throw new InvalidOperationException("The receipt sequence could not be configured.");

                var productId = context.Set<Product>().Single(x => x.Sku == "ROLLBACK-001").Id;
                var first = CreateTestSale(context, 1000m);
                var firstLine = new SaleItem { ProductId = productId, Quantity = 1 };
                first.SaleItems.Add(firstLine);
                var second = CreateTestSale(context, 1000m);
                second.SaleItems.Add(new SaleItem { ProductId = productId, Quantity = 1 });

                sales.CreateSale(first);
                sales.CreateSale(second);
                if (first.ReceiptNumber != "POS-00000041" ||
                    second.ReceiptNumber != "POS-00000042" ||
                    first.ReceiptNumber == second.ReceiptNumber)
                    throw new InvalidOperationException("Completed sales did not receive sequential receipt numbers.");
                if (first.Subtotal != 100m || first.TaxAmount != 12m ||
                    first.TotalAmount != 112m || firstLine.TaxRate != 0.12m ||
                    firstLine.TaxAmount != 12m || first.CurrencyCode != "PHP" ||
                    first.TaxName != "VAT" || first.TaxInclusive ||
                    first.MoneyDecimalPlaces != 2 ||
                    first.RoundingMethod != MidpointRounding.AwayFromZero.ToString())
                    throw new InvalidOperationException("The configured tax was not snapshotted on the sale.");
                AssertEqual(
                    43,
                    (int)context.NumberSequences.AsNoTracking()
                        .Single(x => x.DocumentType == NumberSequenceService.SaleReceipt).NextNumber,
                    "Next receipt number");
            }
        }

        private static void StaleSettingsCannotRewindReceiptNumbers()
        {
            using (var settingsContext = new POSContext(ConnectionString))
            using (var settings = new StoreSettingsService(
                settingsContext, new SystemClock(), new AllowAllAuthorizationService()))
            using (var sequences = new NumberSequenceService(
                settingsContext, new AllowAllAuthorizationService()))
            {
                var stale = settings.GetSettings();
                var originalName = stale.StoreName;
                using (var checkoutContext = new POSContext(ConnectionString))
                using (var sales = CreateSalesService(checkoutContext))
                {
                    var sale = CreateTestSale(checkoutContext, 1000m);
                    sale.SaleItems.Add(new SaleItem
                    {
                        ProductId = checkoutContext.Set<Product>().Single(x => x.Sku == "ROLLBACK-001").Id,
                        Quantity = 1
                    });
                    sales.CreateSale(sale);
                }

                var result = sequences.ConfigureReceipt(stale.ReceiptPrefix, stale.NextReceiptNumber);
                if (result.Succeeded)
                    throw new InvalidOperationException("Stale receipt configuration moved the sequence backward.");

                stale.StoreName = "Rejected stale settings";
                AssertThrows<ValidationException>(() => settings.SaveSettings(stale), "Stale settings save");
                using (var verification = new POSContext(ConnectionString))
                {
                    var sequence = verification.NumberSequences.Single(x => x.DocumentType == NumberSequenceService.SaleReceipt);
                    if (sequence.NextNumber != stale.NextReceiptNumber + 1)
                        throw new InvalidOperationException("A rejected settings save changed the receipt sequence.");
                    if (verification.StoreSettings.Single().StoreName != originalName)
                        throw new InvalidOperationException("A rejected settings save persisted unrelated settings.");
                }
            }
        }

        private static void ConcurrentCheckoutsReceiveUniqueReceiptNumbers()
        {
            var productIds = new int[2];
            long nextNumber;
            string prefix;
            using (var context = new POSContext(ConnectionString))
            {
                var categoryId = context.Set<Product>().First().CategoryId;
                for (var index = 0; index < productIds.Length; index++)
                {
                    var product = new Product
                    {
                        Name = "Concurrent receipt product " + index,
                        Sku = "CONCURRENT-" + index,
                        Barcode = "CONCURRENT-" + index,
                        Unit = "Each",
                        CategoryId = categoryId,
                        Price = 10m,
                        CostPrice = 5m,
                        Quantity = 2,
                        IsActive = true
                    };
                    context.Set<Product>().Add(product);
                    context.SaveChanges();
                    context.InventoryBalances.Add(new InventoryBalance
                    {
                        ProductId = product.Id,
                        QuantityOnHand = product.Quantity
                    });
                    context.StockMovements.Add(new StockMovement
                    {
                        ProductId = product.Id,
                        MovementType = StockMovementType.OpeningBalance,
                        QuantityDelta = product.Quantity,
                        ReferenceType = "IntegrationSetup",
                        ReferenceId = product.Sku,
                        Reason = "Concurrent checkout test opening balance",
                        UserId = "integration-user-id",
                        CreatedUtc = DateTime.UtcNow
                    });
                    context.SaveChanges();
                    productIds[index] = product.Id;
                }
                var sequence = context.NumberSequences.Single(x => x.DocumentType == NumberSequenceService.SaleReceipt);
                nextNumber = sequence.NextNumber;
                prefix = sequence.Prefix;
            }

            using (var barrier = new Barrier(2))
            {
                var tasks = productIds.Select(productId => Task.Run(() =>
                {
                    try
                    {
                        using (var context = new POSContext(ConnectionString))
                        using (var sales = CreateSalesService(context, new CheckoutBarrierClock(barrier)))
                        {
                            var sale = CreateTestSale(context, 100m);
                            sale.SaleItems.Add(new SaleItem { ProductId = productId, Quantity = 1 });
                            return sales.CreateSale(sale).ReceiptNumber;
                        }
                    }
                    catch (ValidationException exception)
                    {
                        if (!exception.Message.Contains("Checkout conflicted")) throw;
                        return null;
                    }
                })).ToArray();
                Task.WaitAll(tasks);
                var actual = tasks.Select(x => x.Result).Where(x => x != null).ToList();
                for (var index = 0; index < tasks.Length; index++)
                {
                    if (tasks[index].Result != null) continue;
                    using (var context = new POSContext(ConnectionString))
                    using (var sales = CreateSalesService(context))
                    {
                        var reviewedRetry = CreateTestSale(context, 100m);
                        reviewedRetry.SaleItems.Add(new SaleItem { ProductId = productIds[index], Quantity = 1 });
                        actual.Add(sales.CreateSale(reviewedRetry).ReceiptNumber);
                    }
                }
                var expected = new[] { prefix + nextNumber.ToString("D8"), prefix + (nextNumber + 1).ToString("D8") };
                if (!actual.OrderBy(x => x).SequenceEqual(expected.OrderBy(x => x)))
                    throw new InvalidOperationException("Concurrent checkouts did not allocate distinct consecutive receipts.");
            }

            using (var context = new POSContext(ConnectionString))
            {
                if (context.NumberSequences.Single(x => x.DocumentType == NumberSequenceService.SaleReceipt).NextNumber != nextNumber + 2)
                    throw new InvalidOperationException("Concurrent receipt allocations did not persist the next number.");
                AssertEqual(2, context.Sales.Count(x => x.SaleItems.Any(item => productIds.Contains(item.ProductId))), "Concurrent persisted sales");
            }
        }

        private sealed class CheckoutBarrierClock : IClock
        {
            private readonly Barrier _barrier;
            public CheckoutBarrierClock(Barrier barrier) { _barrier = barrier; }
            public DateTime UtcNow
            {
                get
                {
                    if (!_barrier.SignalAndWait(TimeSpan.FromSeconds(20)))
                        throw new TimeoutException("Concurrent checkouts did not reach receipt allocation together.");
                    return DateTime.UtcNow;
                }
            }
        }

        private static void AuditRecordsCannotBeEditedOrDeleted()
        {
            long auditId;
            int productId;
            string productName;
            using (var context = new POSContext(ConnectionString))
            {
                var audit = new POS.Domains.AuditEntry.AuditLog
                {
                    TableName = "AuditGuardTest",
                    Action = "Original",
                    NewValue = "Safe original details",
                    DateLogged = DateTime.UtcNow
                };
                context.AuditLogs.Add(audit);
                context.SaveChanges();
                auditId = audit.Id;
                var product = context.Set<Product>().First();
                productId = product.Id;
                productName = product.Name;
            }

            foreach (var delete in new[] { false, true })
            foreach (var asynchronous in new[] { false, true })
            {
                using (var context = new POSContext(ConnectionString))
                {
                    var audit = context.AuditLogs.Find(auditId);
                    if (delete) context.AuditLogs.Remove(audit);
                    else audit.NewValue = "Attempted replacement";
                    context.Set<Product>().Find(productId).Name = "Must not persist";
                    var entriesBeforeSave = context.ChangeTracker.Entries().Count();
                    AssertThrows<InvalidOperationException>(() =>
                    {
                        if (asynchronous)
                            context.SaveChangesAsync().GetAwaiter().GetResult();
                        else
                            context.SaveChanges();
                    }, "Append-only audit guard");
                    AssertEqual(entriesBeforeSave, context.ChangeTracker.Entries().Count(),
                        "Rejected save must not stage additional audit records");
                }

                using (var verification = new POSContext(ConnectionString))
                {
                    var audit = verification.AuditLogs.Single(x => x.Id == auditId);
                    if (audit.NewValue != "Safe original details" ||
                        verification.Set<Product>().Find(productId).Name != productName)
                        throw new InvalidOperationException("A rejected audit mutation persisted changes.");
                }
            }
        }

        private static void AuditSearchFiltersAndPagesInStableOrder()
        {
            var start = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
            var ids = new long[3];
            using (var context = new POSContext(ConnectionString))
            {
                for (var index = 0; index < 3; index++)
                {
                    var audit = new POS.Domains.AuditEntry.AuditLog
                    {
                        TableName = "ViewerTest", RecordId = "viewer-record-" + index,
                        Action = "ViewerEvent", DateLogged = start,
                        NewValue = "Safe details " + index
                    };
                    context.AuditLogs.Add(audit);
                    context.SaveChanges();
                    ids[index] = audit.Id;
                }
                context.AuditLogs.Add(new POS.Domains.AuditEntry.AuditLog
                {
                    TableName = "ViewerTest", RecordId = "viewer-record-end",
                    Action = "ViewerEvent", DateLogged = start.AddDays(1)
                });
                context.SaveChanges();
            }
            var service = new AuditService(() => new POSContext(ConnectionString), new AllowAllAuthorizationService());
            var filter = new AuditSearchDTO
            {
                FromUtc = start, ToUtcExclusive = start.AddDays(1),
                Search = "viewer-record", Entity = "ViewerTest", Action = "ViewerEvent", PageSize = 2
            };
            var first = service.SearchAsync(filter).GetAwaiter().GetResult();
            AssertEqual(3, first.TotalCount, "Audit date bounds and filters");
            AssertEqual(2, first.TotalPages, "Audit page count");
            if (!first.Items.Select(x => x.Id).SequenceEqual(new[] { ids[2], ids[1] }))
                throw new InvalidOperationException("Audit ties were not ordered by descending event ID.");
            if (first.Items[0].NewValue != "Safe details 2" || first.Items[0].Username != null)
                throw new InvalidOperationException("Audit details or events without users were lost.");
            filter.PageNumber = int.MaxValue;
            var last = service.SearchAsync(filter).GetAwaiter().GetResult();
            AssertEqual(2, last.PageNumber, "Clamped audit page");
            if (last.Items.Single().Id != ids[0])
                throw new InvalidOperationException("Audit pages duplicated or skipped an event.");
            filter.Action = "NoMatchingAction";
            var empty = service.SearchAsync(filter).GetAwaiter().GetResult();
            AssertEqual(0, empty.TotalCount, "Empty audit filter");
            AssertEqual(1, empty.PageNumber, "Empty audit page");
            filter.ToUtcExclusive = start;
            AssertThrows<ValidationException>(() => service.SearchAsync(filter).GetAwaiter().GetResult(), "Invalid audit date range");
        }

        private static void AutomaticAuditPersistsOnlyApprovedChanges()
        {
            long previousAuditId;
            string originalName;
            using (var context = new POSContext(ConnectionString))
            {
                previousAuditId = context.AuditLogs.Max(x => x.Id);
                var product = context.Set<Product>().First();
                originalName = product.Name;
                product.Name = "Audited \"name\"";
                product.Description = "Excluded private description";
                context.SaveChangesAsync().GetAwaiter().GetResult();
            }
            using (var context = new POSContext(ConnectionString))
            {
                var audit = context.AuditLogs.Single(x => x.Id > previousAuditId && x.TableName == "Product");
                var oldValues = AuditValuePolicyTests.Read(audit.OldValue);
                var newValues = AuditValuePolicyTests.Read(audit.NewValue);
                if (oldValues.Count != 1 || newValues.Count != 1 || oldValues["Name"] != originalName ||
                    newValues["Name"] != "Audited \"name\"")
                    throw new InvalidOperationException("Automatic auditing did not isolate approved changed fields.");
                previousAuditId = audit.Id;
                context.Set<Product>().First().Description = "Another excluded value";
                context.SaveChanges();
            }
            using (var context = new POSContext(ConnectionString))
            {
                var audit = context.AuditLogs.Single(x => x.Id > previousAuditId && x.TableName == "Product");
                if (AuditValuePolicyTests.Read(audit.NewValue).Count != 0 || AuditValuePolicyTests.Read(audit.OldValue).Count != 0)
                    throw new InvalidOperationException("Excluded-only changes leaked field values.");
            }
        }

        private static void RepeatedAuthenticationFailuresLockAccount()
        {
            const string username = "lockouttest";
            using (var context = new POSContext(ConnectionString))
            using (var service = CreateUserService(context))
            {
                service.AddUserAsync(new UserDTO
                {
                    Username = username,
                    Password = "Correct@123",
                    FirstName = "Lockout",
                    LastName = "Test"
                }).GetAwaiter().GetResult();

                for (var attempt = 1; attempt <= 5; attempt++)
                {
                    var result = service.AuthenticateAsync(username, "Wrong@123")
                        .GetAwaiter().GetResult();
                    if (result.Succeeded)
                        throw new InvalidOperationException("Invalid credentials were accepted.");
                }

                var lockedResult = service.AuthenticateAsync(username, "Correct@123")
                    .GetAwaiter().GetResult();
                var storedUser = context.Users.Single(x => x.UserName == username);
                if (lockedResult.Succeeded ||
                    !lockedResult.Errors[0].Contains("temporarily locked"))
                {
                    throw new InvalidOperationException(
                        $"Account lockout was not enforced. Attempts={storedUser.AccessFailedCount}, " +
                        $"enabled={storedUser.LockoutEnabled}, end={storedUser.LockoutEndDateUtc}, " +
                        $"result={string.Join("; ", lockedResult.Errors)}.");
                }

                var leakedIdentitySecret = context.AuditLogs.Any(x =>
                    x.OldValue.Contains("PasswordHash") ||
                    x.NewValue.Contains("PasswordHash") ||
                    x.OldValue.Contains("SecurityStamp") ||
                    x.NewValue.Contains("SecurityStamp"));
                if (leakedIdentitySecret)
                    throw new InvalidOperationException("Identity secrets were written to the audit log.");

                if (!context.AuditLogs.Any(x => x.Action == "LoginLockedOut" && x.RecordId == storedUser.Id))
                    throw new InvalidOperationException("Account lockout was not explicitly audited.");
                foreach (var audit in context.AuditLogs.Where(x => x.TableName == "Authentication" && x.RecordId == storedUser.Id).ToList())
                {
                    var details = AuditValuePolicyTests.Read(audit.NewValue);
                    if (details.Count != 2 || details["Username"] != username || !details.ContainsKey("Outcome"))
                        throw new InvalidOperationException("Authentication audit did not store only structured username/outcome details.");
                }
            }
        }

        private static void LegacyAccountEnablesLockoutDuringSignIn()
        {
            const string username = "legacylockoutuser";
            using (var context = new POSContext(ConnectionString))
            using (var service = CreateUserService(context))
            {
                service.AddUserAsync(new UserDTO
                {
                    Username = username,
                    Password = "Legacy@123",
                    FirstName = "Legacy",
                    LastName = "Lockout"
                }).GetAwaiter().GetResult();

                context.Database.ExecuteSqlCommand(
                    "UPDATE dbo.Users SET LockoutEnabled = 0 WHERE UserName = @username",
                    new SqlParameter("@username", username));
                context.Entry(context.Users.Single(x => x.UserName == username)).Reload();

                var result = service.AuthenticateAsync(username, "Legacy@123")
                    .GetAwaiter().GetResult();
                if (!result.Succeeded ||
                    !context.Users.Single(x => x.UserName == username).LockoutEnabled)
                    throw new InvalidOperationException(
                        "A legacy account could not enable lockout tracking during sign-in.");
                if (context.AuditLogs.Any(x => x.TableName.Length > 20))
                    throw new InvalidOperationException("An EF proxy name leaked into the audit entity type.");
            }
        }

        private static void EnsureAuditTableExists(POSContext context)
        {
            context.Database.ExecuteSqlCommand(
                "IF OBJECT_ID('dbo.AuditLogs', 'U') IS NULL " +
                "BEGIN CREATE TABLE dbo.AuditLogs (" +
                "Id bigint IDENTITY(1,1) NOT NULL PRIMARY KEY, " +
                "TableName nvarchar(20) NULL, RecordId nvarchar(max) NULL, " +
                "Action nvarchar(max) NULL, OldValue nvarchar(max) NULL, " +
                "NewValue nvarchar(max) NULL, UserId nvarchar(36) NULL, " +
                "DateLogged datetime NOT NULL); END");
        }

        private static void PasswordChangeAndAdministrativeResetReplaceCredentials()
        {
            const string username = "passwordtest";
            using (var context = new POSContext(ConnectionString))
            using (var service = CreateUserService(context))
            {
                service.AddUserAsync(new UserDTO
                {
                    Username = username,
                    Password = "Original@123",
                    FirstName = "Password",
                    LastName = "Test"
                }).GetAwaiter().GetResult();

                var userId = context.Users.Single(x => x.UserName == username).Id;
                var wrongCurrent = service.ChangePasswordAsync(userId, "Wrong@123", "Changed@123")
                    .GetAwaiter().GetResult();
                if (wrongCurrent.Succeeded)
                    throw new InvalidOperationException("Password change accepted an incorrect current password.");

                var changed = service.ChangePasswordAsync(userId, "Original@123", "Changed@123")
                    .GetAwaiter().GetResult();
                if (!changed.Succeeded)
                    throw new InvalidOperationException("Valid password change failed.");

                if ((service.AuthenticateAsync(username, "Original@123").GetAwaiter().GetResult()).Succeeded ||
                    !(service.AuthenticateAsync(username, "Changed@123").GetAwaiter().GetResult()).Succeeded)
                    throw new InvalidOperationException("Changed password was not enforced.");

                var reset = service.ResetPasswordByAdministratorAsync(userId, "Reset@123")
                    .GetAwaiter().GetResult();
                if (!reset.Succeeded ||
                    (service.AuthenticateAsync(username, "Changed@123").GetAwaiter().GetResult()).Succeeded ||
                    !(service.AuthenticateAsync(username, "Reset@123").GetAwaiter().GetResult()).Succeeded)
                    throw new InvalidOperationException("Administrative password reset was not enforced.");

                if (!context.AuditLogs.Any(x => x.Action == "PasswordChanged" && x.RecordId == userId) ||
                    !context.AuditLogs.Any(x => x.Action == "PasswordReset" && x.RecordId == userId))
                    throw new InvalidOperationException("Password lifecycle events were not audited.");
            }
        }

        private static void LogoutClearsSessionAndWritesAudit()
        {
            const string userId = "logout-test-user";
            CurrentUser.Principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, "logouttest")
            }, "Test"));

            using (var context = new POSContext(ConnectionString))
            using (var service = CreateUserService(context))
            {
                service.Logout();
                if (CurrentUser.IsAuthenticated)
                    throw new InvalidOperationException("Logout did not clear the session.");
                if (!context.AuditLogs.Any(x => x.Action == "Logout" && x.RecordId == userId))
                    throw new InvalidOperationException("Logout was not audited.");
            }
        }

        private static void LockLifecycleClearsSessionAndWritesAudit()
        {
            const string userId = "lock-session-user";
            const string username = "locksession";
            CurrentUser.Principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, username)
            }, "Test"));

            using (var context = new POSContext(ConnectionString))
            using (var service = CreateUserService(context))
            {
                service.LockSession();
                if (CurrentUser.IsAuthenticated)
                    throw new InvalidOperationException("Locking did not clear the active session.");

                service.RecordSessionUnlocked(userId, username);
                if (!context.AuditLogs.Any(x => x.Action == "SessionLocked" && x.RecordId == userId) ||
                    !context.AuditLogs.Any(x => x.Action == "SessionUnlocked" && x.RecordId == userId))
                    throw new InvalidOperationException("Lock lifecycle events were not audited.");
            }
        }

        private static void DisabledAccountCannotAuthenticate()
        {
            const string username = "disabledtest";
            using (var context = new POSContext(ConnectionString))
            using (var service = CreateUserService(context))
            {
                service.AddUserAsync(new UserDTO
                {
                    Username = username,
                    Password = "Enabled@123",
                    FirstName = "Disabled",
                    LastName = "Test"
                }).GetAwaiter().GetResult();

                var userId = context.Users.Single(x => x.UserName == username).Id;
                var disabled = service.SetUserEnabledAsync(userId, false).GetAwaiter().GetResult();
                if (!disabled.Succeeded)
                    throw new InvalidOperationException("The test account could not be disabled.");

                var rejected = service.AuthenticateAsync(username, "Enabled@123").GetAwaiter().GetResult();
                if (rejected.Succeeded || !rejected.Errors[0].Contains("disabled"))
                    throw new InvalidOperationException("A disabled account was allowed to authenticate.");

                var enabled = service.SetUserEnabledAsync(userId, true).GetAwaiter().GetResult();
                var accepted = service.AuthenticateAsync(username, "Enabled@123").GetAwaiter().GetResult();
                if (!enabled.Succeeded || !accepted.Succeeded)
                    throw new InvalidOperationException("A re-enabled account could not authenticate.");

                if (!context.AuditLogs.Any(x => x.Action == "UserDisabled" && x.RecordId == userId) ||
                    !context.AuditLogs.Any(x => x.Action == "LoginDisabled" && x.RecordId == userId) ||
                    !context.AuditLogs.Any(x => x.Action == "UserEnabled" && x.RecordId == userId))
                    throw new InvalidOperationException("Account state events were not audited.");
            }
        }

        private static void AdministratorSafetyRulesAreEnforced()
        {
            using (var context = new POSContext(ConnectionString))
            using (var service = CreateUserService(context))
            {
                service.AddRoleAsync(new RoleDTO { Name = "System Administrator" })
                    .GetAwaiter().GetResult();
                service.AddRoleAsync(new RoleDTO { Name = "Cashier" })
                    .GetAwaiter().GetResult();

                var administratorRoleId = context.Roles
                    .Single(x => x.Name == "System Administrator").Id;
                var cashierRoleId = context.Roles.Single(x => x.Name == "Cashier").Id;

                service.AddUserAsync(new UserDTO
                {
                    Username = "safetyadminone",
                    Password = "Safety@123",
                    FirstName = "Safety",
                    LastName = "Admin One",
                    Role = administratorRoleId
                }).GetAwaiter().GetResult();

                var firstAdministratorId = context.Users
                    .Single(x => x.UserName == "safetyadminone").Id;
                CurrentUser.Principal = CreatePrincipal(firstAdministratorId, "safetyadminone");

                var selfDisable = service.SetUserEnabledAsync(firstAdministratorId, false)
                    .GetAwaiter().GetResult();
                if (selfDisable.Succeeded)
                    throw new InvalidOperationException("An administrator disabled their own account.");

                CurrentUser.Principal = CreatePrincipal("different-operator", "differentoperator");
                var finalDisable = service.SetUserEnabledAsync(firstAdministratorId, false)
                    .GetAwaiter().GetResult();
                if (finalDisable.Succeeded)
                    throw new InvalidOperationException("The final active administrator was disabled.");

                var finalDemotion = service.AssignRoleToUserAsync(firstAdministratorId, cashierRoleId)
                    .GetAwaiter().GetResult();
                if (finalDemotion.Succeeded)
                    throw new InvalidOperationException("The final active administrator was demoted.");

                service.AddUserAsync(new UserDTO
                {
                    Username = "safetyadmintwo",
                    Password = "Safety@123",
                    FirstName = "Safety",
                    LastName = "Admin Two",
                    Role = administratorRoleId
                }).GetAwaiter().GetResult();

                var disableWithBackup = service.SetUserEnabledAsync(firstAdministratorId, false)
                    .GetAwaiter().GetResult();
                if (!disableWithBackup.Succeeded)
                    throw new InvalidOperationException(
                        "An administrator could not be disabled after another active administrator existed.");

                var secondAdministratorId = context.Users
                    .Single(x => x.UserName == "safetyadmintwo").Id;
                var assignedRoleId = context.Users.Single(x => x.Id == secondAdministratorId)
                    .Roles.Single().RoleId;
                if (assignedRoleId != administratorRoleId)
                    throw new InvalidOperationException("A selected role ID was not assigned to the user.");
            }

            CurrentUser.Logout();
        }

        private static ClaimsPrincipal CreatePrincipal(string userId, string username)
        {
            return new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, username)
            }, "Test"));
        }

        private static void ModuleHierarchyAndDeletionRulesAreEnforced()
        {
            using (var context = new POSContext(ConnectionString))
            using (var service = new ModuleService(context, new AllowAllAuthorizationService()))
            {
                var parent = new ModuleDTO { Name = "Integration Parent Module" };
                var child = new ModuleDTO
                {
                    Name = "Integration Child Module"
                };
                if (!service.AddModule(parent).Succeeded)
                    throw new InvalidOperationException("The parent module could not be created.");

                child.ParentModuleId = parent.ModuleId;
                if (!service.AddModule(child).Succeeded)
                    throw new InvalidOperationException("The child module could not be created.");

                child.Name = "Integration Child Module Updated";
                if (!service.UpdateModule(child).Succeeded)
                    throw new InvalidOperationException("The child module could not be updated.");
                AssertEqual(
                    parent.ModuleId,
                    context.Set<Module>().Single(x => x.Id == child.ModuleId).ParentModuleId.Value,
                    "Updated module parent");

                parent.ParentModuleId = child.ModuleId;
                if (service.UpdateModule(parent).Succeeded)
                    throw new InvalidOperationException("A cyclic module hierarchy was accepted.");
                parent.ParentModuleId = null;

                if (service.DeleteModule(parent).Succeeded)
                    throw new InvalidOperationException("A parent module with children was deleted.");

                var roleId = context.Roles.Select(x => x.Id).First();
                context.Set<RoleClaim>().Add(new RoleClaim
                {
                    Id = Guid.NewGuid().ToString(),
                    RoleId = roleId,
                    ModuleId = child.ModuleId
                });
                context.SaveChanges();
                if (service.DeleteModule(child).Succeeded)
                    throw new InvalidOperationException("A module used by permissions was deleted.");

                var unused = new ModuleDTO { Name = "Integration Unused Module" };
                service.AddModule(unused);
                if (!service.DeleteModule(unused).Succeeded ||
                    context.Set<Module>().Any(x => x.Id == unused.ModuleId))
                    throw new InvalidOperationException("An unused module was not deleted.");
            }
        }

        private static void UserSearchAndPaginationAreAppliedByTheService()
        {
            using (var context = new POSContext(ConnectionString))
            using (var service = CreateUserService(context))
            {
                foreach (var username in new[] { "pagealpha", "pagebeta", "pagegamma" })
                {
                    service.AddUserAsync(new UserDTO
                    {
                        Username = username,
                        Password = "Paging@123",
                        FirstName = username,
                        LastName = "Paging"
                    }).GetAwaiter().GetResult();
                }

                var firstPage = service.GetUsersPageAsync("page", 1, 2).GetAwaiter().GetResult();
                var secondPage = service.GetUsersPageAsync("page", 2, 2).GetAwaiter().GetResult();
                AssertEqual(3, firstPage.TotalCount, "Filtered user count");
                AssertEqual(2, firstPage.TotalPages, "Filtered user page count");
                AssertEqual(2, firstPage.Items.Count, "First user page size");
                AssertEqual(1, secondPage.Items.Count, "Second user page size");
                if (firstPage.Items[0].Username != "pagealpha" ||
                    firstPage.Items[1].Username != "pagebeta" ||
                    secondPage.Items[0].Username != "pagegamma")
                    throw new InvalidOperationException("User pagination ordering was not stable.");

                var roleSearch = service.GetUsersPageAsync("System Administrator", 1, 25)
                    .GetAwaiter().GetResult();
                AssertEqual(2, roleSearch.TotalCount, "Role-filtered user count");
                if (roleSearch.Items.Any(x => x.RoleName != "System Administrator"))
                    throw new InvalidOperationException("Role search returned an unrelated user.");
            }
        }

        private static void PermissionMatrixIsRoleScopedAndReadOnlyOnLoad()
        {
            using (var context = new POSContext(ConnectionString))
            using (var modules = new ModuleService(context, new AllowAllAuthorizationService()))
            using (var roles = new RoleService(context, new AllowAllAuthorizationService()))
            {
                var matrixModule = new ModuleDTO { Name = "Integration Matrix Module" };
                if (!modules.AddModule(matrixModule).Succeeded)
                    throw new InvalidOperationException("The permission matrix module could not be created.");

                var administratorRoleId = context.Roles
                    .Single(x => x.Name == "System Administrator").Id;
                var claimsBefore = context.Set<RoleClaim>().Count();
                var matrix = roles.GetClaims(administratorRoleId).ToList();
                AssertEqual(claimsBefore, context.Set<RoleClaim>().Count(), "Claims after matrix read");
                if (matrix.Any(x => x.RoleId != administratorRoleId))
                    throw new InvalidOperationException("The permission matrix mixed role identifiers.");

                var newPermission = matrix.Single(x => x.ModuleId == matrixModule.ModuleId);
                if (!string.IsNullOrEmpty(newPermission.RoleClaimId))
                    throw new InvalidOperationException("A missing permission was persisted while reading.");
                newPermission.CanView = true;
                roles.UpdateClaim(newPermission);

                var stored = context.Set<RoleClaim>().Single(x =>
                    x.RoleId == administratorRoleId && x.ModuleId == matrixModule.ModuleId);
                if (!stored.CanView)
                    throw new InvalidOperationException("The permission matrix change was not persisted.");
                var createdAudit = context.AuditLogs.Single(x =>
                    x.Action == "PermissionCreated" && x.RecordId == stored.Id);
                var createdDetails = AuditValuePolicyTests.Read(createdAudit.NewValue);
                if (createdDetails.Count != 6 || createdDetails["View"] != "True" || createdDetails["Add"] != "False")
                    throw new InvalidOperationException("A permission grant was not explicitly audited.");

                newPermission.CanAdd = true;
                roles.UpdateClaim(newPermission);
                var updatedAudit = context.AuditLogs.Single(x =>
                    x.Action == "PermissionUpdated" && x.RecordId == stored.Id);
                if (AuditValuePolicyTests.Read(updatedAudit.OldValue)["Add"] != "False" ||
                    AuditValuePolicyTests.Read(updatedAudit.NewValue)["Add"] != "True")
                    throw new InvalidOperationException("A permission change was not explicitly audited.");
            }
        }

        private static void RoleLifecycleSafetyIsEnforced()
        {
            using (var context = new POSContext(ConnectionString))
            using (var roles = new RoleService(context, new AllowAllAuthorizationService()))
            using (var users = CreateUserService(context))
            {
                var administrator = roles.GetRoles()
                    .Single(x => x.Name == "System Administrator");
                if (roles.UpdateRole(new RoleDTO
                    {
                        RoleId = administrator.RoleId,
                        Name = "Renamed Administrator"
                    }).Succeeded || roles.DeleteRole(administrator.RoleId).Succeeded)
                    throw new InvalidOperationException("The System Administrator role was not protected.");

                var assigned = new RoleDTO { Name = "Integration Assigned Role" };
                if (!roles.AddRole(assigned).Succeeded)
                    throw new InvalidOperationException("The assigned test role could not be created.");
                users.AddUserAsync(new UserDTO
                {
                    Username = "roleassigneduser",
                    Password = "RoleTest@123",
                    FirstName = "Role",
                    LastName = "Assigned",
                    Role = assigned.RoleId
                }).GetAwaiter().GetResult();
                if (roles.DeleteRole(assigned.RoleId).Succeeded)
                    throw new InvalidOperationException("A role assigned to a user was deleted.");

                var assignedUserId = context.Users
                    .Single(x => x.UserName == "roleassigneduser").Id;
                var cashierRoleId = context.Roles.Single(x => x.Name == "Cashier").Id;
                var reassigned = users.AssignRoleToUserAsync(assignedUserId, cashierRoleId)
                    .GetAwaiter().GetResult();
                var assignmentAudit = context.AuditLogs.Where(x =>
                    x.Action == "UserRoleAssigned" && x.RecordId == assignedUserId)
                    .OrderByDescending(x => x.Id).First();
                var previousRoles = AuditValuePolicyTests.Read<System.Collections.Generic.Dictionary<string, string[]>>(assignmentAudit.OldValue);
                if (!reassigned.Succeeded || !previousRoles["Roles"].Contains("Integration Assigned Role") ||
                    AuditValuePolicyTests.Read(assignmentAudit.NewValue)["Role"] != "Cashier")
                    throw new InvalidOperationException("Role reassignment was not persisted and audited.");
                var activity = users.GetUserActivityAsync(assignedUserId, 10)
                    .GetAwaiter().GetResult();
                if (!activity.Any(x => x.Action == "UserRoleAssigned") ||
                    activity.Any(x => x.Details != null && x.Details.Contains("PasswordHash")))
                    throw new InvalidOperationException("The user activity view was incomplete or exposed a secret.");

                var unused = new RoleDTO { Name = "Integration Unused Role" };
                if (!roles.AddRole(unused).Succeeded ||
                    roles.AddRole(new RoleDTO { Name = unused.Name }).Succeeded)
                    throw new InvalidOperationException("Role name validation was not enforced.");
                if (!roles.UpdateRole(new RoleDTO
                    {
                        RoleId = unused.RoleId,
                        Name = "Integration Renamed Role"
                    }).Succeeded)
                    throw new InvalidOperationException("An unused role could not be renamed.");
                if (!roles.DeleteRole(unused.RoleId).Succeeded ||
                    context.Roles.Any(x => x.Id == unused.RoleId))
                    throw new InvalidOperationException("An unused role could not be deleted.");
            }
        }

        private static void TaxChangesCreateEffectiveDatedHistory()
        {
            using (var context = new POSContext(ConnectionString))
            using (var service = new StoreSettingsService(
                context,
                new SystemClock(),
                new AllowAllAuthorizationService()))
            {
                var settings = service.GetSettings();
                var originalTaxId = settings.TaxRateId;

                settings.TaxRate = 0.15m;
                service.SaveSettings(settings);
                var updated = service.GetSettings();
                var original = context.TaxRates.Single(x => x.Id == originalTaxId);
                var replacement = context.TaxRates.Single(x => x.Id == updated.TaxRateId);
                if (updated.TaxRateId == originalTaxId || original.IsActive ||
                    !original.EffectiveToUtc.HasValue || !replacement.IsActive ||
                    replacement.Rate != 0.15m ||
                    context.StoreSettings.Single().DefaultTaxRateId != replacement.Id)
                    throw new InvalidOperationException("A tax change rewrote history or selected the wrong default.");

                var history = service.GetTaxHistory();
                AssertEqual(2, history.Count, "Tax history count");
                if (history[0].TaxRateId != replacement.Id)
                    throw new InvalidOperationException("Tax history did not return the latest version first.");
            }
        }

        private static void SupplierMaintenanceRejectsStaleRevisions()
        {
            int supplierId;
            string staleRevision;

            using (var context = new POSContext(ConnectionString))
            {
                var service = new SupplierService(context, new AllowAllAuthorizationService());
                supplierId = service.Save(new SupplierDTO
                {
                    Code = " test-supplier ",
                    Name = "Test Supplier",
                    ContactName = "Initial Contact",
                    Email = "supplier@example.test"
                });

                var created = service.GetDetails(supplierId);
                if (created.Code != "TEST-SUPPLIER" || !created.IsActive ||
                    string.IsNullOrWhiteSpace(created.Revision))
                    throw new InvalidOperationException("Supplier creation did not normalize and return lifecycle metadata.");

                staleRevision = created.Revision;
                created.Name = "Updated Test Supplier";
                service.Save(created);

                AssertThrows<ValidationException>(
                    () => service.SetActive(supplierId, false, staleRevision),
                    "Stale supplier revision");

                var current = service.GetDetails(supplierId);
                service.SetActive(supplierId, false, current.Revision);
            }

            using (var verification = new POSContext(ConnectionString))
            {
                var supplier = verification.Suppliers.AsNoTracking().Single(x => x.Id == supplierId);
                if (supplier.Name != "Updated Test Supplier" || supplier.IsActive)
                    throw new InvalidOperationException("Supplier update or deactivation was not persisted.");
            }
        }

        private static void PurchaseOrderLifecycleRejectsStaleRevisions()
        {
            int orderId;
            using (var context = new POSContext(ConnectionString))
            {
                var authorization = new AllowAllAuthorizationService();
                var suppliers = new SupplierService(context, authorization);
                var supplierId = suppliers.Save(new SupplierDTO
                {
                    Code = "PO-SUPPLIER",
                    Name = "Purchase Order Supplier"
                });
                var productId = context.Set<Product>().AsNoTracking()
                    .Where(product => product.IsActive)
                    .Select(product => product.Id)
                    .First();
                var orders = new PurchaseOrderService(context, new SystemClock(), authorization);

                orderId = orders.CreateDraft(new PurchaseOrderDraftDTO
                {
                    OrderNumber = " po-test-001 ",
                    SupplierId = supplierId,
                    Items =
                    {
                        new PurchaseOrderLineInputDTO
                        {
                            ProductId = productId,
                            Quantity = 2,
                            UnitCost = 15m
                        }
                    }
                });

                var original = orders.GetDetails(orderId);
                if (original.Order.OrderNumber != "PO-TEST-001" ||
                    original.Order.Status != DocumentStatus.Draft.ToString() ||
                    original.Order.TotalAmount != 30m || original.Items.Single().RemainingQuantity != 2)
                    throw new InvalidOperationException("Purchase-order draft facts were not persisted correctly.");

                orders.UpdateDraft(new PurchaseOrderDraftUpdateDTO
                {
                    PurchaseOrderId = orderId,
                    Revision = original.Revision,
                    SupplierId = supplierId,
                    Items =
                    {
                        new PurchaseOrderLineInputDTO
                        {
                            ProductId = productId,
                            Quantity = 3,
                            UnitCost = 15m
                        }
                    }
                });

                AssertThrows<ValidationException>(() => orders.OrderDraft(new PurchaseOrderTransitionDTO
                {
                    PurchaseOrderId = orderId,
                    Revision = original.Revision
                }), "Stale purchase-order transition");

                var reviewed = orders.GetDetails(orderId);
                orders.OrderDraft(new PurchaseOrderTransitionDTO
                {
                    PurchaseOrderId = orderId,
                    Revision = reviewed.Revision
                });
            }

            using (var context = new POSContext(ConnectionString))
            {
                var persisted = context.PurchaseOrders.Include(order => order.Lines).Single(order => order.Id == orderId);
                if (persisted.Status != DocumentStatus.Pending || !persisted.OrderedUtc.HasValue ||
                    persisted.TotalAmount != 45m || persisted.Lines.Single().OrderedQuantity != 3)
                    throw new InvalidOperationException("Purchase-order ordering did not persist the reviewed draft.");
            }
        }

        private static void PartialGoodsReceiptIsIdempotentAndUpdatesInventory()
        {
            int orderId;
            int productId;
            int quantityBefore;
            int receiptId;

            using (var context = new POSContext(ConnectionString))
            {
                var order = context.PurchaseOrders.Include(candidate => candidate.Lines)
                    .Single(candidate => candidate.OrderNumber == "PO-TEST-001");
                var line = order.Lines.Single();
                orderId = order.Id;
                productId = line.ProductId;
                quantityBefore = context.InventoryBalances.AsNoTracking()
                    .Single(balance => balance.ProductId == productId).QuantityOnHand;

                var service = new GoodsReceiptService(
                    context,
                    new SystemClock(),
                    new TestCurrentUser(),
                    new AllowAllAuthorizationService());
                var request = new PurchaseOrderReceiptDTO
                {
                    ReceiptNumber = " gr-test-001 ",
                    PurchaseOrderId = orderId,
                    SupplierReference = "SUPPLIER-INV-001",
                    Items =
                    {
                        new GoodsReceiptLineInputDTO { ProductId = productId, Quantity = 1 }
                    }
                };

                receiptId = service.ReceivePurchaseOrder(request);
                var replayId = service.ReceivePurchaseOrder(request);
                if (replayId != receiptId)
                    throw new InvalidOperationException("An identical goods-receipt replay created another document.");

                AssertThrows<ValidationException>(() => service.ReceivePurchaseOrder(new PurchaseOrderReceiptDTO
                {
                    ReceiptNumber = "GR-TEST-001",
                    PurchaseOrderId = orderId,
                    SupplierReference = "SUPPLIER-INV-001",
                    Items =
                    {
                        new GoodsReceiptLineInputDTO { ProductId = productId, Quantity = 2 }
                    }
                }), "Conflicting goods-receipt replay");
            }

            using (var context = new POSContext(ConnectionString))
            {
                var order = context.PurchaseOrders.Include(candidate => candidate.Lines)
                    .Single(candidate => candidate.Id == orderId);
                var balance = context.InventoryBalances.Single(candidate => candidate.ProductId == productId);
                var product = context.Set<Product>().Single(candidate => candidate.Id == productId);
                var movement = context.StockMovements.Single(candidate =>
                    candidate.MovementType == StockMovementType.PurchaseReceipt &&
                    candidate.ReferenceId == "GR-TEST-001");

                if (context.GoodsReceipts.Count(candidate => candidate.Id == receiptId) != 1 ||
                    order.Status != DocumentStatus.Pending || order.Lines.Single().ReceivedQuantity != 1 ||
                    balance.QuantityOnHand != quantityBefore + 1 || product.Quantity != balance.QuantityOnHand ||
                    movement.QuantityDelta != 1)
                    throw new InvalidOperationException("Partial receiving did not persist one reconciled receipt and inventory movement.");
            }
        }

        private static void PurchaseReturnIsIdempotentAndReversesInventory()
        {
            int receiptId;
            int receiptLineId;
            int productId;
            int quantityBefore;
            int returnId;

            using (var context = new POSContext(ConnectionString))
            {
                var receipt = context.GoodsReceipts.Include(candidate => candidate.Lines)
                    .Single(candidate => candidate.ReceiptNumber == "GR-TEST-001");
                var receiptLine = receipt.Lines.Single();
                receiptId = receipt.Id;
                receiptLineId = receiptLine.Id;
                productId = receiptLine.ProductId;
                quantityBefore = context.InventoryBalances.AsNoTracking()
                    .Single(balance => balance.ProductId == productId).QuantityOnHand;

                var service = new PurchaseReturnService(
                    context,
                    new SystemClock(),
                    new TestCurrentUser(),
                    new AllowAllAuthorizationService());
                var request = new PurchaseReturnPostDTO
                {
                    ReturnNumber = " pr-test-001 ",
                    GoodsReceiptId = receiptId,
                    Reason = "Damaged supplier delivery",
                    Items =
                    {
                        new PurchaseReturnLineInputDTO
                        {
                            GoodsReceiptLineId = receiptLineId,
                            Quantity = 1
                        }
                    }
                };

                returnId = service.Post(request);
                if (service.Post(request) != returnId)
                    throw new InvalidOperationException("An identical purchase-return replay created another document.");

                AssertThrows<ValidationException>(() => service.Post(new PurchaseReturnPostDTO
                {
                    ReturnNumber = "PR-TEST-001",
                    GoodsReceiptId = receiptId,
                    Reason = "Changed reason",
                    Items =
                    {
                        new PurchaseReturnLineInputDTO
                        {
                            GoodsReceiptLineId = receiptLineId,
                            Quantity = 1
                        }
                    }
                }), "Conflicting purchase-return replay");
            }

            using (var context = new POSContext(ConnectionString))
            {
                var receipt = context.GoodsReceipts.Include(candidate => candidate.Lines)
                    .Single(candidate => candidate.Id == receiptId);
                var purchaseReturn = context.PurchaseReturns.Include(candidate => candidate.Lines)
                    .Single(candidate => candidate.Id == returnId);
                var balance = context.InventoryBalances.Single(candidate => candidate.ProductId == productId);
                var product = context.Set<Product>().Single(candidate => candidate.Id == productId);
                var movement = context.StockMovements.Single(candidate =>
                    candidate.MovementType == StockMovementType.PurchaseReturn &&
                    candidate.ReferenceId == "PR-TEST-001");

                if (context.PurchaseReturns.Count(candidate => candidate.ReturnNumber == "PR-TEST-001") != 1 ||
                    receipt.Status != DocumentStatus.Returned || receipt.Lines.Single().Quantity != 1 ||
                    purchaseReturn.Status != DocumentStatus.Posted || purchaseReturn.TotalAmount != 15m ||
                    purchaseReturn.Lines.Single().Quantity != 1 ||
                    balance.QuantityOnHand != quantityBefore - 1 || product.Quantity != balance.QuantityOnHand ||
                    movement.QuantityDelta != -1)
                    throw new InvalidOperationException("Purchase return did not preserve the receipt and post one reconciled reversal.");
            }
        }

        private static void ManualAdjustmentIsIdempotentAndRequiresReviewedQuantity()
        {
            int productId;
            int quantityBefore;
            Guid requestId = Guid.NewGuid();
            long movementId;

            using (var context = new POSContext(ConnectionString))
            using (var service = new InventoryLedgerService(
                context,
                new SystemClock(),
                new TestCurrentUser(),
                new AllowAllAuthorizationService()))
            {
                productId = context.Set<Product>().AsNoTracking()
                    .Where(product => product.Sku == "ROLLBACK-001")
                    .Select(product => product.Id)
                    .Single();
                quantityBefore = context.InventoryBalances.AsNoTracking()
                    .Single(balance => balance.ProductId == productId).QuantityOnHand;
                var request = new StockAdjustmentDTO
                {
                    RequestId = requestId,
                    ProductId = productId,
                    ExpectedQuantityOnHand = quantityBefore,
                    QuantityDelta = 2,
                    Reason = "Verified integration adjustment"
                };

                movementId = service.PostAdjustment(request).Id;
                if (service.PostAdjustment(request).Id != movementId)
                    throw new InvalidOperationException("An identical stock-adjustment replay created another movement.");

                AssertThrows<InvalidOperationException>(() => service.PostAdjustment(new StockAdjustmentDTO
                {
                    RequestId = requestId,
                    ProductId = productId,
                    ExpectedQuantityOnHand = quantityBefore,
                    QuantityDelta = 1,
                    Reason = "Verified integration adjustment"
                }), "Conflicting stock-adjustment request reuse");

                AssertThrows<InvalidOperationException>(() => service.PostAdjustment(new StockAdjustmentDTO
                {
                    RequestId = Guid.NewGuid(),
                    ProductId = productId,
                    ExpectedQuantityOnHand = quantityBefore,
                    QuantityDelta = 1,
                    Reason = "Stale reviewed quantity"
                }), "Stale stock-adjustment quantity");
            }

            using (var context = new POSContext(ConnectionString))
            {
                var requestReference = requestId.ToString("D");
                var balance = context.InventoryBalances.Single(candidate => candidate.ProductId == productId);
                var product = context.Set<Product>().Single(candidate => candidate.Id == productId);
                var movements = context.StockMovements.Where(candidate =>
                    candidate.MovementType == StockMovementType.Adjustment &&
                    candidate.ReferenceId == requestReference).ToList();
                var ledgerQuantity = context.StockMovements.Where(candidate => candidate.ProductId == productId)
                    .Sum(candidate => (long)candidate.QuantityDelta);

                if (movements.Count != 1 || movements[0].Id != movementId || movements[0].QuantityDelta != 2 ||
                    balance.QuantityOnHand != quantityBefore + 2 || product.Quantity != balance.QuantityOnHand ||
                    ledgerQuantity != balance.QuantityOnHand)
                    throw new InvalidOperationException("Manual adjustment did not preserve idempotency and inventory reconciliation.");
            }
        }

        private static void CheckoutReplayDoesNotDuplicateSaleOrStockMovement()
        {
            using (var context = new POSContext(ConnectionString))
            using (var service = CreateSalesService(context))
            {
                var productId = context.Set<Product>().AsNoTracking()
                    .Where(x => x.Sku == "ROLLBACK-001").Select(x => x.Id).Single();
                var requestId = Guid.NewGuid();
                var first = CreateTestSale(context, 1000m);
                first.RequestId = requestId;
                first.SaleItems.Add(new SaleItem { ProductId = productId, Quantity = 1 });
                var completed = service.CreateSale(first);
                var quantityAfterFirst = context.InventoryBalances.AsNoTracking()
                    .Single(x => x.ProductId == productId).QuantityOnHand;
                var movementCount = context.StockMovements.AsNoTracking()
                    .Count(x => x.ReferenceType == "Sale" && x.ReferenceId == completed.ReceiptNumber);

                var replay = CreateTestSale(context, 1000m);
                replay.RequestId = requestId;
                replay.SaleItems.Add(new SaleItem { ProductId = productId, Quantity = 1 });
                var returned = service.CreateSale(replay);

                AssertEqual(completed.Id, returned.Id, "Idempotent checkout sale ID");
                AssertEqual(1, context.Sales.AsNoTracking().Count(x => x.RequestId == requestId),
                    "Idempotent checkout sale count");
                AssertEqual(quantityAfterFirst, context.InventoryBalances.AsNoTracking()
                    .Single(x => x.ProductId == productId).QuantityOnHand,
                    "Idempotent checkout inventory quantity");
                AssertEqual(movementCount, context.StockMovements.AsNoTracking()
                    .Count(x => x.ReferenceType == "Sale" && x.ReferenceId == completed.ReceiptNumber),
                    "Idempotent checkout movement count");
            }
        }

        private static void ReturnPostingIsIdempotentAndRestocksInventory()
        {
            int saleId;
            int productId;
            using (var context = new POSContext(ConnectionString))
            using (var sales = CreateSalesService(context))
            {
                productId = context.Set<Product>().AsNoTracking()
                    .Where(x => x.Sku == "ROLLBACK-001").Select(x => x.Id).Single();
                var sale = CreateTestSale(context, 1000m);
                sale.SaleItems.Add(new SaleItem { ProductId = productId, Quantity = 1 });
                saleId = sales.CreateSale(sale).Id;
            }

            using (var context = new POSContext(ConnectionString))
            using (var returns = new SaleReturnService(context, new SystemClock(),
                new TestCurrentUser(), new AllowAllAuthorizationService()))
            {
                var before = context.InventoryBalances.AsNoTracking()
                    .Single(x => x.ProductId == productId).QuantityOnHand;
                var eligibility = returns.GetEligibility(saleId);
                var request = new SaleReturnPostDTO
                {
                    RequestId = Guid.NewGuid(), SaleId = saleId,
                    RegisterStationId = context.CashierShifts.AsNoTracking()
                        .Single(x => x.Status == ShiftStatus.Open).RegisterStationId,
                    CashierShiftId = context.CashierShifts.AsNoTracking()
                        .Single(x => x.Status == ShiftStatus.Open).Id,
                    Reason = "Integration return verification"
                };
                request.Lines.Add(new SaleReturnLinePostDTO
                {
                    SaleItemId = eligibility.Lines.Single().SaleItemId,
                    Quantity = 1, Disposition = ReturnDisposition.Restock
                });
                request.Refunds.Add(new RefundTenderPostDTO
                {
                    OriginalPaymentId = eligibility.Payments.Single().OriginalPaymentId,
                    Amount = eligibility.OriginalTotal
                });

                var posted = returns.Post(request);
                var replay = returns.Post(request);
                AssertEqual(posted.Id, replay.Id, "Idempotent return ID");
                AssertEqual(1, context.SaleReturns.AsNoTracking().Count(x => x.RequestId == request.RequestId),
                    "Idempotent return count");
                AssertEqual(before + 1, context.InventoryBalances.AsNoTracking()
                    .Single(x => x.ProductId == productId).QuantityOnHand,
                    "Return restocked quantity");
            }
        }

        private static void ManagementReportsReconcileWithPersistedFacts()
        {
            using (var context = new POSContext(ConnectionString))
            using (var reports = new ManagementReportService(context,
                new AllowAllAuthorizationService(), new SystemClock()))
            {
                var filter = new ManagementFilterDTO
                {
                    FromUtc = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(-2), DateTimeKind.Utc),
                    ToUtcExclusive = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(2), DateTimeKind.Utc),
                    MaximumRows = 500
                };
                var report = reports.GetReport(filter);
                var dashboard = reports.GetDashboard(filter);
                if (report.Financials.GrossSales != dashboard.Financials.GrossSales ||
                    report.Financials.Refunds != dashboard.Financials.Refunds ||
                    report.Financials.NetSales != report.Financials.GrossSales - report.Financials.Refunds ||
                    report.Financials.NetSales != dashboard.Financials.NetSales)
                    throw new InvalidOperationException("Dashboard and report financial totals do not reconcile.");
                AssertEqual(report.Sales.Count, report.Financials.TransactionCount,
                    "Management report transaction count");
            }
        }

        private static void CashierShiftLifecycleIsIdempotentAndDerived()
        {
            using (var context = new POSContext(ConnectionString))
            {
                var existing = context.CashierShifts.Single(x => x.Status == ShiftStatus.Open);
                existing.Status = ShiftStatus.Closed;
                existing.ClosedUtc = DateTime.UtcNow;
                context.SaveChanges();
            }

            using (var context = new POSContext(ConnectionString))
            {
                var service = new CashierShiftService(context, new SystemClock(),
                    new TestCurrentUser(), new AllowAllAuthorizationService());
                var registerId = context.RegisterStations.AsNoTracking().Select(x => x.Id).Single();
                var open = new OpenCashierShiftDTO
                {
                    RequestId = Guid.NewGuid(), RegisterStationId = registerId, OpeningCash = 500m
                };
                var shiftId = service.Open(open);
                AssertEqual(shiftId, service.Open(open), "Idempotent shift opening");
                var details = service.GetDetails(shiftId);
                var movement = new PostCashMovementDTO
                {
                    RequestId = Guid.NewGuid(), CashierShiftId = shiftId,
                    ShiftRevision = details.Revision, IsCashIn = true,
                    Amount = 25m, Reason = "Integration cash-in"
                };
                var movementId = service.PostCashMovement(movement);
                AssertEqual((int)movementId, (int)service.PostCashMovement(movement),
                    "Idempotent cash movement");
                details = service.GetDetails(shiftId);
                if (details.ExpectedCash != 525m)
                    throw new InvalidOperationException("Expected cash was not derived from opening cash and movements.");
                var close = new CloseCashierShiftDTO
                {
                    RequestId = Guid.NewGuid(), CashierShiftId = shiftId,
                    ShiftRevision = details.Revision, CountedCash = 525m
                };
                service.Close(close);
                service.Close(close);
                details = service.GetDetails(shiftId);
                if (details.Status != ShiftStatus.Closed.ToString() || details.Variance != 0m)
                    throw new InvalidOperationException("Shift close did not persist the expected state and variance.");
            }
        }

        private static void AssertEqual(int expected, int actual, string testName)
        {
            if (expected != actual)
                throw new InvalidOperationException(
                    $"{testName} failed: expected {expected}, actual {actual}.");
        }

        private static UserService CreateUserService(POSContext context)
        {
            return new UserService(context, new SystemClock(), new AllowAllAuthorizationService());
        }

        private static SalesService CreateSalesService(POSContext context, IClock clock = null)
        {
            return new SalesService(
                context,
                clock ?? new SystemClock(),
                new TestCurrentUser(),
                new AllowAllAuthorizationService());
        }

        private static Sale CreateTestSale(POSContext context, decimal cashReceived)
        {
            var shift = context.CashierShifts.AsNoTracking().Single(x => x.Status == ShiftStatus.Open);
            var sale = new Sale
            {
                RequestId = Guid.NewGuid(),
                RegisterStationId = shift.RegisterStationId,
                CashierShiftId = shift.Id,
                CashReceived = cashReceived
            };
            if (cashReceived > 0)
            {
                sale.Payments.Add(new Payment
                {
                    TenderType = TenderType.Cash,
                    Amount = cashReceived,
                    Status = PaymentStatus.Pending
                });
            }
            return sale;
        }

        private sealed class TestCurrentUser : ICurrentUser
        {
            private static readonly ClaimsPrincipal TestPrincipal = new ClaimsPrincipal(
                new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "integration-user") }, "Test"));

            public ClaimsPrincipal Principal => TestPrincipal;
            public bool IsAuthenticated => true;
            public string UserId => "integration-user-id";
            public string Username => "integration-user";
            public string RoleId => "integration-role-id";
            public string RoleName => "System Administrator";
        }

        private sealed class AllowAllAuthorizationService : IAuthorizationService
        {
            public bool HasPermission(string resource, ClaimActionType action) => true;
        }

        private static void AssertThrows<TException>(Action action, string testName)
            where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException)
            {
                return;
            }

            throw new InvalidOperationException(
                $"{testName} failed: expected {typeof(TException).Name}.");
        }
    }
}
