using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace POS.Models.Operations
{
    public sealed class GoodsReceiptLineInputDTO
    {
        [Range(1, int.MaxValue)] public int ProductId { get; set; }
        [Range(1, int.MaxValue)] public int Quantity { get; set; }
    }

    public sealed class PurchaseOrderReceiptDTO
    {
        [Required, StringLength(30)] public string ReceiptNumber { get; set; }
        [Range(1, int.MaxValue)] public int PurchaseOrderId { get; set; }
        [StringLength(50)] public string SupplierReference { get; set; }
        public List<GoodsReceiptLineInputDTO> Items { get; set; } = new List<GoodsReceiptLineInputDTO>();
    }

    public sealed class DirectGoodsReceiptLineInputDTO
    {
        [Range(1, int.MaxValue)] public int ProductId { get; set; }
        [Range(1, int.MaxValue)] public int Quantity { get; set; }
        public decimal UnitCost { get; set; }
    }

    public sealed class DirectGoodsReceiptDTO
    {
        [Required, StringLength(30)] public string ReceiptNumber { get; set; }
        [Range(1, int.MaxValue)] public int SupplierId { get; set; }
        [StringLength(50)] public string SupplierReference { get; set; }
        public List<DirectGoodsReceiptLineInputDTO> Items { get; set; } = new List<DirectGoodsReceiptLineInputDTO>();
    }

    public sealed class ReceiptCostProposalDTO
    {
        public int GoodsReceiptLineId { get; set; }
        public int GoodsReceiptId { get; set; }
        public string ReceiptNumber { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public decimal CurrentCatalogCost { get; set; }
        public decimal ReceivedUnitCost { get; set; }
    }

    public sealed class ApplyReceiptCostDTO
    {
        [Range(1, int.MaxValue)] public int GoodsReceiptLineId { get; set; }
        public decimal ExpectedCurrentCatalogCost { get; set; }
    }
}
