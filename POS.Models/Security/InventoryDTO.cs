using System.ComponentModel.DataAnnotations;

namespace POS.Models.Security
{
    public class InventoryDTO
    {
        public int Id { get; set; }
        [Required, StringLength(100)]
        public string ProductName { get; set; }
        [StringLength(500)]
        public string Description { get; set; }
        [Required]
        [Range(typeof(decimal), "0.01", "9999999999999999.99")]
        public decimal SellingPrice { get; set; }
        [Required]
        [Range(typeof(decimal), "0.01", "9999999999999999.99")]
        public decimal CostPrice { get; set; }
        [Range(1, int.MaxValue)] public int CategoryId { get; set; }
        public string CategoryName { get; set; }
        [Range(0, int.MaxValue)]
        public int BuyingThreshold { get; set; }
        [Required, StringLength(50)]
        public string Barcode { get; set; }
        // Optional during creation for compatibility with the existing add form.
        [StringLength(50)] public string Sku { get; set; }
        [StringLength(30)] public string Unit { get; set; }

    }
}
