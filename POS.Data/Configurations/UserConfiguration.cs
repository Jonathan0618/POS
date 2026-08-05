using System.Data.Entity.ModelConfiguration;

namespace POS.Data.Configurations
{
    public class UserConfiguration : EntityTypeConfiguration<Domains.Security.User>
    {
        public UserConfiguration()
        {
        }
    }
}
