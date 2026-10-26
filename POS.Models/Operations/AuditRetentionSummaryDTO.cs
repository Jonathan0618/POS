using System;

namespace POS.Models.Operations
{
    public sealed class AuditRetentionSummaryDTO
    {
        public long TotalEvents { get; set; }
        public DateTime? OldestEventUtc { get; set; }
        public DateTime? NewestEventUtc { get; set; }
        public string Policy => "Retain all audit events. Automatic deletion is disabled.";
    }
}
