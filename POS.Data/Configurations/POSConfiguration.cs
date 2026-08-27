using POS.Domains.BusinessObjects;
using System.Data.Entity.ModelConfiguration;

namespace POS.Data.Configurations
{
    public class POSConfiguration : EntityTypeConfiguration<SaleItem>
    {
        public POSConfiguration()
        {
            Property(s => s.UnitPrice)
                .HasPrecision(18, 2);
            Property(s => s.Subtotal)
                .HasPrecision(18, 2);
            HasRequired(s => s.Sale)
              .WithMany(s => s.SaleItems)
              .HasForeignKey(s => s.SaleId)
              .WillCascadeOnDelete(true);

            HasRequired(s => s.Product)
                .WithMany()
                .HasForeignKey(s => s.ProductId)
                .WillCascadeOnDelete(false);
        }
    }
}
