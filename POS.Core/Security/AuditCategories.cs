using System;
using System.Collections.Generic;
using System.Linq;

namespace POS.Core.Security
{
    public static class AuditCategories
    {
        public const string Other = "Other";

        // Classify both historical and new events without rewriting stored audit facts.
        private static readonly Dictionary<string, string[]> Entities = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            { "Security", new[] { "Authentication", "User", "Role", "RoleClaim", "RolePermission", "UserRole", "Module", "IdentityUserRole", "IdentityUserClaim", "IdentityUserLogin" } },
            { "Configuration", new[] { "StoreSetting", "TaxRate", "RegisterStation", "NumberSequence" } },
            { "Catalog", new[] { "Product", "Category" } },
            { "Inventory", new[] { "Stock", "InventoryBalance", "StockMovement", "StockCount", "StockCountLine" } },
            { "Purchasing", new[] { "Supplier", "SupplierProduct", "PurchaseOrder", "PurchaseOrderLine", "GoodsReceipt", "GoodsReceiptLine" } },
            { "Sales", new[] { "Sale", "SaleItem", "Payment", "SaleReturn", "SaleReturnLine", "RefundPayment" } },
            { "Customers", new[] { "Customer" } },
            { "Cash management", new[] { "CashierShift", "CashMovement" } }
            ,{ "Maintenance", new[] { "Maintenance" } }
        };

        public static IReadOnlyList<string> Names { get; } = Array.AsReadOnly(Entities.Keys.Concat(new[] { Other }).ToArray());

        public static string[] GetEntities(string category)
        {
            if (category == Other) return Entities.Values.SelectMany(x => x).ToArray();
            if (!Entities.TryGetValue(category, out var entities))
                throw new ArgumentException("Select a valid audit category.", nameof(category));
            return entities.ToArray();
        }

        public static string Classify(string entity)
        {
            return Entities.FirstOrDefault(x => x.Value.Contains(entity)).Key ?? Other;
        }
    }
}
