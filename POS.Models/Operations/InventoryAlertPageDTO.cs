using System.Collections.Generic;

namespace POS.Models.Operations
{
    public enum InventoryAlertKind
    {
        LowStock = 0,
        OutOfStock = 1,
        ExpiringSoon = 2,
        Expired = 3
    }

    public sealed class InventoryAlertSearchDTO
    {
        public InventoryAlertKind Kind { get; set; } = InventoryAlertKind.LowStock;
        public string Search { get; set; }
        public int? CategoryId { get; set; }
        // Used only for ExpiringSoon. Today is included; valid range is 1-365 days.
        public int ExpiringWithinDays { get; set; } = 30;
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    public sealed class InventoryAlertItemDTO
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public string Sku { get; set; }
        public string Barcode { get; set; }
        public string Unit { get; set; }
        public int CategoryId { get; set; }
        public string CategoryName { get; set; }
        public int QuantityOnHand { get; set; }
        public int ReorderLevel { get; set; }
        public System.DateTime? ExpiryDate { get; set; }
        public bool IsOutOfStock => QuantityOnHand <= 0;
    }

    public sealed class InventoryAlertPageDTO
    {
        public List<InventoryAlertItemDTO> Items { get; set; } = new List<InventoryAlertItemDTO>();
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
    }
}
