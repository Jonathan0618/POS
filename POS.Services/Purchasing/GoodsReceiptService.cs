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
    public sealed class GoodsReceiptService
    {
        private readonly POSContext _context;
        private readonly IClock _clock;
        private readonly ICurrentUser _currentUser;
        private readonly IAuthorizationService _authorization;

        public GoodsReceiptService(POSContext context, IClock clock, ICurrentUser currentUser,
            IAuthorizationService authorization)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        }

        public int ReceivePurchaseOrder(PurchaseOrderReceiptDTO request)
        {
            PermissionGuard.Demand(_authorization, ResourceCodes.Purchasing, ClaimActionType.Edit);
            var userId = _currentUser.UserId;
            if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(userId))
                throw new UnauthorizedAccessException("An authenticated user is required to receive inventory.");
            Validate(request);
            var receiptNumber = request.ReceiptNumber.Trim().ToUpperInvariant();
            var supplierReference = Clean(request.SupplierReference);
            if (_context.Database.CurrentTransaction != null || _context.ChangeTracker.HasChanges())
                throw new InvalidOperationException("Receive inventory using a context without pending changes or an active transaction.");
            ClearTracking();
            using (var audit = AuditOperation.Begin())
            using (var transaction = _context.Database.BeginTransaction(System.Data.IsolationLevel.Serializable))
            {
                try
                {
                    var previous = _context.GoodsReceipts.AsNoTracking()
                        .SingleOrDefault(r => r.ReceiptNumber == receiptNumber);
                    if (previous != null)
                    {
                        if (!Matches(previous, request, supplierReference))
                            throw new ValidationException("This goods-receipt number was already used with different details. Review the original receipt and use a new number for changed work.");
                        transaction.Commit();
                        return previous.Id;
                    }
                    var order = _context.PurchaseOrders.Include(o => o.Lines)
                        .SingleOrDefault(o => o.Id == request.PurchaseOrderId);
                    if (order == null) throw new ValidationException("The purchase order no longer exists. Reload purchase orders.");
                    if (order.Status != DocumentStatus.Pending)
                        throw new ValidationException("Only an ordered purchase order with remaining quantities can be received.");
                    if (supplierReference != null && _context.GoodsReceipts.Any(r =>
                        r.SupplierId == order.SupplierId && r.SupplierReference == supplierReference))
                        throw new ValidationException("This supplier reference is already recorded for the supplier. Review the existing receipt.");

                    var now = _clock.UtcNow;
                    var receipt = new GoodsReceipt
                    {
                        ReceiptNumber = receiptNumber, SupplierId = order.SupplierId,
                        PurchaseOrderId = order.Id, SupplierReference = supplierReference,
                        Status = DocumentStatus.Posted, ReceivedUtc = now
                    };
                    foreach (var input in request.Items.OrderBy(i => i.ProductId))
                    {
                        var line = order.Lines.SingleOrDefault(l => l.ProductId == input.ProductId);
                        if (line == null) throw new ValidationException("A received product is not present on the purchase order.");
                        var remaining = line.OrderedQuantity - line.ReceivedQuantity;
                        if (remaining <= 0 || input.Quantity > remaining)
                            throw new ValidationException("A received quantity exceeds the remaining ordered quantity.");
                        var product = _context.Set<Product>().SingleOrDefault(p => p.Id == input.ProductId);
                        var balance = _context.InventoryBalances.SingleOrDefault(b => b.ProductId == input.ProductId);
                        if (product == null || balance == null)
                            throw new ValidationException("A received product or its inventory balance is missing. Reconcile inventory before receiving.");
                        var ledger = _context.StockMovements.Where(m => m.ProductId == product.Id)
                            .Sum(m => (long?)m.QuantityDelta) ?? 0L;
                        if (product.Quantity != balance.QuantityOnHand || ledger != balance.QuantityOnHand)
                            throw new ValidationException($"Inventory records disagree for '{product.Name}'. Reconcile inventory before receiving.");
                        var proposed = (long)balance.QuantityOnHand + input.Quantity;
                        if (proposed > int.MaxValue)
                            throw new ValidationException($"Receiving would exceed the supported inventory quantity for '{product.Name}'.");
                        line.ReceivedQuantity += input.Quantity;
                        balance.QuantityOnHand = (int)proposed;
                        product.Quantity = (int)proposed;
                        receipt.Lines.Add(new GoodsReceiptLine
                        {
                            ProductId = product.Id, Quantity = input.Quantity, UnitCost = line.UnitCost
                        });
                        _context.StockMovements.Add(new StockMovement
                        {
                            ProductId = product.Id, MovementType = StockMovementType.PurchaseReceipt,
                            QuantityDelta = input.Quantity, ReferenceType = "GoodsReceipt",
                            ReferenceId = receiptNumber, Reason = "Purchase order receipt",
                            UserId = userId, CreatedUtc = now
                        });
                    }
                    order.Status = order.Lines.All(l => l.ReceivedQuantity == l.OrderedQuantity)
                        ? DocumentStatus.Completed : DocumentStatus.Pending;
                    _context.GoodsReceipts.Add(receipt);
                    _context.SaveChanges();
                    transaction.Commit();
                    return receipt.Id;
                }
                catch (Exception exception)
                {
                    try { transaction.Rollback(); }
                    finally { ClearTracking(); }
                    if (Inventory.InventoryConflict.IsUniqueIndexViolation(exception, "IX_ReceiptNumber"))
                        throw new ValidationException("This goods-receipt number was committed concurrently. Reload receipts and retry the same unchanged request.", exception);
                    if (Inventory.InventoryConflict.IsRecognized(exception))
                        throw new ValidationException("Receiving conflicted with another inventory operation. Reload the order, receipts, and inventory before trying again.", exception);
                    throw;
                }
            }
        }

        public int ReceiveDirect(DirectGoodsReceiptDTO request)
        {
            PermissionGuard.Demand(_authorization, ResourceCodes.Purchasing, ClaimActionType.Add);
            var userId = _currentUser.UserId;
            if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(userId))
                throw new UnauthorizedAccessException("An authenticated user is required to receive inventory.");
            Validate(request);
            var receiptNumber = request.ReceiptNumber.Trim().ToUpperInvariant();
            var supplierReference = Clean(request.SupplierReference);
            if (_context.Database.CurrentTransaction != null || _context.ChangeTracker.HasChanges())
                throw new InvalidOperationException("Receive inventory using a context without pending changes or an active transaction.");
            ClearTracking();
            using (var audit = AuditOperation.Begin())
            using (var transaction = _context.Database.BeginTransaction(System.Data.IsolationLevel.Serializable))
            {
                try
                {
                    var previous = _context.GoodsReceipts.AsNoTracking()
                        .SingleOrDefault(r => r.ReceiptNumber == receiptNumber);
                    if (previous != null)
                    {
                        if (!Matches(previous, request, supplierReference))
                            throw new ValidationException("This goods-receipt number was already used with different details. Review the original receipt and use a new number for changed work.");
                        transaction.Commit();
                        return previous.Id;
                    }
                    if (!_context.Suppliers.Any(s => s.Id == request.SupplierId && s.IsActive))
                        throw new ValidationException("Select an active supplier before posting a direct receipt.");
                    if (supplierReference != null && _context.GoodsReceipts.Any(r =>
                        r.SupplierId == request.SupplierId && r.SupplierReference == supplierReference))
                        throw new ValidationException("This supplier reference is already recorded for the supplier. Review the existing receipt.");

                    var productIds = request.Items.Select(i => i.ProductId).ToList();
                    if (_context.Set<Product>().Count(p => productIds.Contains(p.Id) && p.IsActive) != productIds.Count)
                        throw new ValidationException("Every direct-receipt line must reference an active product.");
                    var now = _clock.UtcNow;
                    var receipt = new GoodsReceipt
                    {
                        ReceiptNumber = receiptNumber, SupplierId = request.SupplierId,
                        PurchaseOrderId = null, SupplierReference = supplierReference,
                        Status = DocumentStatus.Posted, ReceivedUtc = now
                    };
                    foreach (var input in request.Items.OrderBy(i => i.ProductId))
                    {
                        var product = _context.Set<Product>().Single(p => p.Id == input.ProductId);
                        var balance = _context.InventoryBalances.SingleOrDefault(b => b.ProductId == input.ProductId);
                        if (balance == null)
                            throw new ValidationException("A received product has no inventory balance. Reconcile inventory before receiving.");
                        var ledger = _context.StockMovements.Where(m => m.ProductId == product.Id)
                            .Sum(m => (long?)m.QuantityDelta) ?? 0L;
                        if (product.Quantity != balance.QuantityOnHand || ledger != balance.QuantityOnHand)
                            throw new ValidationException($"Inventory records disagree for '{product.Name}'. Reconcile inventory before receiving.");
                        var proposed = (long)balance.QuantityOnHand + input.Quantity;
                        if (proposed > int.MaxValue)
                            throw new ValidationException($"Receiving would exceed the supported inventory quantity for '{product.Name}'.");
                        balance.QuantityOnHand = (int)proposed;
                        product.Quantity = (int)proposed;
                        receipt.Lines.Add(new GoodsReceiptLine
                        {
                            ProductId = product.Id, Quantity = input.Quantity, UnitCost = input.UnitCost
                        });
                        _context.StockMovements.Add(new StockMovement
                        {
                            ProductId = product.Id, MovementType = StockMovementType.PurchaseReceipt,
                            QuantityDelta = input.Quantity, ReferenceType = "GoodsReceipt",
                            ReferenceId = receiptNumber, Reason = "Direct supplier receipt",
                            UserId = userId, CreatedUtc = now
                        });
                    }
                    _context.GoodsReceipts.Add(receipt);
                    _context.SaveChanges();
                    transaction.Commit();
                    return receipt.Id;
                }
                catch (Exception exception)
                {
                    try { transaction.Rollback(); }
                    finally { ClearTracking(); }
                    if (Inventory.InventoryConflict.IsUniqueIndexViolation(exception, "IX_ReceiptNumber"))
                        throw new ValidationException("This goods-receipt number was committed concurrently. Reload receipts and retry the same unchanged request.", exception);
                    if (Inventory.InventoryConflict.IsRecognized(exception))
                        throw new ValidationException("Receiving conflicted with another inventory operation. Reload receipts and inventory before trying again.", exception);
                    throw;
                }
            }
        }

        public ReceiptCostProposalDTO GetCostProposal(int goodsReceiptLineId)
        {
            PermissionGuard.Demand(_authorization, ResourceCodes.Purchasing, ClaimActionType.View);
            PermissionGuard.Demand(_authorization, ResourceCodes.Products, ClaimActionType.View);
            if (goodsReceiptLineId <= 0) throw new ValidationException("Select a valid goods-receipt line.");
            var proposal = _context.GoodsReceiptLines.AsNoTracking().Where(l => l.Id == goodsReceiptLineId)
                .Select(l => new ReceiptCostProposalDTO
                {
                    GoodsReceiptLineId = l.Id, GoodsReceiptId = l.GoodsReceiptId,
                    ReceiptNumber = l.GoodsReceipt.ReceiptNumber, ProductId = l.ProductId,
                    ProductName = l.Product.Name, CurrentCatalogCost = l.Product.CostPrice,
                    ReceivedUnitCost = l.UnitCost
                }).SingleOrDefault();
            if (proposal == null) throw new ValidationException("The goods-receipt line does not exist.");
            return proposal;
        }

        public void ApplyReceivedCost(ApplyReceiptCostDTO request)
        {
            PermissionGuard.Demand(_authorization, ResourceCodes.Purchasing, ClaimActionType.View);
            PermissionGuard.Demand(_authorization, ResourceCodes.Products, ClaimActionType.Edit);
            if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUser.UserId))
                throw new UnauthorizedAccessException("An authenticated user is required to update catalog cost.");
            if (request == null || request.GoodsReceiptLineId <= 0 || request.ExpectedCurrentCatalogCost < 0 ||
                request.ExpectedCurrentCatalogCost > 9999999999999999.99m ||
                decimal.Round(request.ExpectedCurrentCatalogCost, 2) != request.ExpectedCurrentCatalogCost)
                throw new ValidationException("A valid receipt line and reviewed two-decimal catalog cost are required.");
            if (_context.Database.CurrentTransaction != null || _context.ChangeTracker.HasChanges())
                throw new InvalidOperationException("Update catalog cost using a context without pending changes or an active transaction.");
            ClearTracking();
            using (var audit = AuditOperation.Begin())
            using (var transaction = _context.Database.BeginTransaction(System.Data.IsolationLevel.Serializable))
            {
                try
                {
                    var line = _context.GoodsReceiptLines.Include(l => l.GoodsReceipt)
                        .Include(l => l.Product).SingleOrDefault(l => l.Id == request.GoodsReceiptLineId);
                    if (line == null) throw new ValidationException("The goods-receipt line no longer exists. Reload the proposal.");
                    if (!IsPostedReceipt(line.GoodsReceipt.Status))
                        throw new ValidationException("Only a posted receipt can provide a catalog cost.");
                    if (line.Product == null || !line.Product.IsActive)
                        throw new ValidationException("The product is inactive or missing. Review the catalog before changing its cost.");
                    if (line.Product.CostPrice == line.UnitCost)
                    {
                        transaction.Commit();
                        return;
                    }
                    if (line.Product.CostPrice != request.ExpectedCurrentCatalogCost)
                        throw new ValidationException("The catalog cost changed after review. Reload the proposal before applying the received cost.");
                    line.Product.CostPrice = line.UnitCost;
                    _context.SaveChanges();
                    transaction.Commit();
                }
                catch (Exception exception)
                {
                    try { transaction.Rollback(); } finally { ClearTracking(); }
                    if (Inventory.InventoryConflict.IsRecognized(exception))
                        throw new ValidationException("The catalog cost changed concurrently. Reload the proposal before trying again.", exception);
                    throw;
                }
            }
        }

        private bool Matches(GoodsReceipt receipt, PurchaseOrderReceiptDTO request, string supplierReference)
        {
            if (receipt.PurchaseOrderId != request.PurchaseOrderId || !IsPostedReceipt(receipt.Status) ||
                receipt.SupplierReference != supplierReference) return false;
            var lines = _context.GoodsReceiptLines.AsNoTracking().Where(l => l.GoodsReceiptId == receipt.Id).ToList();
            return lines.Count == request.Items.Count && lines.All(line => request.Items.Any(item =>
                item.ProductId == line.ProductId && item.Quantity == line.Quantity));
        }

        private bool Matches(GoodsReceipt receipt, DirectGoodsReceiptDTO request, string supplierReference)
        {
            if (receipt.PurchaseOrderId.HasValue || receipt.SupplierId != request.SupplierId ||
                !IsPostedReceipt(receipt.Status) || receipt.SupplierReference != supplierReference) return false;
            var lines = _context.GoodsReceiptLines.AsNoTracking().Where(l => l.GoodsReceiptId == receipt.Id).ToList();
            return lines.Count == request.Items.Count && lines.All(line => request.Items.Any(item =>
                item.ProductId == line.ProductId && item.Quantity == line.Quantity && item.UnitCost == line.UnitCost));
        }

        private static void Validate(PurchaseOrderReceiptDTO request)
        {
            if (request == null || request.PurchaseOrderId <= 0 || string.IsNullOrWhiteSpace(request.ReceiptNumber) ||
                request.ReceiptNumber.Trim().Length > 30 || request.ReceiptNumber.Any(char.IsControl))
                throw new ValidationException("A valid purchase order and receipt number of at most 30 characters are required.");
            if (request.SupplierReference != null &&
                (request.SupplierReference.Trim().Length > 50 || request.SupplierReference.Any(char.IsControl)))
                throw new ValidationException("Supplier reference cannot exceed 50 characters or contain control characters.");
            if (request.Items == null || request.Items.Count < 1 || request.Items.Count > 500 ||
                request.Items.Any(i => i == null || i.ProductId <= 0 || i.Quantity <= 0) ||
                request.Items.GroupBy(i => i.ProductId).Any(g => g.Count() > 1))
                throw new ValidationException("Provide 1-500 distinct purchase-order products with positive received quantities.");
        }

        private static void Validate(DirectGoodsReceiptDTO request)
        {
            if (request == null || request.SupplierId <= 0 || string.IsNullOrWhiteSpace(request.ReceiptNumber) ||
                request.ReceiptNumber.Trim().Length > 30 || request.ReceiptNumber.Any(char.IsControl))
                throw new ValidationException("A valid supplier and receipt number of at most 30 characters are required.");
            if (request.SupplierReference != null &&
                (request.SupplierReference.Trim().Length > 50 || request.SupplierReference.Any(char.IsControl)))
                throw new ValidationException("Supplier reference cannot exceed 50 characters or contain control characters.");
            if (request.Items == null || request.Items.Count < 1 || request.Items.Count > 500 ||
                request.Items.Any(i => i == null || i.ProductId <= 0 || i.Quantity <= 0 ||
                    i.UnitCost <= 0 || i.UnitCost > 9999999999999999.99m || decimal.Round(i.UnitCost, 2) != i.UnitCost) ||
                request.Items.GroupBy(i => i.ProductId).Any(g => g.Count() > 1))
                throw new ValidationException("Provide 1-500 distinct active products with positive quantities and two-decimal costs.");
        }

        private void ClearTracking()
        {
            foreach (var entry in _context.ChangeTracker.Entries().ToList()) entry.State = EntityState.Detached;
        }
        private static bool IsPostedReceipt(DocumentStatus status) => status == DocumentStatus.Posted ||
            status == DocumentStatus.PartiallyReturned || status == DocumentStatus.Returned;
        private static string Clean(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
