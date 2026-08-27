using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.Contracts;

namespace POS.Domains.BusinessObjects
{
    public class Product
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public decimal CostPrice { get; set; }
        public int CategoryId { get; set; }
        public Category Category { get; set; }
        public int Quantity { get; set; }
        public string Barcode { get; set; }
        public string Unit {  get; set; }
        public DateTime? ExpiryDate { get; set; }
        public bool IsActive { get; set; }
        public int BuyingThreshold { get; set; }
    }
}
