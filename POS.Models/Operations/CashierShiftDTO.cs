using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace POS.Models.Operations
{
    public sealed class OpenCashierShiftDTO
    {
        public Guid RequestId { get; set; }
        [Range(1, int.MaxValue)] public int RegisterStationId { get; set; }
        public decimal OpeningCash { get; set; }
    }

    public sealed class CashierShiftDTO
    {
        public int Id { get; set; }
        public Guid RequestId { get; set; }
        public int RegisterStationId { get; set; }
        public string RegisterCode { get; set; }
        public string RegisterName { get; set; }
        public string CashierUserId { get; set; }
        public string CashierName { get; set; }
        public string Status { get; set; }
        public DateTime OpenedUtc { get; set; }
        public DateTime? ClosedUtc { get; set; }
        public decimal OpeningCash { get; set; }
        public decimal ExpectedCash { get; set; }
        public decimal CountedCash { get; set; }
        public decimal Variance { get; set; }
        public string Revision { get; set; }
    }

    public sealed class PostCashMovementDTO
    {
        public Guid RequestId { get; set; }
        [Range(1, int.MaxValue)] public int CashierShiftId { get; set; }
        [Required] public string ShiftRevision { get; set; }
        public bool IsCashIn { get; set; }
        public decimal Amount { get; set; }
        [Required, StringLength(250)] public string Reason { get; set; }
    }

    public sealed class CloseCashierShiftDTO
    {
        public Guid RequestId { get; set; }
        [Range(1, int.MaxValue)] public int CashierShiftId { get; set; }
        [Required] public string ShiftRevision { get; set; }
        public decimal CountedCash { get; set; }
    }

    public sealed class CashierShiftSearchDTO
    {
        public int? RegisterStationId { get; set; }
        public string CashierUserId { get; set; }
        public string Status { get; set; }
        public DateTime? FromUtc { get; set; }
        public DateTime? ToUtcExclusive { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    public sealed class CashierShiftPageDTO
    {
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public List<CashierShiftDTO> Items { get; set; } = new List<CashierShiftDTO>();
    }
}
