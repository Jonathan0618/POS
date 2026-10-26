using POS.Common.Enumerations;
using POS.Domains.BusinessObjects;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace POS.Domains.Operations
{
    public class Supplier
    {
        public Supplier() { Products = new HashSet<SupplierProduct>(); }
        public int Id { get; set; }
        [Required, StringLength(30)] public string Code { get; set; }
        [Required, StringLength(150)] public string Name { get; set; }
        [StringLength(100)] public string ContactName { get; set; }
        [StringLength(50)] public string Phone { get; set; }
        [StringLength(100)] public string Email { get; set; }
        [StringLength(300)] public string Address { get; set; }
        [StringLength(50)] public string TaxIdentifier { get; set; }
        public bool IsActive { get; set; }
        public virtual ICollection<SupplierProduct> Products { get; set; }
    }

    public class SupplierProduct
    {
        public int Id { get; set; }
        public int SupplierId { get; set; }
        public int ProductId { get; set; }
        [StringLength(50)] public string SupplierSku { get; set; }
        public decimal DefaultCost { get; set; }
        public int LeadTimeDays { get; set; }
        public virtual Supplier Supplier { get; set; }
        public virtual Product Product { get; set; }
    }

    public class PurchaseOrder
    {
        public PurchaseOrder() { Lines = new HashSet<PurchaseOrderLine>(); }
        public int Id { get; set; }
        [Required, StringLength(30)] public string OrderNumber { get; set; }
        public int SupplierId { get; set; }
        public DocumentStatus Status { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime? OrderedUtc { get; set; }
        public DateTime? CancelledUtc { get; set; }
        [StringLength(250)] public string CancelReason { get; set; }
        public decimal TotalAmount { get; set; }
        public virtual Supplier Supplier { get; set; }
        public virtual ICollection<PurchaseOrderLine> Lines { get; set; }
    }

    public class PurchaseOrderLine
    {
        public int Id { get; set; }
        public int PurchaseOrderId { get; set; }
        public int ProductId { get; set; }
        public int OrderedQuantity { get; set; }
        public int ReceivedQuantity { get; set; }
        public decimal UnitCost { get; set; }
        public virtual PurchaseOrder PurchaseOrder { get; set; }
        public virtual Product Product { get; set; }
    }

    public class GoodsReceipt
    {
        public GoodsReceipt() { Lines = new HashSet<GoodsReceiptLine>(); }
        public int Id { get; set; }
        [Required, StringLength(30)] public string ReceiptNumber { get; set; }
        public int SupplierId { get; set; }
        public int? PurchaseOrderId { get; set; }
        [StringLength(50)] public string SupplierReference { get; set; }
        public DocumentStatus Status { get; set; }
        public DateTime ReceivedUtc { get; set; }
        public virtual ICollection<GoodsReceiptLine> Lines { get; set; }
    }

    public class GoodsReceiptLine
    {
        public int Id { get; set; }
        public int GoodsReceiptId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public virtual GoodsReceipt GoodsReceipt { get; set; }
        public virtual Product Product { get; set; }
    }

    public class PurchaseReturn
    {
        public PurchaseReturn() { Lines = new HashSet<PurchaseReturnLine>(); }
        public int Id { get; set; }
        [Required, StringLength(30)] public string ReturnNumber { get; set; }
        public int GoodsReceiptId { get; set; }
        public int SupplierId { get; set; }
        public DocumentStatus Status { get; set; }
        [Required, StringLength(250)] public string Reason { get; set; }
        public DateTime ReturnedUtc { get; set; }
        public decimal TotalAmount { get; set; }
        public virtual GoodsReceipt GoodsReceipt { get; set; }
        public virtual Supplier Supplier { get; set; }
        public virtual ICollection<PurchaseReturnLine> Lines { get; set; }
    }

    public class PurchaseReturnLine
    {
        public int Id { get; set; }
        public int PurchaseReturnId { get; set; }
        public int GoodsReceiptLineId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public virtual PurchaseReturn PurchaseReturn { get; set; }
        public virtual GoodsReceiptLine GoodsReceiptLine { get; set; }
        public virtual Product Product { get; set; }
    }
}
