using System;
using System.Collections.Generic;

namespace POS.Models.Operations
{
    public enum SupplierPurchaseDocumentKind
    {
        PurchaseOrder = 0,
        GoodsReceipt = 1
    }

    public sealed class SupplierPurchaseHistorySearchDTO
    {
        public int SupplierId { get; set; }
        public SupplierPurchaseDocumentKind? Kind { get; set; }
        public string DocumentNumber { get; set; }
        public DateTime? FromUtc { get; set; }
        public DateTime? ToUtcExclusive { get; set; }
        public int? ProductId { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    public sealed class SupplierPurchaseHistoryItemDTO
    {
        public SupplierPurchaseDocumentKind Kind { get; set; }
        public int DocumentId { get; set; }
        public string DocumentNumber { get; set; }
        public string Status { get; set; }
        public DateTime EventUtc { get; set; }
        public decimal TotalAmount { get; set; }
        public int LineCount { get; set; }
        public int? PurchaseOrderId { get; set; }
        public string SupplierReference { get; set; }
    }

    public sealed class SupplierPurchaseHistoryPageDTO
    {
        public List<SupplierPurchaseHistoryItemDTO> Items { get; set; } = new List<SupplierPurchaseHistoryItemDTO>();
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
    }
}
