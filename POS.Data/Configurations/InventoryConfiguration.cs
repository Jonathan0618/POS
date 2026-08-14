using POS.Domains.BusinessObjects;
using System.Data.Entity.ModelConfiguration;

namespace POS.Data.Configurations
{
    public class InventoryConfiguration : EntityTypeConfiguration<Category>
    {
        public InventoryConfiguration()
        {
            HasMany(c => c.Products)
                .WithRequired(p => p.Category)
                .HasForeignKey(p => p.CategoryId)
                .WillCascadeOnDelete(true);

        }
    }
}
