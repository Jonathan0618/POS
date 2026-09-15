using System;
using System.Collections.Generic;

namespace POS.Models.Operations
{
    public sealed class AuditSearchDTO
    {
        public string RegisterCode { get; set; }
        public bool UnattributedOnly { get; set; }
        public string Category { get; set; }
        public DateTime? FromUtc { get; set; }
        public DateTime? ToUtcExclusive { get; set; }
        public string Search { get; set; }
        public string Entity { get; set; }
        public string Action { get; set; }
        public Guid? CorrelationId { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    public sealed class AuditEventDTO
    {
        public int? RegisterStationId { get; set; }
        public string RegisterCode { get; set; }
        public string Category { get; set; }
        public Guid? CorrelationId { get; set; }
        public long Id { get; set; }
        public DateTime DateLoggedUtc { get; set; }
        public DateTime LocalTime => DateTime.SpecifyKind(DateLoggedUtc, DateTimeKind.Utc).ToLocalTime();
        public string Username { get; set; }
        public string UserId { get; set; }
        public string Entity { get; set; }
        public string RecordId { get; set; }
        public string Action { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }
    }

    public sealed class AuditPageDTO
    {
        public List<AuditEventDTO> Items { get; set; }
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int TotalPages { get; set; }
    }
}
