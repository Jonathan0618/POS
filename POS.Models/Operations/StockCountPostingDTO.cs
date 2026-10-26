using System;
using System.Collections.Generic;

namespace POS.Models.Operations
{
    public sealed class StockCountQuantityDTO
    {
        public int ProductId { get; set; }
        public int ExpectedQuantity { get; set; }
        public int CountedQuantity { get; set; }
    }

    public sealed class StockCountPostingDTO
    {
        // Generated once by the caller and retained across uncertain retries.
        public Guid RequestId { get; set; }
        public string Reason { get; set; }
        public List<StockCountQuantityDTO> Items { get; set; } = new List<StockCountQuantityDTO>();
    }

    public sealed class StockCountDraftUpdateDTO
    {
        public int CountId { get; set; }
        public byte[] ExpectedRowVersion { get; set; }
        // Partial updates to existing lines only; expected stock is preserved.
        public List<StockCountQuantityDTO> Items { get; set; } = new List<StockCountQuantityDTO>();
    }
}
