using POS.Common.Enumerations;
using POS.Core.Abstractions;
using POS.Core.Security;
using POS.Data.Context;
using POS.Domains.Operations;
using POS.Models.Operations;
using POS.Services.Security;
using System;
using System.ComponentModel.DataAnnotations;
using System.Data.Entity;
using System.Linq;

namespace POS.Services.Shifts
{
    public sealed class CashierShiftService
    {
        private readonly POSContext _context;
        private readonly IClock _clock;
        private readonly ICurrentUser _currentUser;
        private readonly IAuthorizationService _authorization;

        public CashierShiftService(POSContext context, IClock clock, ICurrentUser currentUser,
            IAuthorizationService authorization)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        }

        public int Open(OpenCashierShiftDTO request)
        {
            Demand(ClaimActionType.Add);
            var userId = RequireUser();
            if (request == null || request.RequestId == Guid.Empty || request.RegisterStationId <= 0 ||
                request.OpeningCash < 0 || request.OpeningCash > 9999999999999999.99m ||
                decimal.Round(request.OpeningCash, 2) != request.OpeningCash)
                throw new ValidationException("A request ID, active register, and nonnegative two-decimal opening cash are required.");
            EnsureCleanContext();
            ClearTracking();
            using (var audit = AuditOperation.Begin(request.RegisterStationId))
            using (var transaction = _context.Database.BeginTransaction(System.Data.IsolationLevel.Serializable))
            {
                try
                {
                    var previous = _context.CashierShifts.AsNoTracking()
                        .SingleOrDefault(s => s.RequestId == request.RequestId);
                    if (previous != null)
                    {
                        if (previous.RegisterStationId != request.RegisterStationId ||
                            previous.CashierUserId != userId || previous.OpeningCash != request.OpeningCash)
                            throw new ValidationException("This shift-opening request ID was already used with different details. Review the original shift.");
                        transaction.Commit(); return previous.Id;
                    }
                    if (!_context.RegisterStations.Any(r => r.Id == request.RegisterStationId && r.IsActive))
                        throw new ValidationException("Select an active register before opening a shift.");
                    if (_context.CashierShifts.Any(s => s.Status == ShiftStatus.Open &&
                        s.RegisterStationId == request.RegisterStationId))
                        throw new ValidationException("This register already has an open cashier shift.");
                    if (_context.CashierShifts.Any(s => s.Status == ShiftStatus.Open && s.CashierUserId == userId))
                        throw new ValidationException("This cashier already has an open shift.");
                    var now = _clock.UtcNow;
                    var shift = new CashierShift
                    {
                        RequestId = request.RequestId, RegisterStationId = request.RegisterStationId,
                        CashierUserId = userId, Status = ShiftStatus.Open, OpenedUtc = now,
                        OpeningCash = request.OpeningCash, ExpectedCash = request.OpeningCash,
                        CountedCash = 0m, Variance = 0m
                    };
                    shift.CashMovements.Add(new CashMovement
                    {
                        MovementType = CashMovementType.Opening, Amount = request.OpeningCash,
                        Reason = "Opening float", UserId = userId, CreatedUtc = now
                    });
                    _context.CashierShifts.Add(shift);
                    _context.SaveChanges(); transaction.Commit(); return shift.Id;
                }
                catch (Exception exception)
                {
                    try { transaction.Rollback(); } finally { ClearTracking(); }
                    if (Inventory.InventoryConflict.IsUniqueIndexViolation(exception, "IX_RequestId"))
                        throw new ValidationException("This shift-opening request was committed concurrently. Reload shifts and retry the unchanged request.", exception);
                    if (Inventory.InventoryConflict.IsUniqueIndexViolation(exception, "IX_OpenShift"))
                        throw new ValidationException("The register or cashier acquired another open shift concurrently. Reload shifts.", exception);
                    if (Inventory.InventoryConflict.IsRecognized(exception))
                        throw new ValidationException("Shift opening conflicted with another operation. Reload shifts before trying again.", exception);
                    throw;
                }
            }
        }

        public long PostCashMovement(PostCashMovementDTO request)
        {
            Demand(ClaimActionType.Edit);
            var userId = RequireUser();
            ValidateMovement(request);
            var reason = request.Reason.Trim();
            EnsureCleanContext(); ClearTracking();
            using (var audit = AuditOperation.Begin())
            using (var transaction = _context.Database.BeginTransaction(System.Data.IsolationLevel.Serializable))
            {
                try
                {
                    var previous = _context.CashMovements.AsNoTracking()
                        .SingleOrDefault(m => m.RequestId == request.RequestId);
                    var type = request.IsCashIn ? CashMovementType.CashIn : CashMovementType.CashOut;
                    if (previous != null)
                    {
                        if (previous.CashierShiftId != request.CashierShiftId || previous.MovementType != type ||
                            previous.Amount != request.Amount || previous.Reason != reason || previous.UserId != userId)
                            throw new ValidationException("This cash-movement request ID was already used with different details. Review the original movement.");
                        transaction.Commit(); return previous.Id;
                    }
                    var shift = _context.CashierShifts.SingleOrDefault(s => s.Id == request.CashierShiftId);
                    if (shift == null) throw new ValidationException("The cashier shift no longer exists. Reload shifts.");
                    if (shift.Status != ShiftStatus.Open)
                        throw new ValidationException("Cash movements can only be posted to an open shift.");
                    if (!string.Equals(request.ShiftRevision, Revision(shift.RowVersion), StringComparison.Ordinal))
                        throw new ValidationException("The shift changed after review. Reload it before posting cash.");
                    decimal expected;
                    try
                    {
                        expected = request.IsCashIn ? checked(shift.ExpectedCash + request.Amount) :
                            checked(shift.ExpectedCash - request.Amount);
                    }
                    catch (OverflowException exception)
                    {
                        throw new ValidationException("The resulting expected cash exceeds the supported monetary range.", exception);
                    }
                    if (expected < 0 || expected > 9999999999999999.99m)
                        throw new ValidationException("Cash-out cannot exceed expected cash and the result must stay within the supported monetary range.");
                    var movement = new CashMovement
                    {
                        RequestId = request.RequestId, CashierShiftId = shift.Id, MovementType = type,
                        Amount = request.Amount, Reason = reason, UserId = userId, CreatedUtc = _clock.UtcNow
                    };
                    shift.ExpectedCash = expected;
                    _context.CashMovements.Add(movement);
                    using (AuditOperation.Begin(shift.RegisterStationId))
                        _context.SaveChanges();
                    transaction.Commit(); return movement.Id;
                }
                catch (Exception exception)
                {
                    try { transaction.Rollback(); } finally { ClearTracking(); }
                    if (Inventory.InventoryConflict.IsUniqueIndexViolation(exception, "IX_RequestId"))
                        throw new ValidationException("This cash-movement request was committed concurrently. Reload the shift and retry the unchanged request.", exception);
                    if (Inventory.InventoryConflict.IsRecognized(exception))
                        throw new ValidationException("The shift changed concurrently. Reload it before trying again.", exception);
                    throw;
                }
            }
        }

        public CashierShiftDTO GetDetails(int cashierShiftId)
        {
            Demand(ClaimActionType.View);
            if (cashierShiftId <= 0) throw new ValidationException("Select an existing cashier shift.");
            var shift = _context.CashierShifts.AsNoTracking().Where(s => s.Id == cashierShiftId)
                .Select(s => new
                {
                    Entity = s, RegisterCode = s.RegisterStation.Code,
                    RegisterName = s.RegisterStation.Name,
                    CashierName = _context.Users.Where(u => u.Id == s.CashierUserId)
                        .Select(u => u.UserName).FirstOrDefault()
                }).SingleOrDefault();
            if (shift == null) throw new ValidationException("The cashier shift does not exist.");
            var result = ToDto(shift.Entity, shift.RegisterCode, shift.RegisterName, shift.CashierName);
            if (shift.Entity.Status == ShiftStatus.Open) result.ExpectedCash = CalculateExpected(shift.Entity.Id);
            return result;
        }

        public CashierShiftPageDTO Search(CashierShiftSearchDTO search)
        {
            Demand(ClaimActionType.View);
            if (search == null) throw new ValidationException("Shift search options are required.");
            if (search.RegisterStationId.HasValue && search.RegisterStationId.Value <= 0)
                throw new ValidationException("Select a valid register or search all registers.");
            if ((search.FromUtc.HasValue && search.FromUtc.Value.Kind != DateTimeKind.Utc) ||
                (search.ToUtcExclusive.HasValue && search.ToUtcExclusive.Value.Kind != DateTimeKind.Utc) ||
                (search.FromUtc.HasValue && search.ToUtcExclusive.HasValue && search.FromUtc >= search.ToUtcExclusive))
                throw new ValidationException("Shift dates must be a valid UTC range.");
            var cashier = string.IsNullOrWhiteSpace(search.CashierUserId) ? null : search.CashierUserId.Trim();
            if (cashier?.Length > 128) throw new ValidationException("Cashier filter cannot exceed 128 characters.");
            ShiftStatus parsed;
            ShiftStatus? status = null;
            int numeric;
            if (!string.IsNullOrWhiteSpace(search.Status))
            {
                if (int.TryParse(search.Status, out numeric) || !Enum.TryParse(search.Status.Trim(), true, out parsed) ||
                    !Enum.IsDefined(typeof(ShiftStatus), parsed))
                    throw new ValidationException("Select a valid shift status or leave status empty.");
                status = parsed;
            }
            IQueryable<CashierShift> query = _context.CashierShifts.AsNoTracking();
            if (search.RegisterStationId.HasValue) query = query.Where(s => s.RegisterStationId == search.RegisterStationId.Value);
            if (cashier != null) query = query.Where(s => s.CashierUserId == cashier);
            if (status.HasValue) query = query.Where(s => s.Status == status.Value);
            if (search.FromUtc.HasValue) query = query.Where(s => s.OpenedUtc >= search.FromUtc.Value);
            if (search.ToUtcExclusive.HasValue) query = query.Where(s => s.OpenedUtc < search.ToUtcExclusive.Value);
            var total = query.Count();
            var size = Math.Max(1, Math.Min(200, search.PageSize));
            var pages = Math.Max(1, (int)Math.Ceiling(total / (double)size));
            var page = Math.Max(1, Math.Min(pages, search.PageNumber));
            var rows = query.OrderByDescending(s => s.OpenedUtc).ThenByDescending(s => s.Id)
                .Skip((page - 1) * size).Take(size).Select(s => new
                {
                    Entity = s, RegisterCode = s.RegisterStation.Code, RegisterName = s.RegisterStation.Name,
                    CashierName = _context.Users.Where(u => u.Id == s.CashierUserId)
                        .Select(u => u.UserName).FirstOrDefault()
                }).ToList();
            var items = rows.Select(r => ToDto(r.Entity, r.RegisterCode, r.RegisterName, r.CashierName)).ToList();
            foreach (var item in items.Where(i => i.Status == ShiftStatus.Open.ToString()))
                item.ExpectedCash = CalculateExpected(item.Id);
            return new CashierShiftPageDTO
            {
                TotalCount = total, TotalPages = pages, PageNumber = page, PageSize = size,
                Items = items
            };
        }

        public void Close(CloseCashierShiftDTO request)
        {
            Demand(ClaimActionType.Edit);
            var userId = RequireUser();
            if (request == null || request.RequestId == Guid.Empty || request.CashierShiftId <= 0 ||
                string.IsNullOrWhiteSpace(request.ShiftRevision) || request.CountedCash < 0 ||
                request.CountedCash > 9999999999999999.99m || decimal.Round(request.CountedCash, 2) != request.CountedCash)
                throw new ValidationException("A request ID, reviewed shift, and nonnegative two-decimal cash count are required.");
            EnsureCleanContext(); ClearTracking();
            using (var audit = AuditOperation.Begin())
            using (var transaction = _context.Database.BeginTransaction(System.Data.IsolationLevel.Serializable))
            {
                try
                {
                    var previous = _context.CashMovements.AsNoTracking()
                        .SingleOrDefault(m => m.RequestId == request.RequestId);
                    if (previous != null)
                    {
                        if (previous.CashierShiftId != request.CashierShiftId ||
                            previous.MovementType != CashMovementType.Closing ||
                            previous.Amount != request.CountedCash || previous.UserId != userId)
                            throw new ValidationException("This shift-closing request ID was already used with different details. Review the original close.");
                        transaction.Commit(); return;
                    }
                    var shift = _context.CashierShifts.SingleOrDefault(s => s.Id == request.CashierShiftId);
                    if (shift == null) throw new ValidationException("The cashier shift no longer exists. Reload shifts.");
                    if (shift.Status != ShiftStatus.Open)
                        throw new ValidationException("Only an open cashier shift can be closed.");
                    if (!string.Equals(request.ShiftRevision, Revision(shift.RowVersion), StringComparison.Ordinal))
                        throw new ValidationException("The shift changed after review. Reload and recount before closing.");
                    var expected = CalculateExpected(shift.Id);
                    var variance = request.CountedCash - expected;
                    if (variance != 0m) Demand(ClaimActionType.Delete);
                    var now = _clock.UtcNow;
                    shift.ExpectedCash = expected; shift.CountedCash = request.CountedCash;
                    shift.Variance = variance; shift.Status = ShiftStatus.Closed; shift.ClosedUtc = now;
                    _context.CashMovements.Add(new CashMovement
                    {
                        RequestId = request.RequestId, CashierShiftId = shift.Id,
                        MovementType = CashMovementType.Closing, Amount = request.CountedCash,
                        Reason = "Shift closing count", UserId = userId, CreatedUtc = now
                    });
                    using (AuditOperation.Begin(shift.RegisterStationId)) _context.SaveChanges();
                    transaction.Commit();
                }
                catch (Exception exception)
                {
                    try { transaction.Rollback(); } finally { ClearTracking(); }
                    if (Inventory.InventoryConflict.IsUniqueIndexViolation(exception, "IX_RequestId"))
                        throw new ValidationException("This shift-closing request was committed concurrently. Reload the shift and retry the unchanged request.", exception);
                    if (Inventory.InventoryConflict.IsRecognized(exception))
                        throw new ValidationException("The shift changed concurrently. Reload and recount before trying again.", exception);
                    throw;
                }
            }
        }

        private decimal CalculateExpected(int shiftId)
        {
            var openingAndMovement = _context.CashMovements.Where(m => m.CashierShiftId == shiftId &&
                (m.MovementType == CashMovementType.Opening || m.MovementType == CashMovementType.CashIn ||
                 m.MovementType == CashMovementType.CashOut))
                .Sum(m => (decimal?)(m.MovementType == CashMovementType.CashOut ? -m.Amount : m.Amount)) ?? 0m;
            var cashSales = _context.Payments.Where(p => p.Sale.CashierShiftId == shiftId &&
                p.TenderType == TenderType.Cash && p.Status == PaymentStatus.Completed)
                .Sum(p => (decimal?)p.Amount) ?? 0m;
            var cashRefunds = _context.RefundPayments.Where(p => p.SaleReturn.CashierShiftId == shiftId &&
                p.TenderType == TenderType.Cash &&
                (p.SaleReturn.Status == DocumentStatus.Posted || p.SaleReturn.Status == DocumentStatus.Completed))
                .Sum(p => (decimal?)p.Amount) ?? 0m;
            try
            {
                var result = checked(openingAndMovement + cashSales - cashRefunds);
                if (result < 0 || result > 9999999999999999.99m)
                    throw new ValidationException("Calculated expected cash is outside the supported monetary range. Reconcile the shift before closing.");
                return result;
            }
            catch (OverflowException exception)
            {
                throw new ValidationException("Calculated expected cash exceeds the supported monetary range.", exception);
            }
        }

        private static CashierShiftDTO ToDto(CashierShift shift, string registerCode,
            string registerName, string cashierName) => new CashierShiftDTO
        {
            Id = shift.Id, RequestId = shift.RequestId, RegisterStationId = shift.RegisterStationId,
            RegisterCode = registerCode, RegisterName = registerName,
            CashierUserId = shift.CashierUserId, CashierName = cashierName,
            Status = shift.Status.ToString(), OpenedUtc = DateTime.SpecifyKind(shift.OpenedUtc, DateTimeKind.Utc),
            ClosedUtc = shift.ClosedUtc.HasValue ? (DateTime?)DateTime.SpecifyKind(shift.ClosedUtc.Value, DateTimeKind.Utc) : null,
            OpeningCash = shift.OpeningCash, ExpectedCash = shift.ExpectedCash,
            CountedCash = shift.CountedCash, Variance = shift.Variance,
            Revision = Revision(shift.RowVersion)
        };

        private static void ValidateMovement(PostCashMovementDTO request)
        {
            if (request == null || request.RequestId == Guid.Empty || request.CashierShiftId <= 0 ||
                string.IsNullOrWhiteSpace(request.ShiftRevision) || request.Amount <= 0 ||
                request.Amount > 9999999999999999.99m || decimal.Round(request.Amount, 2) != request.Amount ||
                string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 250 ||
                request.Reason.Any(char.IsControl))
                throw new ValidationException("A request ID, reviewed shift, positive two-decimal amount, and reason of at most 250 characters are required.");
        }

        private static string Revision(byte[] rowVersion) => rowVersion == null ? null : Convert.ToBase64String(rowVersion);

        private string RequireUser()
        {
            if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUser.UserId))
                throw new UnauthorizedAccessException("An authenticated cashier is required for shift operations.");
            return _currentUser.UserId;
        }
        private void EnsureCleanContext()
        {
            if (_context.Database.CurrentTransaction != null || _context.ChangeTracker.HasChanges())
                throw new InvalidOperationException("Use a clean context without pending changes or an active transaction for shift operations.");
        }
        private void ClearTracking()
        {
            foreach (var entry in _context.ChangeTracker.Entries().ToList()) entry.State = EntityState.Detached;
        }
        private void Demand(ClaimActionType action) => PermissionGuard.Demand(_authorization, ResourceCodes.Shifts, action);
    }
}
