using Microsoft.AspNet.Identity.EntityFramework;
using POS.Core;
using POS.Core.Abstractions;
using POS.Domains.AuditEntry;
using POS.Domains.Security;
using POS.Domains.BusinessObjects;
using POS.Domains.Operations;
using POS.Data.Auditing;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Core.Objects;
using System.Linq;
using System.Security.AccessControl;
using System.Threading;
using System.Threading.Tasks;

namespace POS.Data.Context
{
    public class POSContext : IdentityDbContext<User, Role, string, IdentityUserLogin, IdentityUserRole, IdentityUserClaim>
    {
        private readonly IClock _clock;
        private readonly ICurrentUser _currentUser;

        public POSContext() : this("POSConnection", new SystemClock(), new CurrentUserAccessor())
        {
        }

        public POSContext(string nameOrConnectionString)
            : this(nameOrConnectionString, new SystemClock(), new CurrentUserAccessor())
        {
        }

        public POSContext(string nameOrConnectionString, IClock clock, ICurrentUser currentUser)
            : base(nameOrConnectionString)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<Sale> Sales { get; set; }
        public DbSet<SaleItem> SaleItems { get; set; }
        public DbSet<StoreSetting> StoreSettings { get; set; }
        public DbSet<TaxRate> TaxRates { get; set; }
        public DbSet<RegisterStation> RegisterStations { get; set; }
        public DbSet<NumberSequence> NumberSequences { get; set; }
        public DbSet<InventoryBalance> InventoryBalances { get; set; }
        public DbSet<StockMovement> StockMovements { get; set; }
        public DbSet<StockCount> StockCounts { get; set; }
        public DbSet<StockCountLine> StockCountLines { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<SupplierProduct> SupplierProducts { get; set; }
        public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
        public DbSet<PurchaseOrderLine> PurchaseOrderLines { get; set; }
        public DbSet<GoodsReceipt> GoodsReceipts { get; set; }
        public DbSet<GoodsReceiptLine> GoodsReceiptLines { get; set; }
        public DbSet<PurchaseReturn> PurchaseReturns { get; set; }
        public DbSet<PurchaseReturnLine> PurchaseReturnLines { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<SaleReturn> SaleReturns { get; set; }
        public DbSet<SaleReturnLine> SaleReturnLines { get; set; }
        public DbSet<RefundPayment> RefundPayments { get; set; }
        public DbSet<CashierShift> CashierShifts { get; set; }
        public DbSet<CashMovement> CashMovements { get; set; }
        protected override void OnModelCreating(System.Data.Entity.DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Configurations.Add(new Configurations.UserConfiguration());
            modelBuilder.Configurations.Add(new Configurations.RoleConfiguration());
            modelBuilder.Configurations.Add(new Configurations.InventoryConfiguration());
            modelBuilder.Configurations.Add(new Configurations.ProductConfiguration());
            modelBuilder.Configurations.Add(new Configurations.StockConfiguration());
            modelBuilder.Configurations.Add(new Configurations.RoleClaimConfig());
            modelBuilder.Configurations.Add(new Configurations.AuditLogConfiguration());
            modelBuilder.Configurations.Add(new Configurations.POSConfiguration());
            modelBuilder.Configurations.Add(new Configurations.SaleConfiguration());
            modelBuilder.Configurations.Add(new Configurations.StoreSettingConfiguration());
            modelBuilder.Configurations.Add(new Configurations.TaxRateConfiguration());
            modelBuilder.Configurations.Add(new Configurations.RegisterStationConfiguration());
            modelBuilder.Configurations.Add(new Configurations.NumberSequenceConfiguration());
            modelBuilder.Configurations.Add(new Configurations.InventoryBalanceConfiguration());
            modelBuilder.Configurations.Add(new Configurations.StockMovementConfiguration());
            modelBuilder.Configurations.Add(new Configurations.StockCountConfiguration());
            modelBuilder.Configurations.Add(new Configurations.SupplierConfiguration());
            modelBuilder.Configurations.Add(new Configurations.SupplierProductConfiguration());
            modelBuilder.Configurations.Add(new Configurations.PurchaseOrderConfiguration());
            modelBuilder.Configurations.Add(new Configurations.PurchaseOrderLineConfiguration());
            modelBuilder.Configurations.Add(new Configurations.GoodsReceiptConfiguration());
            modelBuilder.Configurations.Add(new Configurations.GoodsReceiptLineConfiguration());
            modelBuilder.Configurations.Add(new Configurations.PurchaseReturnConfiguration());
            modelBuilder.Configurations.Add(new Configurations.PurchaseReturnLineConfiguration());
            modelBuilder.Configurations.Add(new Configurations.CustomerConfiguration());
            modelBuilder.Configurations.Add(new Configurations.PaymentConfiguration());
            modelBuilder.Configurations.Add(new Configurations.SaleReturnConfiguration());
            modelBuilder.Configurations.Add(new Configurations.SaleReturnLineConfiguration());
            modelBuilder.Configurations.Add(new Configurations.RefundPaymentConfiguration());
            modelBuilder.Configurations.Add(new Configurations.CashierShiftConfiguration());
            modelBuilder.Configurations.Add(new Configurations.CashMovementConfiguration());

            modelBuilder.Entity<User>().ToTable("Users");
            modelBuilder.Entity<Role>().ToTable("Roles");
            modelBuilder.Entity<IdentityUserRole>().ToTable("UserRoles");
            modelBuilder.Entity<IdentityUserClaim>().ToTable("UserClaims");
            modelBuilder.Entity<IdentityUserLogin>().ToTable("UserLogins");

        }

        public override int SaveChanges()
        {
            OnBeforeSaveChanges();
            return base.SaveChanges();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            OnBeforeSaveChanges();
            return await base.SaveChangesAsync(cancellationToken);
        }

        private void OnBeforeSaveChanges()
        {
            // Validate the whole pending operation before adding generated audit entries
            // or allowing EF to persist any accompanying business changes.
            if (ChangeTracker.Entries<StockMovement>().Any(entry =>
                entry.State == EntityState.Modified || entry.State == EntityState.Deleted))
                throw new InvalidOperationException(
                    "Stock movements are append-only. Post a correcting movement instead of editing or deleting history.");

            if (ChangeTracker.Entries<AuditLog>().Any(entry =>
                entry.State == EntityState.Modified || entry.State == EntityState.Deleted))
                throw new InvalidOperationException(
                    "Audit records are append-only and cannot be edited or deleted.");

            var auditLogs = new List<AuditLog>();
            var correlationId = AuditOperation.CurrentId ?? Guid.NewGuid();
            var register = ResolveAuditRegister();
            foreach (var entry in ChangeTracker.Entries<AuditLog>().Where(x => x.State == EntityState.Added))
            {
                entry.Entity.CorrelationId = correlationId;
                entry.Entity.RegisterStationId = register?.Id;
                entry.Entity.RegisterCode = register?.Code;
            }

            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.Entity is AuditLog)
                    continue;

                if (entry.State != EntityState.Added &&
                    entry.State != EntityState.Modified &&
                    entry.State != EntityState.Deleted)
                    continue;

                var audit = new AuditLog
                {
                    TableName = ObjectContext.GetObjectType(entry.Entity.GetType()).Name,
                    Action = entry.State.ToString(),
                    UserId = _currentUser.UserId,
                    DateLogged = _clock.UtcNow,
                    CorrelationId = correlationId,
                    RegisterStationId = register?.Id,
                    RegisterCode = register?.Code
                };

                if (entry.State == EntityState.Modified)
                {
                    var changed = entry.CurrentValues.PropertyNames.Where(p =>
                        !Equals(entry.OriginalValues[p], entry.CurrentValues[p])).ToArray();
                    audit.OldValue = AuditValuePolicy.Serialize(ObjectContext.GetObjectType(entry.Entity.GetType()),
                        changed, p => entry.OriginalValues[p]);
                    audit.NewValue = AuditValuePolicy.Serialize(ObjectContext.GetObjectType(entry.Entity.GetType()),
                        changed, p => entry.CurrentValues[p]);
                }
                else if (entry.State == EntityState.Added)
                {
                    audit.NewValue = AuditValuePolicy.Serialize(ObjectContext.GetObjectType(entry.Entity.GetType()),
                        entry.CurrentValues.PropertyNames, p => entry.CurrentValues[p]);
                }
                else if (entry.State == EntityState.Deleted)
                {
                    audit.OldValue = AuditValuePolicy.Serialize(ObjectContext.GetObjectType(entry.Entity.GetType()),
                        entry.OriginalValues.PropertyNames, p => entry.OriginalValues[p]);
                }

                auditLogs.Add(audit);
            }

            AuditLogs.AddRange(auditLogs);
        }

        private RegisterStation ResolveAuditRegister()
        {
            var registerId = AuditOperation.CurrentRegisterId;
            if (registerId.HasValue)
            {
                var register = RegisterStations.AsNoTracking().SingleOrDefault(x => x.Id == registerId.Value && x.IsActive);
                if (register == null)
                    throw new InvalidOperationException("The operation's register is missing or inactive.");
                return register;
            }
            // The current deployment supports a single register. Do not guess an origin
            // when several active registers exist or before initial configuration.
            var active = RegisterStations.AsNoTracking().Where(x => x.IsActive).Take(2).ToList();
            return active.Count == 1 ? active[0] : null;
        }

    }
}
