using System.Collections.Generic;

namespace POS.Models.Operations
{
    public sealed class InventoryBalanceSearchDTO
    {
        public string Search { get; set; }
        public int? CategoryId { get; set; }
        public bool? IsActive { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    public sealed class InventoryBalanceItemDTO
    {
        public int BalanceId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public string Sku { get; set; }
        public string Barcode { get; set; }
        public string Unit { get; set; }
        public int CategoryId { get; set; }
        public string CategoryName { get; set; }
        public bool IsActive { get; set; }
        public int QuantityOnHand { get; set; }
        public int ReorderLevel { get; set; }
    }

    public sealed class InventoryBalancePageDTO
    {
        public List<InventoryBalanceItemDTO> Items { get; set; } = new List<InventoryBalanceItemDTO>();
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
    }
}
