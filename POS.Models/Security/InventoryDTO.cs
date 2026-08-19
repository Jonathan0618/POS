using System.ComponentModel.DataAnnotations;

namespace POS.Models.Security
{
    public class InventoryDTO
    {
        public int ProductId { get; set; }
        [Required]
        public string ProductName { get; set; }
        [Required]
        public string CategoryName { get; set; }
        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal Price { get; set; }
        public string Description { get; set; }
        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal CostPrice { get; set; }
        public int CategoryId { get; set; }
        [Required]
        public string Barcode { get; set; }


    }
}
