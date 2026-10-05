using POS.Domains.AuditEntry;
using System.Data.Entity.ModelConfiguration;

namespace POS.Data.Configurations
{
    public class AuditLogConfiguration : EntityTypeConfiguration<AuditLog>

    {
        public AuditLogConfiguration() 
        {
            HasIndex(x => new { x.UserId, x.TableName });
            HasIndex(x => x.CorrelationId);
            HasIndex(x => new { x.RegisterStationId, x.DateLogged });
        }
    }
}
