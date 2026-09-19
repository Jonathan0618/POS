
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Domains.BusinessObjects
{
    public class SaleItem
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int SaleId { get; set; }
        public int ProductId { get; set; }

        public int Quantity { get; set; }

        [StringLength(150)]
        public string ProductName { get; set; }

        [StringLength(50)]
        public string Sku { get; set; }

        [StringLength(50)]
        public string Barcode { get; set; }

        public decimal UnitPrice { get; set; }

        public decimal Subtotal { get; set; }

        public decimal CostPrice { get; set; }

        public decimal DiscountAmount { get; set; }

        public decimal TaxRate { get; set; }

        public decimal TaxAmount { get; set; }

        public Sale Sale { get; set; }
        public Product Product { get; set; }
    }
}
