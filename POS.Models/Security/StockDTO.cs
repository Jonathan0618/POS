
using System.ComponentModel.DataAnnotations;

namespace POS.Models.Security
{
    public class StockDTO
    {
        public int ProductId { get; set; }
        [Required]
        public string ProductName { get; set; }
        [Required]
        public int Quantity { get; set; }
        [Required]
        public string Unit { get; set; }
    }
}
