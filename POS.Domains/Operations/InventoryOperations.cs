using POS.Common.Enumerations;
using POS.Domains.BusinessObjects;
using System;
using System.ComponentModel.DataAnnotations;

namespace POS.Domains.Operations
{
    public class InventoryBalance
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int QuantityOnHand { get; set; }
        [Timestamp] public byte[] RowVersion { get; set; }
        public virtual Product Product { get; set; }
    }

    public class StockMovement
    {
        public long Id { get; set; }
        public int ProductId { get; set; }
        public StockMovementType MovementType { get; set; }
        public int QuantityDelta { get; set; }
        [StringLength(40)] public string ReferenceType { get; set; }
        [StringLength(50)] public string ReferenceId { get; set; }
        [Required, StringLength(250)] public string Reason { get; set; }
        [StringLength(128)] public string UserId { get; set; }
        public DateTime CreatedUtc { get; set; }
        public virtual Product Product { get; set; }
    }

    public class StockCount
    {
        public int Id { get; set; }
        public Guid? RequestId { get; set; }
        [StringLength(64)] public string RequestHash { get; set; }
        public DocumentStatus Status { get; set; }
        [StringLength(128)] public string CreatedByUserId { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime? PostedUtc { get; set; }
        [Timestamp] public byte[] RowVersion { get; set; }
    }

    public class StockCountLine
    {
        public int Id { get; set; }
        public int StockCountId { get; set; }
        public int ProductId { get; set; }
        public int ExpectedQuantity { get; set; }
        public int CountedQuantity { get; set; }
        public virtual StockCount StockCount { get; set; }
        public virtual Product Product { get; set; }
    }
}
