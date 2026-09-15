using POS.Domains.Operations;
using System.Data.Entity.ModelConfiguration;

namespace POS.Data.Configurations
{
    public class StoreSettingConfiguration : EntityTypeConfiguration<StoreSetting>
    {
        public StoreSettingConfiguration() { Property(x => x.StoreName).IsRequired(); }
    }

    public class TaxRateConfiguration : EntityTypeConfiguration<TaxRate>
    {
        public TaxRateConfiguration() { Property(x => x.Rate).HasPrecision(9, 6); }
    }

    public class RegisterStationConfiguration : EntityTypeConfiguration<RegisterStation>
    {
        public RegisterStationConfiguration() { HasIndex(x => x.Code).IsUnique(); }
    }

    public class NumberSequenceConfiguration : EntityTypeConfiguration<NumberSequence>
    {
        public NumberSequenceConfiguration() { HasIndex(x => x.DocumentType).IsUnique(); }
    }

    public class InventoryBalanceConfiguration : EntityTypeConfiguration<InventoryBalance>
    {
        public InventoryBalanceConfiguration()
        {
            HasIndex(x => x.ProductId).IsUnique();
            HasRequired(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).WillCascadeOnDelete(false);
        }
    }

    public class StockMovementConfiguration : EntityTypeConfiguration<StockMovement>
    {
        public StockMovementConfiguration()
        {
            HasRequired(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).WillCascadeOnDelete(false);
            HasIndex(x => new { x.ProductId, x.CreatedUtc });
        }
    }

    public class StockCountConfiguration : EntityTypeConfiguration<StockCount>
    {
        public StockCountConfiguration()
        {
            Property(x => x.RequestHash).HasMaxLength(64);
        }
    }

    public class SupplierConfiguration : EntityTypeConfiguration<Supplier>
    {
        public SupplierConfiguration() { HasIndex(x => x.Code).IsUnique(); }
    }

    public class SupplierProductConfiguration : EntityTypeConfiguration<SupplierProduct>
    {
        public SupplierProductConfiguration()
        {
            HasIndex(x => new { x.SupplierId, x.ProductId }).IsUnique();
            Property(x => x.DefaultCost).HasPrecision(18, 2);
        }
    }

    public class PurchaseOrderConfiguration : EntityTypeConfiguration<PurchaseOrder>
    {
        public PurchaseOrderConfiguration()
        {
            HasIndex(x => x.OrderNumber).IsUnique();
            Property(x => x.TotalAmount).HasPrecision(18, 2);
            Property(x => x.CancelReason).HasMaxLength(250);
        }
    }

    public class PurchaseOrderLineConfiguration : EntityTypeConfiguration<PurchaseOrderLine>
    {
        public PurchaseOrderLineConfiguration() { Property(x => x.UnitCost).HasPrecision(18, 2); }
    }

    public class GoodsReceiptConfiguration : EntityTypeConfiguration<GoodsReceipt>
    {
        public GoodsReceiptConfiguration() { HasIndex(x => x.ReceiptNumber).IsUnique(); }
    }

    public class GoodsReceiptLineConfiguration : EntityTypeConfiguration<GoodsReceiptLine>
    {
        public GoodsReceiptLineConfiguration() { Property(x => x.UnitCost).HasPrecision(18, 2); }
    }

    public class PurchaseReturnConfiguration : EntityTypeConfiguration<PurchaseReturn>
    {
        public PurchaseReturnConfiguration()
        {
            HasIndex(x => x.ReturnNumber).IsUnique();
            Property(x => x.TotalAmount).HasPrecision(18, 2);
            HasRequired(x => x.GoodsReceipt).WithMany().HasForeignKey(x => x.GoodsReceiptId).WillCascadeOnDelete(false);
            HasRequired(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).WillCascadeOnDelete(false);
        }
    }

    public class PurchaseReturnLineConfiguration : EntityTypeConfiguration<PurchaseReturnLine>
    {
        public PurchaseReturnLineConfiguration()
        {
            Property(x => x.UnitCost).HasPrecision(18, 2);
            HasRequired(x => x.PurchaseReturn).WithMany(x => x.Lines).HasForeignKey(x => x.PurchaseReturnId).WillCascadeOnDelete(true);
            HasRequired(x => x.GoodsReceiptLine).WithMany().HasForeignKey(x => x.GoodsReceiptLineId).WillCascadeOnDelete(false);
            HasRequired(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).WillCascadeOnDelete(false);
        }
    }

    public class CustomerConfiguration : EntityTypeConfiguration<Customer>
    {
        public CustomerConfiguration() { HasIndex(x => x.Code).IsUnique(); }
    }

    public class PaymentConfiguration : EntityTypeConfiguration<Payment>
    {
        public PaymentConfiguration()
        {
            Property(x => x.Amount).HasPrecision(18, 2);
            HasRequired(x => x.Sale).WithMany(x => x.Payments).HasForeignKey(x => x.SaleId).WillCascadeOnDelete(false);
        }
    }

    public class SaleReturnConfiguration : EntityTypeConfiguration<SaleReturn>
    {
        public SaleReturnConfiguration()
        {
            HasIndex(x => x.ReturnNumber).IsUnique();
            HasIndex(x => x.RequestId).IsUnique();
            Property(x => x.RequestHash).IsRequired().HasMaxLength(64);
            Property(x => x.TotalAmount).HasPrecision(18, 2);
            HasRequired(x => x.Sale).WithMany().HasForeignKey(x => x.SaleId).WillCascadeOnDelete(false);
            HasOptional(x => x.ExchangeSale).WithMany().HasForeignKey(x => x.ExchangeSaleId).WillCascadeOnDelete(false);
            HasRequired(x => x.RegisterStation).WithMany().HasForeignKey(x => x.RegisterStationId).WillCascadeOnDelete(false);
            HasRequired(x => x.CashierShift).WithMany().HasForeignKey(x => x.CashierShiftId).WillCascadeOnDelete(false);
        }
    }

    public class SaleReturnLineConfiguration : EntityTypeConfiguration<SaleReturnLine>
    {
        public SaleReturnLineConfiguration()
        {
            Property(x => x.RefundAmount).HasPrecision(18, 2);
            HasRequired(x => x.SaleItem).WithMany().HasForeignKey(x => x.SaleItemId).WillCascadeOnDelete(false);
            HasRequired(x => x.SaleReturn).WithMany(x => x.Lines).HasForeignKey(x => x.SaleReturnId).WillCascadeOnDelete(true);
        }
    }

    public class RefundPaymentConfiguration : EntityTypeConfiguration<RefundPayment>
    {
        public RefundPaymentConfiguration()
        {
            Property(x => x.Amount).HasPrecision(18, 2);
            HasRequired(x => x.OriginalPayment).WithMany().HasForeignKey(x => x.OriginalPaymentId).WillCascadeOnDelete(false);
        }
    }

    public class CashierShiftConfiguration : EntityTypeConfiguration<CashierShift>
    {
        public CashierShiftConfiguration()
        {
            HasIndex(x => x.RequestId).IsUnique();
            Property(x => x.OpeningCash).HasPrecision(18, 2);
            Property(x => x.ExpectedCash).HasPrecision(18, 2);
            Property(x => x.CountedCash).HasPrecision(18, 2);
            Property(x => x.Variance).HasPrecision(18, 2);
        }
    }

    public class CashMovementConfiguration : EntityTypeConfiguration<CashMovement>
    {
        public CashMovementConfiguration()
        {
            HasIndex(x => x.RequestId).IsUnique();
            Property(x => x.Amount).HasPrecision(18, 2);
        }
    }
}
