using System;
using System.Collections.Generic;

namespace POS.Models.Operations
{
    public sealed class StockMovementSearchDTO
    {
        public int? ProductId { get; set; }
        public int? MovementType { get; set; }
        public DateTime? FromUtc { get; set; }
        public DateTime? ToUtcExclusive { get; set; }
        public string ReferenceType { get; set; }
        public string ReferenceId { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    public sealed class StockMovementDTO
    {
        public long Id { get; set; }
        public int ProductId { get; set; }
        public string CurrentProductName { get; set; }
        public string CurrentSku { get; set; }
        public string MovementType { get; set; }
        public int QuantityDelta { get; set; }
        public string Reason { get; set; }
        public string ReferenceType { get; set; }
        public string ReferenceId { get; set; }
        public string UserId { get; set; }
        public DateTime CreatedUtc { get; set; }
    }

    public sealed class StockMovementPageDTO
    {
        public List<StockMovementDTO> Items { get; set; } = new List<StockMovementDTO>();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }
}
