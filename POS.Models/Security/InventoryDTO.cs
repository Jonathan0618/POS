using System.ComponentModel.DataAnnotations;

namespace POS.Models.Security
{
    public class InventoryDTO
    {
        public int Id { get; set; }
        [Required]
        public string ProductName { get; set; }
        public string Description { get; set; }
        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal SellingPrice { get; set; }
        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal CostPrice { get; set; }
        public int CategoryId { get; set; }
        public string CategoryName { get; set; }
        [Required]
        public string Barcode { get; set; }
        [Required]
        public string Unit { get; set; }

    }
}
