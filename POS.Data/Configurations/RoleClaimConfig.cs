using POS.Domains.Security;
using System.Data.Entity.ModelConfiguration;

namespace POS.Data.Configurations
{
    internal class RoleClaimConfig : EntityTypeConfiguration<RoleClaim>
    {
        public RoleClaimConfig()
        {
            HasRequired(x => x.Module)
                .WithMany()
                .HasForeignKey(x => x.ModuleId)
                .WillCascadeOnDelete(true);
        }
    }
}
