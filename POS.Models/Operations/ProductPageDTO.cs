using System;
using System.Collections.Generic;

namespace POS.Models.Operations
{
    public sealed class ProductSearchDTO
    {
        public string Search { get; set; }
        public int? CategoryId { get; set; }
        public bool? IsActive { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    public sealed class ProductSummaryDTO
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Sku { get; set; }
        public string Barcode { get; set; }
        public string Description { get; set; }
        public int CategoryId { get; set; }
        public string CategoryName { get; set; }
        public string Unit { get; set; }
        public decimal SellingPrice { get; set; }
        public decimal CostPrice { get; set; }
        public int? Quantity { get; set; }
        public bool MissingInventoryBalance => !Quantity.HasValue;
        public int BuyingThreshold { get; set; }
        public bool IsActive { get; set; }
        public DateTime? ExpiryDate { get; set; }
    }

    public sealed class ProductPageDTO
    {
        public List<ProductSummaryDTO> Items { get; set; } = new List<ProductSummaryDTO>();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }
}
