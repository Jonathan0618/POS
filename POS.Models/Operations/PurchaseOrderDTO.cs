using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace POS.Models.Operations
{
    public sealed class PurchaseOrderLineInputDTO
    {
        [Range(1, int.MaxValue)] public int ProductId { get; set; }
        [Range(1, int.MaxValue)] public int Quantity { get; set; }
        [Range(typeof(decimal), "0.01", "9999999999999999.99")] public decimal UnitCost { get; set; }
    }

    public sealed class PurchaseOrderDraftDTO
    {
        [Required, StringLength(30)] public string OrderNumber { get; set; }
        [Range(1, int.MaxValue)] public int SupplierId { get; set; }
        public List<PurchaseOrderLineInputDTO> Items { get; set; } = new List<PurchaseOrderLineInputDTO>();
    }

    public sealed class PurchaseOrderDraftUpdateDTO
    {
        [Range(1, int.MaxValue)] public int PurchaseOrderId { get; set; }
        [Required] public string Revision { get; set; }
        [Range(1, int.MaxValue)] public int SupplierId { get; set; }
        public List<PurchaseOrderLineInputDTO> Items { get; set; } = new List<PurchaseOrderLineInputDTO>();
    }

    public sealed class PurchaseOrderTransitionDTO
    {
        [Range(1, int.MaxValue)] public int PurchaseOrderId { get; set; }
        [Required] public string Revision { get; set; }
        [StringLength(250)] public string Reason { get; set; }
    }

    public sealed class PurchaseOrderSearchDTO
    {
        public string Search { get; set; }
        public int? SupplierId { get; set; }
        public int? ProductId { get; set; }
        public string Status { get; set; }
        public DateTime? FromUtc { get; set; }
        public DateTime? ToUtcExclusive { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    public sealed class PurchaseOrderSummaryDTO
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; }
        public int SupplierId { get; set; }
        public string SupplierCode { get; set; }
        public string SupplierName { get; set; }
        public string Status { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime? OrderedUtc { get; set; }
        public DateTime? CancelledUtc { get; set; }
        public string CancelReason { get; set; }
        public decimal TotalAmount { get; set; }
        public int LineCount { get; set; }
    }

    public sealed class PurchaseOrderPageDTO
    {
        public List<PurchaseOrderSummaryDTO> Items { get; set; } = new List<PurchaseOrderSummaryDTO>();
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
    }

    public sealed class PurchaseOrderLineDTO
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string CurrentProductName { get; set; }
        public string CurrentSku { get; set; }
        public int OrderedQuantity { get; set; }
        public int ReceivedQuantity { get; set; }
        public int RemainingQuantity => OrderedQuantity - ReceivedQuantity;
        public decimal UnitCost { get; set; }
        public decimal LineTotal => UnitCost * OrderedQuantity;
    }

    public sealed class PurchaseOrderDetailsDTO
    {
        public PurchaseOrderSummaryDTO Order { get; set; }
        public List<PurchaseOrderLineDTO> Items { get; set; } = new List<PurchaseOrderLineDTO>();
        public string Revision { get; set; }
    }
}
