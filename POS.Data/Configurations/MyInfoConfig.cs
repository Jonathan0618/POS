using System.Data.Entity.ModelConfiguration;
using POS.Domains.practicefolder;

namespace POS.Data.Configurations
{
    public class MyInfoConfig : EntityTypeConfiguration<MyInfo>
    {
        public MyInfoConfig()
        {
            ToTable("MyInfo");
            HasKey(x => x.Id);
            Property(x => x.FirtName).IsRequired().HasMaxLength(50);
            Property(x => x.LastName).IsRequired().HasMaxLength(50);
            Property(x => x.age).IsRequired();
            Property(x => x.courses).IsRequired().HasMaxLength(100);
            Property(x => x.Email).IsRequired().HasMaxLength(100);
            Property(x => x.PhoneNumber).IsRequired();

        }
    }
}
