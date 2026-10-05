using System;
using System.Collections.Generic;

namespace POS.Models.Operations
{
    public sealed class ProductPriceChangeDTO
    {
        public long AuditId { get; set; }
        public DateTime ChangedUtc { get; set; }
        public string UserId { get; set; }
        public Guid? CorrelationId { get; set; }
        public string RegisterCode { get; set; }
        public string Action { get; set; }
        public decimal? PreviousSellingPrice { get; set; }
        public decimal? SellingPrice { get; set; }
        public decimal? PreviousCostPrice { get; set; }
        public decimal? CostPrice { get; set; }
        public bool DetailsAvailable { get; set; }
    }

    public sealed class ProductPriceHistoryDTO
    {
        public int ProductId { get; set; }
        public List<ProductPriceChangeDTO> Items { get; set; } = new List<ProductPriceChangeDTO>();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }
}
