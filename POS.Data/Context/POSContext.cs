using Microsoft.AspNet.Identity.EntityFramework;
using POS.Core;
using POS.Domains.AuditEntry;
using POS.Domains.Security;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Security.AccessControl;
using System.Threading;
using System.Threading.Tasks;

namespace POS.Data.Context
{
    public class POSContext : IdentityDbContext<User, Role, string, IdentityUserLogin, IdentityUserRole, IdentityUserClaim>
    {
        public POSContext() : base("POSConnection")
        {
        }
        public DbSet<AuditLog> AuditLogs { get; set; }
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
            var auditLogs = new List<AuditLog>();

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
                    TableName = entry.Entity.GetType().Name,
                    Action = entry.State.ToString(),
                    UserId = CurrentUser.UserId,
                    DateLogged = DateTime.Now
                };

                if (entry.State == EntityState.Modified)
                {
                    audit.OldValue = string.Join(", ",
                        entry.OriginalValues.PropertyNames
                            .Select(p => $"{p}={entry.OriginalValues[p]}"));

                    audit.NewValue = string.Join(", ",
                        entry.CurrentValues.PropertyNames
                            .Select(p => $"{p}={entry.CurrentValues[p]}"));
                }
                else if (entry.State == EntityState.Added)
                {
                    audit.NewValue = string.Join(", ",
                        entry.CurrentValues.PropertyNames
                            .Select(p => $"{p}={entry.CurrentValues[p]}"));
                }
                else if (entry.State == EntityState.Deleted)
                {
                    audit.OldValue = string.Join(", ",
                        entry.OriginalValues.PropertyNames
                            .Select(p => $"{p}={entry.OriginalValues[p]}"));
                }

                auditLogs.Add(audit);
            }

            AuditLogs.AddRange(auditLogs);
        }
    }
}
