using POS.Common.Enumerations;
using System;
using System.Collections.Generic;

namespace POS.Models.Operations
{
    public sealed class SaleReceiptSearchDTO
    {
        public string Search { get; set; }
        public DateTime? FromUtc { get; set; }
        public DateTime? ToUtcExclusive { get; set; }
        public int? RegisterStationId { get; set; }
        public string CashierUserId { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    public sealed class SaleReceiptPageDTO
    {
        public List<SaleReceiptSummaryDTO> Items { get; set; } = new List<SaleReceiptSummaryDTO>();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int TotalPages { get; set; }
    }

    public class SaleReceiptSummaryDTO
    {
        public int SaleId { get; set; }
        public string ReceiptNumber { get; set; }
        public DateTime SaleDateUtc { get; set; }
        public DocumentStatus Status { get; set; }
        public string RegisterCode { get; set; }
        public string CashierName { get; set; }
        public string CustomerName { get; set; }
        public decimal TotalAmount { get; set; }
        public string CurrencyCode { get; set; }
    }

    public sealed class SaleReceiptDTO : SaleReceiptSummaryDTO
    {
        public string StoreName { get; set; }
        public string StoreAddress { get; set; }
        public string StoreTaxIdentifier { get; set; }
        public string ReceiptFooter { get; set; }
        public string RegisterName { get; set; }
        public string CashierUserId { get; set; }
        public string CustomerCode { get; set; }
        public decimal Subtotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal RoundingAmount { get; set; }
        public decimal CashReceived { get; set; }
        public decimal Change { get; set; }
        public string TaxName { get; set; }
        public bool TaxInclusive { get; set; }
        public int MoneyDecimalPlaces { get; set; }
        public List<SaleReceiptLineDTO> Items { get; set; } = new List<SaleReceiptLineDTO>();
        public List<SaleReceiptPaymentDTO> Payments { get; set; } = new List<SaleReceiptPaymentDTO>();
    }

    public sealed class SaleReceiptLineDTO
    {
        public string ProductName { get; set; }
        public string Sku { get; set; }
        public string Barcode { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxRate { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal LineTotal { get; set; }
    }

    public sealed class SaleReceiptPaymentDTO
    {
        public TenderType TenderType { get; set; }
        public PaymentStatus Status { get; set; }
        public decimal Amount { get; set; }
        public string ExternalReference { get; set; }
        public DateTime CreatedUtc { get; set; }
    }
}
