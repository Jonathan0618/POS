using System.ComponentModel.DataAnnotations;

namespace POS.Models.Operations
{
    public sealed class ProductIdentifiersDTO
    {
        [Range(1, int.MaxValue)] public int ProductId { get; set; }
        [Required, StringLength(50)] public string Sku { get; set; }
        [Required, StringLength(30)] public string Unit { get; set; }
    }
}
