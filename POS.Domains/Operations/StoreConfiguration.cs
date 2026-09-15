using System;
using System.ComponentModel.DataAnnotations;

namespace POS.Domains.Operations
{
    public class StoreSetting
    {
        public int Id { get; set; }
        [Required, StringLength(150)] public string StoreName { get; set; }
        [StringLength(300)] public string Address { get; set; }
        [StringLength(50)] public string Phone { get; set; }
        [StringLength(100)] public string Email { get; set; }
        [StringLength(50)] public string TaxIdentifier { get; set; }
        [Required, StringLength(3)] public string CurrencyCode { get; set; }
        public int MoneyDecimalPlaces { get; set; } = 2;
        [Required, StringLength(100)] public string TimeZoneId { get; set; }
        [StringLength(500)] public string ReceiptFooter { get; set; }
        public int DefaultTaxRateId { get; set; }
        public bool AllowNegativeStock { get; set; }
    }

    public class TaxRate
    {
        public int Id { get; set; }
        [Required, StringLength(100)] public string Name { get; set; }
        public decimal Rate { get; set; }
        public bool IsInclusive { get; set; }
        public bool IsActive { get; set; }
        public DateTime EffectiveFromUtc { get; set; }
        public DateTime? EffectiveToUtc { get; set; }
    }

    public class RegisterStation
    {
        public int Id { get; set; }
        [Required, StringLength(30)] public string Code { get; set; }
        [Required, StringLength(100)] public string Name { get; set; }
        [StringLength(200)] public string PrinterName { get; set; }
        public bool IsActive { get; set; }
    }

    public class NumberSequence
    {
        public int Id { get; set; }
        [Required, StringLength(30)] public string DocumentType { get; set; }
        [Required, StringLength(20)] public string Prefix { get; set; }
        public long NextNumber { get; set; }
        [Timestamp] public byte[] RowVersion { get; set; }
    }
}
