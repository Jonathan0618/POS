
using System;
using System.Collections.Generic;

namespace POS.Domains.BusinessObjects
{
    public class Sale
    {
        public int Id { get; set; }

        public DateTime SaleDate { get; set; }

        public decimal TotalAmount { get; set; }

        public decimal CashReceived { get; set; }

        public decimal Change { get; set; }

        public virtual ICollection<SaleItem> Items { get; set; }
        public ICollection<SaleItem> SaleItems { get; set; }
    }
}
