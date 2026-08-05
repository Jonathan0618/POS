using System.Data.Entity.ModelConfiguration;

namespace POS.Data.Configurations
{
    public class UserConfiguration : EntityTypeConfiguration<Domains.Security.User>
    {
        public UserConfiguration()
        {
            ToTable("Users");
            HasKey(u => u.Id);
            Property(u => u.UserName).IsRequired().HasMaxLength(100);
            Property(u => u.PasswordHash).IsRequired();
            Property(u => u.SecurityStamp).IsRequired();
        }
    }   
}
