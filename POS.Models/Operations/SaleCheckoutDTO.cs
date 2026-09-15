using POS.Common.Enumerations;
using System;
using System.Collections.Generic;

namespace POS.Models.Operations
{
    public sealed class SaleCheckoutDTO
    {
        // Generate once and retain unchanged for every retry of this checkout.
        public Guid RequestId { get; set; }
        public int RegisterStationId { get; set; }
        public int CashierShiftId { get; set; }
        public int? CustomerId { get; set; }
        public decimal DiscountAmount { get; set; }
        public int? HeldSaleId { get; set; }
        public string HeldRevision { get; set; }
        public List<SaleCheckoutLineDTO> Items { get; set; } = new List<SaleCheckoutLineDTO>();
        public List<SaleTenderDTO> Tenders { get; set; } = new List<SaleTenderDTO>();
    }

    public sealed class HeldSaleSaveDTO
    {
        public int? Id { get; set; }
        public Guid RequestId { get; set; }
        public string Revision { get; set; }
        public int RegisterStationId { get; set; }
        public int? CustomerId { get; set; }
        public decimal DiscountAmount { get; set; }
        public List<SaleCheckoutLineDTO> Items { get; set; } = new List<SaleCheckoutLineDTO>();
    }

    public sealed class HeldSaleDTO
    {
        public int Id { get; set; }
        public Guid RequestId { get; set; }
        public string Revision { get; set; }
        public int RegisterStationId { get; set; }
        public string RegisterCode { get; set; }
        public int? CustomerId { get; set; }
        public string CustomerName { get; set; }
        public string CashierUserId { get; set; }
        public DateTime HeldUtc { get; set; }
        public decimal Subtotal { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public List<HeldSaleLineDTO> Items { get; set; } = new List<HeldSaleLineDTO>();
    }

    public sealed class HeldSaleLineDTO
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public string Sku { get; set; }
        public string Barcode { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal LineTotal { get; set; }
    }

    public sealed class SaleCheckoutLineDTO
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }

    public sealed class SaleTenderDTO
    {
        public TenderType TenderType { get; set; }
        public decimal Amount { get; set; }
        public string ExternalReference { get; set; }
    }
}
