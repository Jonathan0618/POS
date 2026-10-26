using System.ComponentModel.DataAnnotations;

namespace POS.Models.Operations
{
    public class TaxRateHistoryDTO
    {
        public int TaxRateId { get; set; }
        public string Name { get; set; }
        public decimal Rate { get; set; }
        public bool IsInclusive { get; set; }
        public bool IsActive { get; set; }
        public System.DateTime EffectiveFromUtc { get; set; }
        public System.DateTime? EffectiveToUtc { get; set; }
    }

    public class StoreSettingsDTO
    {
        // Opaque snapshot returned by GetSettings; preserve it while editing.
        public string Revision { get; set; }
        public int StoreSettingId { get; set; }
        [Required, StringLength(150)] public string StoreName { get; set; }
        [StringLength(300)] public string Address { get; set; }
        [StringLength(50)] public string Phone { get; set; }
        [EmailAddress, StringLength(100)] public string Email { get; set; }
        [StringLength(50)] public string TaxIdentifier { get; set; }
        [Required, StringLength(3, MinimumLength = 3)] public string CurrencyCode { get; set; } = "PHP";
        [Range(0, 4)] public int MoneyDecimalPlaces { get; set; } = 2;
        [Required, StringLength(100)] public string TimeZoneId { get; set; }
        [StringLength(500)] public string ReceiptFooter { get; set; }
        public bool AllowNegativeStock { get; set; }

        public int TaxRateId { get; set; }
        [Required, StringLength(100)] public string TaxName { get; set; }
        [Range(typeof(decimal), "0", "1")] public decimal TaxRate { get; set; }
        public bool TaxInclusive { get; set; }

        public int RegisterStationId { get; set; }
        [Required, StringLength(30)] public string RegisterCode { get; set; }
        [Required, StringLength(100)] public string RegisterName { get; set; }
        [StringLength(200)] public string PrinterName { get; set; }

        [Required, StringLength(20)] public string ReceiptPrefix { get; set; } = "R";
        [Range(1, long.MaxValue)] public long NextReceiptNumber { get; set; } = 1;
    }
}
