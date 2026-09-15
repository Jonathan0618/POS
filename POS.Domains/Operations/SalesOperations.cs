using POS.Common.Enumerations;
using POS.Domains.BusinessObjects;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace POS.Domains.Operations
{
    public class Customer
    {
        public int Id { get; set; }
        [Required, StringLength(30)] public string Code { get; set; }
        [Required, StringLength(150)] public string Name { get; set; }
        [StringLength(50)] public string Phone { get; set; }
        [StringLength(100)] public string Email { get; set; }
        [StringLength(300)] public string Address { get; set; }
        [StringLength(50)] public string TaxIdentifier { get; set; }
        public bool IsActive { get; set; }
    }

    public class Payment
    {
        public long Id { get; set; }
        public int SaleId { get; set; }
        public TenderType TenderType { get; set; }
        public PaymentStatus Status { get; set; }
        public decimal Amount { get; set; }
        [StringLength(100)] public string ExternalReference { get; set; }
        public DateTime CreatedUtc { get; set; }
        public virtual Sale Sale { get; set; }
    }

    public class SaleReturn
    {
        public SaleReturn() { Lines = new HashSet<SaleReturnLine>(); Refunds = new HashSet<RefundPayment>(); }
        public int Id { get; set; }
        public Guid RequestId { get; set; }
        [Required, StringLength(64)] public string RequestHash { get; set; }
        [Required, StringLength(30)] public string ReturnNumber { get; set; }
        public int SaleId { get; set; }
        public int RegisterStationId { get; set; }
        public int CashierShiftId { get; set; }
        public int? ExchangeSaleId { get; set; }
        public DocumentStatus Status { get; set; }
        [Required, StringLength(250)] public string Reason { get; set; }
        [StringLength(128)] public string CreatedByUserId { get; set; }
        [StringLength(128)] public string ApprovedByUserId { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime CreatedUtc { get; set; }
        public virtual Sale Sale { get; set; }
        public virtual Sale ExchangeSale { get; set; }
        public virtual RegisterStation RegisterStation { get; set; }
        public virtual CashierShift CashierShift { get; set; }
        public virtual ICollection<SaleReturnLine> Lines { get; set; }
        public virtual ICollection<RefundPayment> Refunds { get; set; }
    }

    public class SaleReturnLine
    {
        public int Id { get; set; }
        public int SaleReturnId { get; set; }
        public int SaleItemId { get; set; }
        public int Quantity { get; set; }
        public decimal RefundAmount { get; set; }
        public ReturnDisposition Disposition { get; set; }
        public virtual SaleReturn SaleReturn { get; set; }
        public virtual SaleItem SaleItem { get; set; }
    }

    public class RefundPayment
    {
        public long Id { get; set; }
        public int SaleReturnId { get; set; }
        public long OriginalPaymentId { get; set; }
        public TenderType TenderType { get; set; }
        public decimal Amount { get; set; }
        [StringLength(100)] public string ExternalReference { get; set; }
        public DateTime CreatedUtc { get; set; }
        public virtual SaleReturn SaleReturn { get; set; }
        public virtual Payment OriginalPayment { get; set; }
    }
}
