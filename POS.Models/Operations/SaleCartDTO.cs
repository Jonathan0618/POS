using System.Collections.Generic;

namespace POS.Models.Operations
{
    public sealed class SaleCartProductPageDTO
    {
        public List<SaleCartLineDTO> Items { get; set; } = new List<SaleCartLineDTO>();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int TotalPages { get; set; }
    }

    public sealed class SaleCartDTO
    {
        public int RegisterStationId { get; set; }
        public int? CustomerId { get; set; }
        public string CurrencyCode { get; set; }
        public decimal Subtotal { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public List<SaleCartLineDTO> Items { get; set; } = new List<SaleCartLineDTO>();
    }

    public sealed class SaleCartLineDTO
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public string Sku { get; set; }
        public string Barcode { get; set; }
        public int Quantity { get; set; }
        public int QuantityAvailable { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TaxRate { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal LineTotal { get; set; }
    }
}
