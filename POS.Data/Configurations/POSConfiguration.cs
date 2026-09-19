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
            Property(s => s.CostPrice).HasPrecision(18, 2);
            Property(s => s.DiscountAmount).HasPrecision(18, 2);
            Property(s => s.TaxRate).HasPrecision(9, 6);
            Property(s => s.TaxAmount).HasPrecision(18, 2);
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

    public class SaleConfiguration : EntityTypeConfiguration<Sale>
    {
        public SaleConfiguration()
        {
            HasIndex(x => x.RequestId).IsUnique();
            Property(x => x.RequestHash).IsRequired().HasMaxLength(64);
            HasIndex(x => x.ReceiptNumber).IsUnique();
            Property(x => x.Subtotal).HasPrecision(18, 2);
            Property(x => x.DiscountAmount).HasPrecision(18, 2);
            Property(x => x.TaxAmount).HasPrecision(18, 2);
            Property(x => x.RoundingAmount).HasPrecision(18, 2);
            Property(x => x.TotalAmount).HasPrecision(18, 2);
            Property(x => x.CashReceived).HasPrecision(18, 2);
            Property(x => x.Change).HasPrecision(18, 2);
        }
    }
}
