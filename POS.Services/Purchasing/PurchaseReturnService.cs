using POS.Common.Enumerations;
using POS.Core.Abstractions;
using POS.Core.Security;
using POS.Data.Context;
using POS.Domains.BusinessObjects;
using POS.Domains.Operations;
using POS.Models.Operations;
using POS.Services.Security;
using System;
using System.ComponentModel.DataAnnotations;
using System.Data.Entity;
using System.Linq;

namespace POS.Services.Purchasing
{
    public sealed class PurchaseReturnService
    {
        private readonly POSContext _context;
        private readonly IClock _clock;
        private readonly ICurrentUser _currentUser;
        private readonly IAuthorizationService _authorization;

        public PurchaseReturnService(POSContext context, IClock clock, ICurrentUser currentUser,
            IAuthorizationService authorization)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        }

        public int Post(PurchaseReturnPostDTO request)
        {
            PermissionGuard.Demand(_authorization, ResourceCodes.Purchasing, ClaimActionType.Edit);
            var userId = _currentUser.UserId;
            if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(userId))
                throw new UnauthorizedAccessException("An authenticated user is required to return inventory.");
            Validate(request);
            var returnNumber = request.ReturnNumber.Trim().ToUpperInvariant();
            var reason = request.Reason.Trim();
            if (_context.Database.CurrentTransaction != null || _context.ChangeTracker.HasChanges())
                throw new InvalidOperationException("Post purchase returns using a context without pending changes or an active transaction.");
            ClearTracking();
            using (var audit = AuditOperation.Begin())
            using (var transaction = _context.Database.BeginTransaction(System.Data.IsolationLevel.Serializable))
            {
                try
                {
                    var previous = _context.PurchaseReturns.AsNoTracking()
                        .SingleOrDefault(r => r.ReturnNumber == returnNumber);
                    if (previous != null)
                    {
                        if (!Matches(previous, request, reason))
                            throw new ValidationException("This purchase-return number was already used with different details. Review the original return and use a new number for changed work.");
                        transaction.Commit();
                        return previous.Id;
                    }
                    var receipt = _context.GoodsReceipts.Include(r => r.Lines)
                        .SingleOrDefault(r => r.Id == request.GoodsReceiptId);
                    if (receipt == null) throw new ValidationException("The goods receipt no longer exists. Reload receipts.");
                    if (receipt.Status != DocumentStatus.Posted && receipt.Status != DocumentStatus.PartiallyReturned)
                        throw new ValidationException("Only a posted receipt with remaining quantities can be returned.");

                    var now = _clock.UtcNow;
                    var document = new PurchaseReturn
                    {
                        ReturnNumber = returnNumber, GoodsReceiptId = receipt.Id,
                        SupplierId = receipt.SupplierId, Status = DocumentStatus.Posted,
                        Reason = reason, ReturnedUtc = now
                    };
                    decimal total = 0m;
                    foreach (var input in request.Items.OrderBy(i => i.GoodsReceiptLineId))
                    {
                        var source = receipt.Lines.SingleOrDefault(l => l.Id == input.GoodsReceiptLineId);
                        if (source == null)
                            throw new ValidationException("A return line is not part of the selected goods receipt.");
                        var returned = _context.PurchaseReturnLines.Where(l =>
                            l.GoodsReceiptLineId == source.Id && l.PurchaseReturn.Status == DocumentStatus.Posted)
                            .Sum(l => (int?)l.Quantity) ?? 0;
                        if (input.Quantity > source.Quantity - returned)
                            throw new ValidationException("A return quantity exceeds the remaining quantity from its receipt line.");
                        var product = _context.Set<Product>().SingleOrDefault(p => p.Id == source.ProductId);
                        var balance = _context.InventoryBalances.SingleOrDefault(b => b.ProductId == source.ProductId);
                        if (product == null || balance == null)
                            throw new ValidationException("A returned product or its inventory balance is missing. Reconcile inventory before returning.");
                        var ledger = _context.StockMovements.Where(m => m.ProductId == product.Id)
                            .Sum(m => (long?)m.QuantityDelta) ?? 0L;
                        if (product.Quantity != balance.QuantityOnHand || ledger != balance.QuantityOnHand)
                            throw new ValidationException($"Inventory records disagree for '{product.Name}'. Reconcile inventory before returning.");
                        if (balance.QuantityOnHand < input.Quantity)
                            throw new ValidationException($"There is not enough on-hand inventory to return '{product.Name}' to the supplier.");
                        balance.QuantityOnHand -= input.Quantity;
                        product.Quantity -= input.Quantity;
                        total = checked(total + checked(source.UnitCost * input.Quantity));
                        if (total > 9999999999999999.99m) throw new OverflowException();
                        document.Lines.Add(new PurchaseReturnLine
                        {
                            GoodsReceiptLineId = source.Id, ProductId = source.ProductId,
                            Quantity = input.Quantity, UnitCost = source.UnitCost
                        });
                        _context.StockMovements.Add(new StockMovement
                        {
                            ProductId = source.ProductId, MovementType = StockMovementType.PurchaseReturn,
                            QuantityDelta = -input.Quantity, ReferenceType = "PurchaseReturn",
                            ReferenceId = returnNumber, Reason = reason, UserId = userId, CreatedUtc = now
                        });
                    }
                    document.TotalAmount = total;
                    _context.PurchaseReturns.Add(document);
                    _context.SaveChanges();
                    var allReturned = receipt.Lines.All(line =>
                        (_context.PurchaseReturnLines.Where(x => x.GoodsReceiptLineId == line.Id &&
                            x.PurchaseReturn.Status == DocumentStatus.Posted).Sum(x => (int?)x.Quantity) ?? 0) == line.Quantity);
                    receipt.Status = allReturned ? DocumentStatus.Returned : DocumentStatus.PartiallyReturned;
                    _context.SaveChanges();
                    transaction.Commit();
                    return document.Id;
                }
                catch (OverflowException exception)
                {
                    try { transaction.Rollback(); } finally { ClearTracking(); }
                    throw new ValidationException("The purchase-return total exceeds the supported monetary range.", exception);
                }
                catch (Exception exception)
                {
                    try { transaction.Rollback(); } finally { ClearTracking(); }
                    if (Inventory.InventoryConflict.IsUniqueIndexViolation(exception, "IX_ReturnNumber"))
                        throw new ValidationException("This purchase-return number was committed concurrently. Reload returns and retry the same unchanged request.", exception);
                    if (Inventory.InventoryConflict.IsRecognized(exception))
                        throw new ValidationException("The return conflicted with another inventory operation. Reload the receipt, returns, and inventory before trying again.", exception);
                    throw;
                }
            }
        }

        private bool Matches(PurchaseReturn document, PurchaseReturnPostDTO request, string reason)
        {
            if (document.GoodsReceiptId != request.GoodsReceiptId || document.Status != DocumentStatus.Posted ||
                document.Reason != reason) return false;
            var lines = _context.PurchaseReturnLines.AsNoTracking()
                .Where(l => l.PurchaseReturnId == document.Id).ToList();
            return lines.Count == request.Items.Count && lines.All(line => request.Items.Any(item =>
                item.GoodsReceiptLineId == line.GoodsReceiptLineId && item.Quantity == line.Quantity));
        }

        private static void Validate(PurchaseReturnPostDTO request)
        {
            if (request == null || request.GoodsReceiptId <= 0 || string.IsNullOrWhiteSpace(request.ReturnNumber) ||
                request.ReturnNumber.Trim().Length > 30 || request.ReturnNumber.Any(char.IsControl))
                throw new ValidationException("A valid goods receipt and return number of at most 30 characters are required.");
            if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 250 ||
                request.Reason.Any(char.IsControl))
                throw new ValidationException("A return reason of at most 250 characters is required.");
            if (request.Items == null || request.Items.Count < 1 || request.Items.Count > 500 ||
                request.Items.Any(i => i == null || i.GoodsReceiptLineId <= 0 || i.Quantity <= 0) ||
                request.Items.GroupBy(i => i.GoodsReceiptLineId).Any(g => g.Count() > 1))
                throw new ValidationException("Provide 1-500 distinct receipt lines with positive return quantities.");
        }

        private void ClearTracking()
        {
            foreach (var entry in _context.ChangeTracker.Entries().ToList()) entry.State = EntityState.Detached;
        }
    }
}
