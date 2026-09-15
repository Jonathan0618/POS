using System.Collections.Generic;

namespace POS.Models.Operations
{
    public sealed class InventoryReconciliationSearchDTO
    {
        public int? ProductId { get; set; }
        public bool MismatchesOnly { get; set; } = true;
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    public sealed class InventoryReconciliationItemDTO
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public string Sku { get; set; }
        public bool IsActive { get; set; }
        public int ProductQuantity { get; set; }
        public int? BalanceQuantity { get; set; }
        public long LedgerQuantity { get; set; }
        public bool MissingBalance => !BalanceQuantity.HasValue;
        public long? BalanceMinusLedger => BalanceQuantity.HasValue
            ? (long?)BalanceQuantity.Value - LedgerQuantity : null;
        public long? ProductMinusBalance => BalanceQuantity.HasValue
            ? (long?)ProductQuantity - BalanceQuantity.Value : null;
        public bool IsReconciled => BalanceQuantity.HasValue &&
            BalanceQuantity.Value == LedgerQuantity && ProductQuantity == BalanceQuantity.Value;
    }

    public sealed class InventoryReconciliationPageDTO
    {
        public List<InventoryReconciliationItemDTO> Items { get; set; } = new List<InventoryReconciliationItemDTO>();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }
}
