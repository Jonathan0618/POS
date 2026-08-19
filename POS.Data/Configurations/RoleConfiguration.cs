using POS.Domains.Security;
using System.Data.Entity.ModelConfiguration;

namespace POS.Data.Configurations
{
    internal class RoleConfiguration : EntityTypeConfiguration<Domains.Security.Role>
    {
        public RoleConfiguration()
        {
            ToTable("Roles");
            HasKey(r => r.Id);
            Property(r => r.Name).IsRequired().HasMaxLength(100);
        }
    }
}
