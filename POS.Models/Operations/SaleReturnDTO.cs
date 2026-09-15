using POS.Common.Enumerations;
using System;
using System.Collections.Generic;

namespace POS.Models.Operations
{
    public sealed class SaleReturnEligibilityDTO
    {
        public int SaleId { get; set; }
        public string ReceiptNumber { get; set; }
        public DateTime SaleDateUtc { get; set; }
        public string CustomerName { get; set; }
        public decimal OriginalTotal { get; set; }
        public decimal PreviouslyRefunded { get; set; }
        public DateTime ReturnDeadlineUtc { get; set; }
        public bool RequiresLateApproval { get; set; }
        public List<SaleReturnEligibleLineDTO> Lines { get; set; } = new List<SaleReturnEligibleLineDTO>();
        public List<SaleReturnEligiblePaymentDTO> Payments { get; set; } = new List<SaleReturnEligiblePaymentDTO>();
    }

    public sealed class SaleReturnEligibleLineDTO
    {
        public int SaleItemId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public int PurchasedQuantity { get; set; }
        public int ReturnedQuantity { get; set; }
        public int EligibleQuantity { get; set; }
        public decimal ApproximateRefundPerUnit { get; set; }
    }

    public sealed class SaleReturnEligiblePaymentDTO
    {
        public long OriginalPaymentId { get; set; }
        public TenderType TenderType { get; set; }
        public decimal OriginalAppliedAmount { get; set; }
        public decimal RefundedAmount { get; set; }
        public decimal EligibleAmount { get; set; }
    }

    public sealed class SaleReturnPostDTO
    {
        public Guid RequestId { get; set; }
        public int SaleId { get; set; }
        public int RegisterStationId { get; set; }
        public int CashierShiftId { get; set; }
        public int? ExchangeSaleId { get; set; }
        public string Reason { get; set; }
        public List<SaleReturnLinePostDTO> Lines { get; set; } = new List<SaleReturnLinePostDTO>();
        public List<RefundTenderPostDTO> Refunds { get; set; } = new List<RefundTenderPostDTO>();
    }

    public sealed class SaleReturnLinePostDTO
    {
        public int SaleItemId { get; set; }
        public int Quantity { get; set; }
        public ReturnDisposition Disposition { get; set; }
    }

    public sealed class RefundTenderPostDTO
    {
        public long OriginalPaymentId { get; set; }
        public decimal Amount { get; set; }
        public string ExternalReference { get; set; }
    }

    public sealed class SaleReturnResultDTO
    {
        public int Id { get; set; }
        public Guid RequestId { get; set; }
        public string ReturnNumber { get; set; }
        public int SaleId { get; set; }
        public int? ExchangeSaleId { get; set; }
        public decimal TotalAmount { get; set; }
        public string ApprovedByUserId { get; set; }
        public DateTime CreatedUtc { get; set; }
    }
}
