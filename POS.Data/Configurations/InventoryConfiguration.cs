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

    public class ProductConfiguration : EntityTypeConfiguration<Product>
    {
        public ProductConfiguration()
        {
            Property(p => p.Name)
                .IsRequired()
                .HasMaxLength(100);
            Property(p => p.Description)
                .HasMaxLength(500);
            Property(p => p.Price)
                .IsRequired()
                .HasPrecision(18, 2);
            Property(p => p.CostPrice)
                .IsRequired()
                .HasPrecision(18, 2);
            Property(p => p.Barcode)
                .HasMaxLength(50);
            Property(p => p.ExpiryDate)
                .HasMaxLength(50);
            Property(p => p.ModifiedBy)
                .HasMaxLength(100);
            Property(p => p.CreatedBy)
                .HasMaxLength(100);

        }
    }
}
