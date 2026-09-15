
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using POS.Common.Enumerations;
using POS.Domains.Operations;

namespace POS.Domains.BusinessObjects
{
    public class Sale
    {
        public Sale()
        {
            SaleItems = new HashSet<SaleItem>();
            Payments = new HashSet<Payment>();
        }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public Guid RequestId { get; set; }

        [StringLength(64)]
        public string RequestHash { get; set; }

        public DateTime SaleDate { get; set; }

        [StringLength(30)]
        public string ReceiptNumber { get; set; }

        public DocumentStatus Status { get; set; }

        public int? RegisterStationId { get; set; }

        public int? CashierShiftId { get; set; }

        public int? CustomerId { get; set; }

        [StringLength(128)]
        public string CashierUserId { get; set; }

        public decimal Subtotal { get; set; }

        public decimal DiscountAmount { get; set; }

        public decimal TaxAmount { get; set; }

        public decimal RoundingAmount { get; set; }

        public decimal TotalAmount { get; set; }

        public decimal CashReceived { get; set; }

        public decimal Change { get; set; }

        [StringLength(3)] public string CurrencyCode { get; set; }
        [StringLength(100)] public string TaxName { get; set; }
        public bool TaxInclusive { get; set; }
        public int MoneyDecimalPlaces { get; set; }
        [StringLength(40)] public string RoundingMethod { get; set; }

        [StringLength(150)] public string StoreName { get; set; }
        [StringLength(300)] public string StoreAddress { get; set; }
        [StringLength(50)] public string StoreTaxIdentifier { get; set; }
        [StringLength(500)] public string ReceiptFooter { get; set; }
        [StringLength(30)] public string RegisterCode { get; set; }
        [StringLength(100)] public string RegisterName { get; set; }
        [StringLength(256)] public string CashierName { get; set; }
        [StringLength(30)] public string CustomerCode { get; set; }
        [StringLength(150)] public string CustomerName { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; }

        public virtual ICollection<SaleItem> SaleItems { get; set; }
        public virtual ICollection<Payment> Payments { get; set; }
        public virtual RegisterStation RegisterStation { get; set; }
        public virtual CashierShift CashierShift { get; set; }
        public virtual Customer Customer { get; set; }
    }
}
