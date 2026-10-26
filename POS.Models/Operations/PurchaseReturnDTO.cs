using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace POS.Models.Operations
{
    public sealed class PurchaseReturnLineInputDTO
    {
        [Range(1, int.MaxValue)] public int GoodsReceiptLineId { get; set; }
        [Range(1, int.MaxValue)] public int Quantity { get; set; }
    }

    public sealed class PurchaseReturnPostDTO
    {
        [Required, StringLength(30)] public string ReturnNumber { get; set; }
        [Range(1, int.MaxValue)] public int GoodsReceiptId { get; set; }
        [Required, StringLength(250)] public string Reason { get; set; }
        public List<PurchaseReturnLineInputDTO> Items { get; set; } = new List<PurchaseReturnLineInputDTO>();
    }
}
