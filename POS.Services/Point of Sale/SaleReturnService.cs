using POS.Common.Enumerations;
using POS.Core.Abstractions;
using POS.Core.Security;
using POS.Data.Context;
using POS.Domains.BusinessObjects;
using POS.Domains.Operations;
using POS.Models.Operations;
using POS.Services.Security;
using POS.Services.Settings;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace POS.Services.Point_of_Sale
{
    public sealed class SaleReturnService : IDisposable
    {
        private const int ReturnWindowDays = 30;
        private const decimal ApprovalThreshold = 1000m;
        private readonly POSContext _context;
        private readonly IClock _clock;
        private readonly ICurrentUser _currentUser;
        private readonly IAuthorizationService _authorization;
        private readonly NumberSequenceService _sequences;

        public SaleReturnService(POSContext context, IClock clock, ICurrentUser currentUser, IAuthorizationService authorization)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
            _sequences = new NumberSequenceService(_context, _authorization);
        }

        public SaleReturnEligibilityDTO GetEligibility(int saleId)
        {
            PermissionGuard.Demand(_authorization, ResourceCodes.Returns, ClaimActionType.View);
            PermissionGuard.Demand(_authorization, ResourceCodes.Sales, ClaimActionType.View);
            var sale = LoadReturnableSale(saleId, false);
            var returned = ReturnedQuantities(saleId);
            var refunded = RefundedAmounts(saleId);
            var applied = AppliedPaymentAmounts(sale);
            var grossTotal = sale.SaleItems.Sum(x => x.Subtotal + x.TaxAmount - x.DiscountAmount);
            var valueFactor = grossTotal <= 0 ? 0 : sale.TotalAmount / grossTotal;
            var priorTotal = _context.SaleReturns.AsNoTracking()
                .Where(x => x.SaleId == saleId && (x.Status == DocumentStatus.Posted || x.Status == DocumentStatus.Completed))
                .Sum(x => (decimal?)x.TotalAmount) ?? 0m;
            var deadline = sale.SaleDate.AddDays(ReturnWindowDays);
            return new SaleReturnEligibilityDTO
            {
                SaleId = sale.Id,
                ReceiptNumber = sale.ReceiptNumber,
                SaleDateUtc = sale.SaleDate,
                CustomerName = sale.CustomerName,
                OriginalTotal = sale.TotalAmount,
                PreviouslyRefunded = priorTotal,
                ReturnDeadlineUtc = deadline,
                RequiresLateApproval = _clock.UtcNow > deadline,
                Lines = sale.SaleItems.OrderBy(x => x.Id).Select(x =>
                {
                    returned.TryGetValue(x.Id, out var prior);
                    var gross = x.Subtotal + x.TaxAmount - x.DiscountAmount;
                    return new SaleReturnEligibleLineDTO
                    {
                        SaleItemId = x.Id, ProductId = x.ProductId, ProductName = x.ProductName,
                        PurchasedQuantity = x.Quantity, ReturnedQuantity = prior,
                        EligibleQuantity = x.Quantity - prior,
                        ApproximateRefundPerUnit = x.Quantity == 0 ? 0 : decimal.Round(gross * valueFactor / x.Quantity, 2, MidpointRounding.AwayFromZero)
                    };
                }).ToList(),
                Payments = sale.Payments.OrderBy(x => x.Id).Select(x =>
                {
                    applied.TryGetValue(x.Id, out var originalApplied);
                    refunded.TryGetValue(x.Id, out var prior);
                    return new SaleReturnEligiblePaymentDTO
                    {
                        OriginalPaymentId = x.Id, TenderType = x.TenderType,
                        OriginalAppliedAmount = originalApplied, RefundedAmount = prior,
                        EligibleAmount = originalApplied - prior
                    };
                }).ToList()
            };
        }

        public SaleReturnResultDTO Post(SaleReturnPostDTO request)
        {
            PermissionGuard.Demand(_authorization, ResourceCodes.Returns, ClaimActionType.Add);
            var actor = RequireActor();
            ValidateRequest(request);
            var normalizedReason = request.Reason.Trim();
            NormalizeRefundReferences(request);
            var requestHash = ComputeHash(request, normalizedReason);
            if (_context.Database.CurrentTransaction != null || _context.ChangeTracker.HasChanges())
                throw new InvalidOperationException("Posting a return requires a clean context.");
            RestoreTrackedState();

            using (var audit = AuditOperation.Begin(request.RegisterStationId))
            using (var transaction = _context.Database.BeginTransaction(System.Data.IsolationLevel.Serializable))
            {
                try
                {
                    var existing = _context.SaleReturns.AsNoTracking()
                        .SingleOrDefault(x => x.RequestId == request.RequestId);
                    if (existing != null)
                    {
                        if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal) ||
                            !string.Equals(existing.CreatedByUserId, actor, StringComparison.Ordinal))
                            throw new InvalidOperationException("The return request ID was already used for different details or by another user.");
                        transaction.Commit();
                        return ToResult(existing);
                    }

                    var register = _context.RegisterStations.SingleOrDefault(x => x.Id == request.RegisterStationId && x.IsActive);
                    if (register == null) throw new InvalidOperationException("The selected return register is missing or inactive.");
                    var shift = _context.CashierShifts.SingleOrDefault(x => x.Id == request.CashierShiftId);
                    if (shift == null || shift.Status != ShiftStatus.Open)
                        throw new InvalidOperationException("Returns require an open cashier shift.");
                    if (shift.RegisterStationId != request.RegisterStationId)
                        throw new InvalidOperationException("The return shift does not belong to the selected register.");
                    if (!string.Equals(shift.CashierUserId, actor, StringComparison.Ordinal))
                        throw new UnauthorizedAccessException("The return shift belongs to another cashier.");

                    var sale = LoadReturnableSale(request.SaleId, true);
                    if (request.ExchangeSaleId.HasValue)
                    {
                        if (request.ExchangeSaleId.Value == sale.Id || !_context.Sales.Any(x => x.Id == request.ExchangeSaleId.Value &&
                            (x.Status == DocumentStatus.Completed || x.Status == DocumentStatus.PartiallyReturned)))
                            throw new InvalidOperationException("The linked exchange sale is invalid or incomplete.");
                        if (_context.SaleReturns.Any(x => x.ExchangeSaleId == request.ExchangeSaleId.Value))
                            throw new InvalidOperationException("The exchange sale is already linked to another return.");
                    }

                    var returned = ReturnedQuantities(sale.Id);
                    var priorReturnTotal = _context.SaleReturns.Where(x => x.SaleId == sale.Id &&
                        (x.Status == DocumentStatus.Posted || x.Status == DocumentStatus.Completed))
                        .Sum(x => (decimal?)x.TotalAmount) ?? 0m;
                    var selected = new List<Tuple<SaleReturnLinePostDTO, SaleItem, decimal>>();
                    var grossSaleLines = sale.SaleItems.Sum(x => x.Subtotal + x.TaxAmount - x.DiscountAmount);
                    if (grossSaleLines <= 0 || sale.TotalAmount <= 0)
                        throw new InvalidOperationException("The original sale has no refundable value.");
                    var factor = sale.TotalAmount / grossSaleLines;
                    foreach (var lineRequest in request.Lines.OrderBy(x => x.SaleItemId))
                    {
                        var original = sale.SaleItems.SingleOrDefault(x => x.Id == lineRequest.SaleItemId)
                            ?? throw new InvalidOperationException($"Sale line {lineRequest.SaleItemId} does not belong to the original sale.");
                        returned.TryGetValue(original.Id, out var priorQuantity);
                        if (lineRequest.Quantity > original.Quantity - priorQuantity)
                            throw new InvalidOperationException($"Return quantity exceeds the eligible quantity for '{original.ProductName}'.");
                        var grossLine = original.Subtotal + original.TaxAmount - original.DiscountAmount;
                        var amount = decimal.Round(grossLine * factor * lineRequest.Quantity / original.Quantity, 2, MidpointRounding.AwayFromZero);
                        selected.Add(Tuple.Create(lineRequest, original, amount));
                    }

                    var fullyReturned = sale.SaleItems.All(item =>
                    {
                        returned.TryGetValue(item.Id, out var prior);
                        var current = request.Lines.Where(x => x.SaleItemId == item.Id).Sum(x => x.Quantity);
                        return prior + current == item.Quantity;
                    });
                    var returnTotal = selected.Sum(x => x.Item3);
                    var remainingValue = sale.TotalAmount - priorReturnTotal;
                    if (fullyReturned && selected.Count > 0)
                    {
                        var last = selected[selected.Count - 1];
                        selected[selected.Count - 1] = Tuple.Create(last.Item1, last.Item2,
                            last.Item3 + remainingValue - returnTotal);
                        returnTotal = remainingValue;
                    }
                    if (returnTotal <= 0 || returnTotal > remainingValue)
                        throw new InvalidOperationException("The calculated refund exceeds the remaining value of the original sale.");

                    var requiresApproval = returnTotal > ApprovalThreshold ||
                        _clock.UtcNow > sale.SaleDate.AddDays(ReturnWindowDays) ||
                        request.Lines.Any(x => x.Disposition != ReturnDisposition.Restock);
                    if (requiresApproval)
                        PermissionGuard.Demand(_authorization, ResourceCodes.Returns, ClaimActionType.Edit);

                    var priorRefunds = RefundedAmounts(sale.Id);
                    var applied = AppliedPaymentAmounts(sale);
                    decimal refundTotal = 0;
                    var refundRows = new List<RefundPayment>();
                    foreach (var refund in request.Refunds)
                    {
                        var payment = sale.Payments.SingleOrDefault(x => x.Id == refund.OriginalPaymentId)
                            ?? throw new InvalidOperationException("A refund references a payment outside the original sale.");
                        if (payment.Status != PaymentStatus.Completed)
                            throw new InvalidOperationException("Only a completed original payment can be refunded.");
                        if (payment.TenderType == TenderType.Cash && refund.ExternalReference != null)
                            throw new InvalidOperationException("Cash refunds cannot store an external reference.");
                        if (payment.TenderType != TenderType.Cash && refund.ExternalReference == null)
                            throw new InvalidOperationException("Non-cash refunds require a safe provider refund reference.");
                        applied.TryGetValue(payment.Id, out var originalApplied);
                        priorRefunds.TryGetValue(payment.Id, out var prior);
                        if (refund.Amount > originalApplied - prior)
                            throw new InvalidOperationException($"The refund exceeds the remaining {payment.TenderType} payment value.");
                        refundRows.Add(new RefundPayment
                        {
                            OriginalPaymentId = payment.Id, TenderType = payment.TenderType,
                            Amount = refund.Amount, ExternalReference = refund.ExternalReference,
                            CreatedUtc = _clock.UtcNow
                        });
                        refundTotal += refund.Amount;
                    }
                    if (refundTotal != returnTotal)
                        throw new InvalidOperationException("Refund tenders must exactly equal the calculated return total.");

                    var document = new SaleReturn
                    {
                        RequestId = request.RequestId, RequestHash = requestHash,
                        ReturnNumber = _sequences.AllocateReturnNumber(), SaleId = sale.Id,
                        RegisterStationId = request.RegisterStationId, CashierShiftId = request.CashierShiftId,
                        ExchangeSaleId = request.ExchangeSaleId, Status = DocumentStatus.Posted,
                        Reason = normalizedReason, CreatedByUserId = actor,
                        ApprovedByUserId = requiresApproval ? actor : null,
                        TotalAmount = returnTotal, CreatedUtc = _clock.UtcNow
                    };
                    foreach (var selectedLine in selected)
                    {
                        document.Lines.Add(new SaleReturnLine
                        {
                            SaleItemId = selectedLine.Item2.Id, Quantity = selectedLine.Item1.Quantity,
                            RefundAmount = selectedLine.Item3, Disposition = selectedLine.Item1.Disposition
                        });
                        if (selectedLine.Item1.Disposition == ReturnDisposition.Restock)
                            Restock(document.ReturnNumber, selectedLine.Item2, selectedLine.Item1.Quantity, actor, document.CreatedUtc);
                    }
                    foreach (var refund in refundRows) document.Refunds.Add(refund);
                    sale.Status = fullyReturned ? DocumentStatus.Returned : DocumentStatus.PartiallyReturned;
                    _context.SaleReturns.Add(document);
                    _context.SaveChanges();
                    transaction.Commit();
                    return ToResult(document);
                }
                catch (Exception exception)
                {
                    try { transaction.Rollback(); }
                    finally { RestoreTrackedState(); }
                    if (Inventory.InventoryConflict.IsUniqueIndexViolation(exception, "IX_RequestId"))
                        throw new System.ComponentModel.DataAnnotations.ValidationException(
                            "This return was committed concurrently. Retry the same request ID and unchanged details to retrieve it.", exception);
                    if (Inventory.InventoryConflict.IsUniqueIndexViolation(exception, "UX_RefundPayments_ExternalReference"))
                        throw new System.ComponentModel.DataAnnotations.ValidationException(
                            "A refund already uses that provider reference. Review return history before retrying.", exception);
                    if (Inventory.InventoryConflict.IsRecognized(exception))
                        throw new System.ComponentModel.DataAnnotations.ValidationException(
                            "The return conflicted with another stock, sale, or shift operation. Reload eligibility before retrying.", exception);
                    throw;
                }
            }
        }

        private void Restock(string returnNumber, SaleItem line, int quantity, string actor, DateTime createdUtc)
        {
            var product = _context.Set<Product>().SingleOrDefault(x => x.Id == line.ProductId)
                ?? throw new InvalidOperationException("A returned product no longer exists.");
            var balance = _context.InventoryBalances.SingleOrDefault(x => x.ProductId == line.ProductId)
                ?? throw new InvalidOperationException($"Inventory balance is missing for '{line.ProductName}'.");
            var ledger = _context.StockMovements.Where(x => x.ProductId == line.ProductId)
                .Sum(x => (long?)x.QuantityDelta) ?? 0L;
            if (balance.QuantityOnHand != product.Quantity || ledger != balance.QuantityOnHand)
                throw new InvalidOperationException($"Inventory is not reconciled for '{line.ProductName}'.");
            balance.QuantityOnHand = checked(balance.QuantityOnHand + quantity);
            product.Quantity = balance.QuantityOnHand;
            _context.StockMovements.Add(new StockMovement
            {
                ProductId = product.Id, MovementType = StockMovementType.SaleReturn,
                QuantityDelta = quantity, Reason = "Customer return restock",
                ReferenceType = "SaleReturn", ReferenceId = returnNumber,
                UserId = actor, CreatedUtc = createdUtc
            });
        }

        private Sale LoadReturnableSale(int id, bool tracked)
        {
            if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
            var query = tracked ? _context.Sales.AsQueryable() : _context.Sales.AsNoTracking();
            var sale = query.Include(x => x.SaleItems).Include(x => x.Payments).SingleOrDefault(x => x.Id == id);
            if (sale == null || (sale.Status != DocumentStatus.Completed && sale.Status != DocumentStatus.PartiallyReturned))
                throw new InvalidOperationException("The sale is missing or is not eligible for another return.");
            return sale;
        }

        private Dictionary<int, int> ReturnedQuantities(int saleId) => _context.SaleReturnLines.AsNoTracking()
            .Where(x => x.SaleReturn.SaleId == saleId &&
                (x.SaleReturn.Status == DocumentStatus.Posted || x.SaleReturn.Status == DocumentStatus.Completed))
            .GroupBy(x => x.SaleItemId).Select(x => new { x.Key, Quantity = x.Sum(y => y.Quantity) })
            .ToDictionary(x => x.Key, x => x.Quantity);

        private Dictionary<long, decimal> RefundedAmounts(int saleId) => _context.RefundPayments.AsNoTracking()
            .Where(x => x.SaleReturn.SaleId == saleId &&
                (x.SaleReturn.Status == DocumentStatus.Posted || x.SaleReturn.Status == DocumentStatus.Completed))
            .GroupBy(x => x.OriginalPaymentId).Select(x => new { x.Key, Amount = x.Sum(y => y.Amount) })
            .ToDictionary(x => x.Key, x => x.Amount);

        private static Dictionary<long, decimal> AppliedPaymentAmounts(Sale sale)
        {
            var result = sale.Payments.ToDictionary(x => x.Id, x => x.Amount);
            var remainingChange = sale.Change;
            foreach (var cash in sale.Payments.Where(x => x.TenderType == TenderType.Cash).OrderByDescending(x => x.Id))
            {
                var reduction = Math.Min(remainingChange, result[cash.Id]);
                result[cash.Id] -= reduction;
                remainingChange -= reduction;
            }
            return result;
        }

        private static void ValidateRequest(SaleReturnPostDTO request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.RequestId == Guid.Empty || request.SaleId <= 0 || request.RegisterStationId <= 0 || request.CashierShiftId <= 0)
                throw new InvalidOperationException("Return request, sale, register, and shift identifiers are required.");
            if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 250 || request.Reason.Any(char.IsControl))
                throw new InvalidOperationException("A return reason between 1 and 250 printable characters is required.");
            if (request.Lines == null || request.Lines.Count == 0 || request.Lines.Count > 500 ||
                request.Lines.Any(x => x == null || x.SaleItemId <= 0 || x.Quantity <= 0 || !Enum.IsDefined(typeof(ReturnDisposition), x.Disposition)) ||
                request.Lines.GroupBy(x => x.SaleItemId).Any(x => x.Count() > 1))
                throw new InvalidOperationException("Return lines must be unique and contain valid positive quantities and dispositions.");
            if (request.Refunds == null || request.Refunds.Count == 0 || request.Refunds.Count > 10 ||
                request.Refunds.Any(x => x == null || x.OriginalPaymentId <= 0 || x.Amount <= 0 ||
                    x.Amount > 9999999999999999.99m || decimal.Round(x.Amount, 2) != x.Amount) ||
                request.Refunds.GroupBy(x => x.OriginalPaymentId).Any(x => x.Count() > 1))
                throw new InvalidOperationException("Refund tenders must uniquely reference original payments and use positive two-decimal amounts.");
            if (request.ExchangeSaleId.HasValue && request.ExchangeSaleId.Value <= 0)
                throw new InvalidOperationException("The exchange sale identifier is invalid.");
        }

        private static void NormalizeRefundReferences(SaleReturnPostDTO request)
        {
            foreach (var refund in request.Refunds)
            {
                refund.ExternalReference = string.IsNullOrWhiteSpace(refund.ExternalReference) ? null : refund.ExternalReference.Trim();
                if ((refund.ExternalReference?.Length ?? 0) > 100 || (refund.ExternalReference?.Any(char.IsControl) ?? false))
                    throw new InvalidOperationException("Refund references cannot exceed 100 characters or contain control characters.");
            }
        }

        private static string ComputeHash(SaleReturnPostDTO request, string reason)
        {
            var value = new StringBuilder("sale-return|v1|").Append(request.SaleId).Append('|')
                .Append(request.RegisterStationId).Append('|').Append(request.CashierShiftId).Append('|')
                .Append(request.ExchangeSaleId?.ToString(CultureInfo.InvariantCulture) ?? "none").Append('|').Append(reason);
            foreach (var line in request.Lines.OrderBy(x => x.SaleItemId))
                value.Append('|').Append(line.SaleItemId).Append(':').Append(line.Quantity).Append(':').Append((int)line.Disposition);
            foreach (var refund in request.Refunds.OrderBy(x => x.OriginalPaymentId))
                value.Append('|').Append(refund.OriginalPaymentId).Append(':')
                    .Append(refund.Amount.ToString("0.00", CultureInfo.InvariantCulture)).Append(':').Append(refund.ExternalReference ?? string.Empty);
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value.ToString()))).Replace("-", string.Empty);
        }

        private string RequireActor()
        {
            if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUser.UserId))
                throw new UnauthorizedAccessException("An authenticated cashier is required to post a return.");
            return _currentUser.UserId;
        }

        private static SaleReturnResultDTO ToResult(SaleReturn value) => new SaleReturnResultDTO
        {
            Id = value.Id, RequestId = value.RequestId, ReturnNumber = value.ReturnNumber,
            SaleId = value.SaleId, ExchangeSaleId = value.ExchangeSaleId, TotalAmount = value.TotalAmount,
            ApprovedByUserId = value.ApprovedByUserId, CreatedUtc = value.CreatedUtc
        };

        private void RestoreTrackedState()
        {
            foreach (var entry in _context.ChangeTracker.Entries().ToList()) entry.State = EntityState.Detached;
        }

        public void Dispose() { _sequences.Dispose(); }
    }
}
