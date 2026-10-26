using POS.Common.Enumerations;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace POS.Domains.Operations
{
    public class CashierShift
    {
        public CashierShift() { CashMovements = new HashSet<CashMovement>(); }
        public int Id { get; set; }
        public Guid RequestId { get; set; }
        public int RegisterStationId { get; set; }
        [Required, StringLength(128)] public string CashierUserId { get; set; }
        public ShiftStatus Status { get; set; }
        public DateTime OpenedUtc { get; set; }
        public DateTime? ClosedUtc { get; set; }
        public decimal OpeningCash { get; set; }
        public decimal ExpectedCash { get; set; }
        public decimal CountedCash { get; set; }
        public decimal Variance { get; set; }
        [Timestamp] public byte[] RowVersion { get; set; }
        public virtual RegisterStation RegisterStation { get; set; }
        public virtual ICollection<CashMovement> CashMovements { get; set; }
    }

    public class CashMovement
    {
        public long Id { get; set; }
        public Guid RequestId { get; set; }
        public int CashierShiftId { get; set; }
        public CashMovementType MovementType { get; set; }
        public decimal Amount { get; set; }
        [Required, StringLength(250)] public string Reason { get; set; }
        [Required, StringLength(128)] public string UserId { get; set; }
        public DateTime CreatedUtc { get; set; }
        public virtual CashierShift CashierShift { get; set; }
    }
}
