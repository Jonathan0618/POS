using Microsoft.AspNet.Identity.EntityFramework;
using POS.Domains.BusinessObjects;
using POS.Domains.Operations;
using POS.Domains.Security;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace POS.Data.Auditing
{
    public static class AuditValuePolicy
    {
        // New entity types and properties receive no value logging until reviewed here.
        // Contact details, free-form notes, credentials, payment references and row versions
        // are deliberately excluded. Explicit business events retain their own safe details.
        private static readonly Dictionary<Type, string[]> Allowed = new Dictionary<Type, string[]>
        {
            { typeof(User), Fields("Id UserName LockoutEnabled LockoutEndDateUtc AccessFailedCount EmailConfirmed PhoneNumberConfirmed TwoFactorEnabled") },
            { typeof(Role), Fields("Id Name") },
            { typeof(RoleClaim), Fields("Id RoleId ModuleId CanView CanAdd CanEdit CanDelete") },
            { typeof(Module), Fields("Id ParentModuleId Name") },
            { typeof(IdentityUserRole), Fields("UserId RoleId") },
            { typeof(IdentityUserClaim), Fields("Id UserId") },
            { typeof(IdentityUserLogin), Fields("UserId") },
            { typeof(Category), Fields("Id Name IsActive") },
            { typeof(Product), Fields("Id Name Sku Price CostPrice CategoryId Quantity Barcode Unit ExpiryDate IsActive BuyingThreshold") },
            { typeof(Stock), Fields("Id ProductId Quantity") },
            { typeof(StoreSetting), Fields("Id StoreName CurrencyCode MoneyDecimalPlaces TimeZoneId DefaultTaxRateId AllowNegativeStock") },
            { typeof(TaxRate), Fields("Id Name Rate IsInclusive IsActive EffectiveFromUtc EffectiveToUtc") },
            { typeof(RegisterStation), Fields("Id Code Name IsActive") },
            { typeof(NumberSequence), Fields("Id DocumentType Prefix NextNumber") },
            { typeof(InventoryBalance), Fields("Id ProductId QuantityOnHand") },
            { typeof(StockMovement), Fields("Id ProductId MovementType QuantityDelta ReferenceType ReferenceId UserId CreatedUtc") },
            { typeof(StockCount), Fields("Id RequestId Status CreatedByUserId CreatedUtc PostedUtc") },
            { typeof(StockCountLine), Fields("Id StockCountId ProductId ExpectedQuantity CountedQuantity") },
            { typeof(Supplier), Fields("Id Code IsActive") },
            { typeof(SupplierProduct), Fields("Id SupplierId ProductId SupplierSku DefaultCost LeadTimeDays") },
            { typeof(PurchaseOrder), Fields("Id OrderNumber SupplierId Status CreatedUtc OrderedUtc CancelledUtc CancelReason TotalAmount") },
            { typeof(PurchaseOrderLine), Fields("Id PurchaseOrderId ProductId OrderedQuantity ReceivedQuantity UnitCost") },
            { typeof(GoodsReceipt), Fields("Id ReceiptNumber SupplierId PurchaseOrderId Status ReceivedUtc") },
            { typeof(GoodsReceiptLine), Fields("Id GoodsReceiptId ProductId Quantity UnitCost") },
            { typeof(Customer), Fields("Id Code IsActive") },
            { typeof(Sale), Fields("Id RequestId SaleDate ReceiptNumber Status RegisterStationId CashierShiftId CustomerId CashierUserId Subtotal DiscountAmount TaxAmount RoundingAmount TotalAmount CashReceived Change CurrencyCode TaxName TaxInclusive MoneyDecimalPlaces RoundingMethod StoreName RegisterCode RegisterName CashierName CustomerCode CustomerName") },
            { typeof(SaleItem), Fields("Id SaleId ProductId Quantity ProductName Sku Barcode UnitPrice Subtotal CostPrice DiscountAmount TaxRate TaxAmount") },
            { typeof(Payment), Fields("Id SaleId TenderType Status Amount CreatedUtc") },
            { typeof(SaleReturn), Fields("Id RequestId ReturnNumber SaleId RegisterStationId CashierShiftId ExchangeSaleId Status CreatedByUserId ApprovedByUserId TotalAmount CreatedUtc") },
            { typeof(SaleReturnLine), Fields("Id SaleReturnId SaleItemId Quantity RefundAmount Disposition") },
            { typeof(RefundPayment), Fields("Id SaleReturnId OriginalPaymentId TenderType Amount CreatedUtc") },
            { typeof(CashierShift), Fields("Id RegisterStationId CashierUserId Status OpenedUtc ClosedUtc OpeningCash ExpectedCash CountedCash Variance") },
            { typeof(CashMovement), Fields("Id CashierShiftId MovementType Amount UserId CreatedUtc") }
        };

        private static string[] Fields(string fields) => fields.Split(' ');

        public static string Serialize(Type entityType, IEnumerable<string> propertyNames, Func<string, object> getValue)
        {
            var values = new Dictionary<string, string>();
            if (Allowed.TryGetValue(entityType, out var allowed))
            {
                var available = new HashSet<string>(propertyNames, StringComparer.Ordinal);
                foreach (var name in allowed.Where(available.Contains))
                {
                    var value = getValue(name);
                    var text = value is DateTime date ? date.ToString("O", CultureInfo.InvariantCulture) :
                        value == null ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
                    values.Add(name, AuditEventDetails.Limit(text));
                }
            }
            return AuditEventDetails.Serialize(values);
        }
    }
}
