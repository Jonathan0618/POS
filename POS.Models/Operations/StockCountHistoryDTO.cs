using System;
using System.Collections.Generic;

namespace POS.Models.Operations
{
    public sealed class StockCountSearchDTO
    {
        // Accepted states: Draft, Posted, Cancelled (case-insensitive).
        public string Status { get; set; }
        public DateTime? FromUtc { get; set; }
        public DateTime? ToUtcExclusive { get; set; }
        public string CreatedByUserId { get; set; }
        public int? ProductId { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    public sealed class StockCountSummaryDTO
    {
        public int Id { get; set; }
        public Guid? RequestId { get; set; }
        public string Status { get; set; }
        public string CreatedByUserId { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime? PostedUtc { get; set; }
        public int ProductCount { get; set; }
        public byte[] RowVersion { get; set; }
    }

    public sealed class StockCountHistoryDTO
    {
        public List<StockCountSummaryDTO> Items { get; set; } = new List<StockCountSummaryDTO>();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    public sealed class StockCountDetailLineDTO
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        // Catalog labels are current values; quantities are stored count facts.
        public string CurrentProductName { get; set; }
        public string CurrentSku { get; set; }
        public int ExpectedQuantity { get; set; }
        public int CountedQuantity { get; set; }
        public long Variance => (long)CountedQuantity - ExpectedQuantity;
    }

    public sealed class StockCountDetailsDTO
    {
        public StockCountSummaryDTO Count { get; set; }
        public List<StockCountDetailLineDTO> Items { get; set; } = new List<StockCountDetailLineDTO>();
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }
}
