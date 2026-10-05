using System;
using System.Collections.Generic;
using POS.Common.Enumerations;

namespace POS.Models.Operations
{
    public sealed class ManagementFilterDTO
    {
        public DateTime FromUtc { get; set; }
        public DateTime ToUtcExclusive { get; set; }
        public int? RegisterStationId { get; set; }
        public string UserId { get; set; }
        public int? CategoryId { get; set; }
        public int? ProductId { get; set; }
        public int? SupplierId { get; set; }
        public int? CustomerId { get; set; }
        public bool IncludeInactiveOrCancelled { get; set; }
        public int MaximumRows { get; set; } = 500;
    }

    public sealed class FinancialSummaryDTO
    {
        public decimal GrossSales { get; set; }
        public decimal Refunds { get; set; }
        public decimal NetSales { get; set; }
        public decimal Tax { get; set; }
        public decimal Discounts { get; set; }
        public decimal EstimatedCost { get; set; }
        public decimal EstimatedMargin { get; set; }
        public int TransactionCount { get; set; }
        public int ReturnCount { get; set; }
    }

    public sealed class SalesReportRowDTO
    {
        public int SaleId { get; set; }
        public DateTime SaleUtc { get; set; }
        public string ReceiptNumber { get; set; }
        public DocumentStatus Status { get; set; }
        public int? RegisterStationId { get; set; }
        public string RegisterCode { get; set; }
        public string CashierUserId { get; set; }
        public string CashierName { get; set; }
        public int? CustomerId { get; set; }
        public string CustomerName { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Discount { get; set; }
        public decimal Tax { get; set; }
        public decimal Total { get; set; }
        public decimal Refunded { get; set; }
        public decimal NetTotal { get; set; }
    }

    public sealed class ItemSalesReportRowDTO
    {
        public int ProductId { get; set; }
        public string Sku { get; set; }
        public string ProductName { get; set; }
        public string CategoryName { get; set; }
        public int QuantitySold { get; set; }
        public int QuantityReturned { get; set; }
        public decimal GrossSales { get; set; }
        public decimal Refunds { get; set; }
        public decimal EstimatedCost { get; set; }
        public decimal EstimatedMargin { get; set; }
    }

    public sealed class TenderReportRowDTO
    {
        public string TenderType { get; set; }
        public int PaymentCount { get; set; }
        public decimal Collected { get; set; }
        public decimal Refunded { get; set; }
        public decimal NetCollected { get; set; }
    }

    public sealed class InventoryReportRowDTO
    {
        public int ProductId { get; set; }
        public string Sku { get; set; }
        public string ProductName { get; set; }
        public string CategoryName { get; set; }
        public int QuantityOnHand { get; set; }
        public int AlertThreshold { get; set; }
        public decimal UnitCost { get; set; }
        public decimal Valuation { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public bool IsActive { get; set; }
    }

    public sealed class MovementReportRowDTO
    {
        public long MovementId { get; set; }
        public DateTime CreatedUtc { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public string MovementType { get; set; }
        public int QuantityDelta { get; set; }
        public string ReferenceType { get; set; }
        public string ReferenceId { get; set; }
        public string UserId { get; set; }
        public string Reason { get; set; }
    }

    public sealed class PurchasingReportRowDTO
    {
        public int SupplierId { get; set; }
        public string SupplierCode { get; set; }
        public string SupplierName { get; set; }
        public int ReceiptCount { get; set; }
        public decimal ReceivedValue { get; set; }
        public decimal ReturnedValue { get; set; }
        public decimal NetPurchased { get; set; }
    }

    public sealed class ShiftReportRowDTO
    {
        public int ShiftId { get; set; }
        public int RegisterStationId { get; set; }
        public string RegisterCode { get; set; }
        public string CashierUserId { get; set; }
        public DateTime OpenedUtc { get; set; }
        public DateTime? ClosedUtc { get; set; }
        public ShiftStatus Status { get; set; }
        public decimal OpeningCash { get; set; }
        public decimal ExpectedCash { get; set; }
        public decimal CountedCash { get; set; }
        public decimal Variance { get; set; }
    }

    public sealed class AuditActivityReportRowDTO
    {
        public DateTime DayUtc { get; set; }
        public string Entity { get; set; }
        public string Action { get; set; }
        public int EventCount { get; set; }
    }

    public sealed class DashboardDTO
    {
        public DateTime GeneratedUtc { get; set; }
        public ManagementFilterDTO Filter { get; set; }
        public FinancialSummaryDTO Financials { get; set; }
        public IReadOnlyList<ItemSalesReportRowDTO> TopProducts { get; set; }
        public IReadOnlyList<InventoryReportRowDTO> StockAlerts { get; set; }
        public int ExpiringProductCount { get; set; }
        public int OpenShiftCount { get; set; }
        public int OpenShiftIssueCount { get; set; }
    }

    public sealed class ManagementReportDTO
    {
        public DateTime GeneratedUtc { get; set; }
        public ManagementFilterDTO Filter { get; set; }
        public FinancialSummaryDTO Financials { get; set; }
        public IReadOnlyList<SalesReportRowDTO> Sales { get; set; }
        public IReadOnlyList<ItemSalesReportRowDTO> Items { get; set; }
        public IReadOnlyList<TenderReportRowDTO> Tenders { get; set; }
        public IReadOnlyList<InventoryReportRowDTO> Inventory { get; set; }
        public IReadOnlyList<MovementReportRowDTO> Movements { get; set; }
        public IReadOnlyList<PurchasingReportRowDTO> Purchasing { get; set; }
        public IReadOnlyList<ShiftReportRowDTO> Shifts { get; set; }
        public IReadOnlyList<AuditActivityReportRowDTO> AuditActivity { get; set; }
    }
}
