using System;

namespace POS.Models.Security
{
    public class SaleDTO
    {
        public int Id { get; set; }
        public DateTime SaleDate { get; set; }
        public decimal Subtotal { get; set; }
        public decimal VAT { get; set; }
        public decimal Discount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal CashReceived { get; set; }
        public decimal Change { get; set; }
        public int CashierId { get; set; }
    }
}
