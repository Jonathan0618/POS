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
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace POS.Services.Purchasing
{
    public sealed class PurchaseOrderService
    {
        private readonly POSContext _context;
        private readonly IClock _clock;
        private readonly IAuthorizationService _authorization;

        public PurchaseOrderService(POSContext context, IClock clock, IAuthorizationService authorization)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        }

        public int CreateDraft(PurchaseOrderDraftDTO request)
        {
            PermissionGuard.Demand(_authorization, ResourceCodes.Purchasing, ClaimActionType.Add);
            ValidateDraft(request);
            var orderNumber = request.OrderNumber.Trim().ToUpperInvariant();
            var total = CalculateTotal(request);
            if (_context.Database.CurrentTransaction != null || _context.ChangeTracker.HasChanges())
                throw new InvalidOperationException("Create purchase orders using a context without pending changes or an active transaction.");
            ClearTracking();
            using (var audit = AuditOperation.Begin())
            using (var transaction = _context.Database.BeginTransaction(System.Data.IsolationLevel.Serializable))
            {
                try
                {
                    var existing = _context.PurchaseOrders.AsNoTracking()
                        .SingleOrDefault(o => o.OrderNumber == orderNumber);
                    if (existing != null)
                    {
                        if (!Matches(existing.Id, request, total))
                            throw new ValidationException("This purchase-order number was already used with different details. Review the original order and use a new number for changed work.");
                        transaction.Commit();
                        return existing.Id;
                    }
                    if (!_context.Suppliers.Any(s => s.Id == request.SupplierId && s.IsActive))
                        throw new ValidationException("Select an active supplier before creating a purchase order.");
                    var productIds = request.Items.Select(i => i.ProductId).ToList();
                    if (_context.Set<Product>().Count(p => productIds.Contains(p.Id) && p.IsActive) != productIds.Count)
                        throw new ValidationException("Every purchase-order line must reference an active product.");
                    var order = new PurchaseOrder
                    {
                        OrderNumber = orderNumber, SupplierId = request.SupplierId,
                        Status = DocumentStatus.Draft, CreatedUtc = _clock.UtcNow,
                        TotalAmount = total
                    };
                    foreach (var item in request.Items.OrderBy(i => i.ProductId))
                        order.Lines.Add(new PurchaseOrderLine
                        {
                            ProductId = item.ProductId, OrderedQuantity = item.Quantity,
                            ReceivedQuantity = 0, UnitCost = item.UnitCost
                        });
                    _context.PurchaseOrders.Add(order);
                    _context.SaveChanges();
                    transaction.Commit();
                    return order.Id;
                }
                catch (Exception exception)
                {
                    try { transaction.Rollback(); }
                    finally { ClearTracking(); }
                    if (Inventory.InventoryConflict.IsUniqueIndexViolation(exception, "IX_OrderNumber"))
                        throw new ValidationException("This purchase-order number was committed concurrently. Reload purchase orders and retry the same unchanged request.", exception);
                    throw;
                }
            }
        }

        public PurchaseOrderPageDTO Search(PurchaseOrderSearchDTO search)
        {
            PermissionGuard.Demand(_authorization, ResourceCodes.Purchasing, ClaimActionType.View);
            if (search == null) throw new ValidationException("Purchase-order search options are required.");
            var term = string.IsNullOrWhiteSpace(search.Search) ? null : search.Search.Trim();
            if (term?.Length > 100) throw new ValidationException("Purchase-order search cannot exceed 100 characters.");
            if (search.SupplierId.HasValue && search.SupplierId.Value <= 0)
                throw new ValidationException("Select a valid supplier or search all suppliers.");
            if (search.ProductId.HasValue && search.ProductId.Value <= 0)
                throw new ValidationException("Select a valid product or search all products.");
            var status = ParseStatus(search.Status);
            ValidateUtcRange(search.FromUtc, search.ToUtcExclusive);
            IQueryable<PurchaseOrder> query = _context.PurchaseOrders.AsNoTracking();
            if (term != null) query = query.Where(o => o.OrderNumber.Contains(term) ||
                o.Supplier.Code.Contains(term) || o.Supplier.Name.Contains(term));
            if (search.SupplierId.HasValue) query = query.Where(o => o.SupplierId == search.SupplierId.Value);
            if (search.ProductId.HasValue)
                query = query.Where(o => o.Lines.Any(line => line.ProductId == search.ProductId.Value));
            if (status.HasValue) query = query.Where(o => o.Status == status.Value);
            if (search.FromUtc.HasValue) query = query.Where(o => o.CreatedUtc >= search.FromUtc.Value);
            if (search.ToUtcExclusive.HasValue) query = query.Where(o => o.CreatedUtc < search.ToUtcExclusive.Value);
            var pageSize = Math.Max(1, Math.Min(200, search.PageSize));
            var totalCount = query.Count();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            var pageNumber = Math.Max(1, Math.Min(totalPages, search.PageNumber));
            var rows = query.OrderByDescending(o => o.CreatedUtc).ThenByDescending(o => o.Id)
                .Skip((pageNumber - 1) * pageSize).Take(pageSize).Select(o => new
                {
                    o.Id, o.OrderNumber, o.SupplierId, SupplierCode = o.Supplier.Code,
                    SupplierName = o.Supplier.Name, o.Status, o.CreatedUtc, o.OrderedUtc,
                    o.CancelledUtc, o.CancelReason,
                    o.TotalAmount, LineCount = o.Lines.Count()
                }).ToList();
            return new PurchaseOrderPageDTO
            {
                TotalCount = totalCount, TotalPages = totalPages, PageNumber = pageNumber, PageSize = pageSize,
                Items = rows.Select(o => ToSummary(o.Id, o.OrderNumber, o.SupplierId,
                    o.SupplierCode, o.SupplierName, o.Status, o.CreatedUtc, o.OrderedUtc,
                    o.CancelledUtc, o.CancelReason, o.TotalAmount, o.LineCount)).ToList()
            };
        }

        public void UpdateDraft(PurchaseOrderDraftUpdateDTO request)
        {
            PermissionGuard.Demand(_authorization, ResourceCodes.Purchasing, ClaimActionType.Edit);
            if (request == null || request.PurchaseOrderId <= 0 || request.SupplierId <= 0 ||
                string.IsNullOrWhiteSpace(request.Revision))
                throw new ValidationException("Load an existing purchase-order draft and retain its revision before editing.");
            ValidateLines(request.Items);
            var total = CalculateTotal(request.Items);
            if (_context.Database.CurrentTransaction != null || _context.ChangeTracker.HasChanges())
                throw new InvalidOperationException("Edit purchase orders using a context without pending changes or an active transaction.");
            ClearTracking();
            using (var audit = AuditOperation.Begin())
            using (var transaction = _context.Database.BeginTransaction(System.Data.IsolationLevel.Serializable))
            {
                try
                {
                    var order = _context.PurchaseOrders.Include(o => o.Lines)
                        .SingleOrDefault(o => o.Id == request.PurchaseOrderId);
                    if (order == null) throw new ValidationException("The purchase order no longer exists. Reload purchase orders.");
                    if (order.Status != DocumentStatus.Draft)
                        throw new ValidationException("Only a draft purchase order can be edited.");
                    var current = CreateRevisionDetails(order, order.Lines.ToList());
                    if (!string.Equals(request.Revision, CalculateRevision(current), StringComparison.Ordinal))
                        throw new ValidationException("The purchase order changed. Reload and review it before saving edits.");
                    if (!_context.Suppliers.Any(s => s.Id == request.SupplierId && s.IsActive))
                        throw new ValidationException("Select an active supplier before saving the purchase order.");
                    var productIds = request.Items.Select(i => i.ProductId).ToList();
                    if (_context.Set<Product>().Count(p => productIds.Contains(p.Id) && p.IsActive) != productIds.Count)
                        throw new ValidationException("Every purchase-order line must reference an active product.");

                    _context.PurchaseOrderLines.RemoveRange(order.Lines);
                    order.SupplierId = request.SupplierId;
                    order.TotalAmount = total;
                    foreach (var item in request.Items.OrderBy(i => i.ProductId))
                        order.Lines.Add(new PurchaseOrderLine
                        {
                            ProductId = item.ProductId, OrderedQuantity = item.Quantity,
                            ReceivedQuantity = 0, UnitCost = item.UnitCost
                        });
                    _context.SaveChanges();
                    transaction.Commit();
                }
                catch (Exception exception)
                {
                    try { transaction.Rollback(); }
                    finally { ClearTracking(); }
                    ThrowWriteConflict(exception);
                    throw;
                }
            }
        }

        public void OrderDraft(PurchaseOrderTransitionDTO request)
        {
            Transition(request, false);
        }

        public void Cancel(PurchaseOrderTransitionDTO request)
        {
            Transition(request, true);
        }

        private void Transition(PurchaseOrderTransitionDTO request, bool cancel)
        {
            PermissionGuard.Demand(_authorization, ResourceCodes.Purchasing, ClaimActionType.Edit);
            if (request == null || request.PurchaseOrderId <= 0 || string.IsNullOrWhiteSpace(request.Revision))
                throw new ValidationException("Load the current purchase order and retain its revision before changing status.");
            var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
            if (cancel && (reason == null || reason.Length > 250))
                throw new ValidationException("Cancellation requires a reason of at most 250 characters.");
            if (_context.Database.CurrentTransaction != null || _context.ChangeTracker.HasChanges())
                throw new InvalidOperationException("Change purchase-order status using a context without pending changes or an active transaction.");
            ClearTracking();
            using (var audit = AuditOperation.Begin())
            using (var transaction = _context.Database.BeginTransaction(System.Data.IsolationLevel.Serializable))
            {
                try
                {
                    var order = _context.PurchaseOrders.Include(o => o.Lines)
                        .SingleOrDefault(o => o.Id == request.PurchaseOrderId);
                    if (order == null) throw new ValidationException("The purchase order no longer exists. Reload purchase orders.");
                    var current = CreateRevisionDetails(order, order.Lines.ToList());
                    if (!string.Equals(request.Revision, CalculateRevision(current), StringComparison.Ordinal))
                        throw new ValidationException("The purchase order changed. Reload and review it before changing status.");
                    if (cancel)
                    {
                        if (order.Status != DocumentStatus.Draft && order.Status != DocumentStatus.Pending)
                            throw new ValidationException("Only draft or ordered purchase orders without receipts can be cancelled.");
                        if (order.Lines.Any(l => l.ReceivedQuantity != 0) ||
                            _context.GoodsReceipts.Any(r => r.PurchaseOrderId == order.Id))
                            throw new ValidationException("This purchase order has receiving activity and cannot be cancelled. Use a purchase return for received goods.");
                        order.Status = DocumentStatus.Cancelled;
                        order.CancelReason = reason;
                        order.CancelledUtc = _clock.UtcNow;
                    }
                    else
                    {
                        if (order.Status != DocumentStatus.Draft)
                            throw new ValidationException("Only a draft purchase order can be ordered.");
                        if (order.Lines.Count == 0)
                            throw new ValidationException("A purchase order must contain at least one line before ordering.");
                        if (!_context.Suppliers.Any(s => s.Id == order.SupplierId && s.IsActive))
                            throw new ValidationException("The supplier is inactive. Review the order before submitting it.");
                        var productIds = order.Lines.Select(l => l.ProductId).ToList();
                        if (_context.Set<Product>().Count(p => productIds.Contains(p.Id) && p.IsActive) != productIds.Count)
                            throw new ValidationException("An ordered product is inactive. Review the order before submitting it.");
                        order.Status = DocumentStatus.Pending;
                        order.OrderedUtc = _clock.UtcNow;
                        order.CancelReason = null;
                        order.CancelledUtc = null;
                    }
                    _context.SaveChanges();
                    transaction.Commit();
                }
                catch (Exception exception)
                {
                    try { transaction.Rollback(); }
                    finally { ClearTracking(); }
                    ThrowWriteConflict(exception);
                    throw;
                }
            }
        }

        public PurchaseOrderDetailsDTO GetDetails(int purchaseOrderId)
        {
            PermissionGuard.Demand(_authorization, ResourceCodes.Purchasing, ClaimActionType.View);
            if (purchaseOrderId <= 0) throw new ValidationException("Select an existing purchase order.");
            var order = _context.PurchaseOrders.AsNoTracking().Where(o => o.Id == purchaseOrderId)
                .Select(o => new
                {
                    o.Id, o.OrderNumber, o.SupplierId, SupplierCode = o.Supplier.Code,
                    SupplierName = o.Supplier.Name, o.Status, o.CreatedUtc, o.OrderedUtc,
                    o.CancelledUtc, o.CancelReason,
                    o.TotalAmount, LineCount = o.Lines.Count()
                }).SingleOrDefault();
            if (order == null) throw new ValidationException("The purchase order does not exist.");
            var lines = _context.PurchaseOrderLines.AsNoTracking().Where(l => l.PurchaseOrderId == purchaseOrderId)
                .OrderBy(l => l.ProductId).ThenBy(l => l.Id).Select(l => new PurchaseOrderLineDTO
                {
                    Id = l.Id, ProductId = l.ProductId, CurrentProductName = l.Product.Name,
                    CurrentSku = l.Product.Sku, OrderedQuantity = l.OrderedQuantity,
                    ReceivedQuantity = l.ReceivedQuantity, UnitCost = l.UnitCost
                }).ToList();
            var result = new PurchaseOrderDetailsDTO
            {
                Order = ToSummary(order.Id, order.OrderNumber, order.SupplierId, order.SupplierCode,
                    order.SupplierName, order.Status, order.CreatedUtc, order.OrderedUtc,
                    order.CancelledUtc, order.CancelReason, order.TotalAmount, order.LineCount), Items = lines
            };
            result.Revision = CalculateRevision(result);
            return result;
        }

        private bool Matches(int orderId, PurchaseOrderDraftDTO request, decimal total)
        {
            var order = _context.PurchaseOrders.AsNoTracking().Single(o => o.Id == orderId);
            if (order.SupplierId != request.SupplierId || order.Status != DocumentStatus.Draft ||
                order.TotalAmount != total) return false;
            var lines = _context.PurchaseOrderLines.AsNoTracking().Where(l => l.PurchaseOrderId == orderId).ToList();
            return lines.Count == request.Items.Count && lines.All(line => request.Items.Any(item =>
                item.ProductId == line.ProductId && item.Quantity == line.OrderedQuantity &&
                item.UnitCost == line.UnitCost && line.ReceivedQuantity == 0));
        }

        private static PurchaseOrderSummaryDTO ToSummary(int id, string number, int supplierId,
            string supplierCode, string supplierName, DocumentStatus status, DateTime createdUtc,
            DateTime? orderedUtc, DateTime? cancelledUtc, string cancelReason, decimal total, int lineCount)
        {
            return new PurchaseOrderSummaryDTO
            {
                Id = id, OrderNumber = number, SupplierId = supplierId,
                SupplierCode = supplierCode, SupplierName = supplierName,
                Status = status.ToString(), CreatedUtc = DateTime.SpecifyKind(createdUtc, DateTimeKind.Utc),
                OrderedUtc = orderedUtc.HasValue
                    ? (DateTime?)DateTime.SpecifyKind(orderedUtc.Value, DateTimeKind.Utc) : null,
                CancelledUtc = cancelledUtc.HasValue
                    ? (DateTime?)DateTime.SpecifyKind(cancelledUtc.Value, DateTimeKind.Utc) : null,
                CancelReason = cancelReason,
                TotalAmount = total, LineCount = lineCount
            };
        }

        private static DocumentStatus? ParseStatus(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            DocumentStatus parsed;
            int numeric;
            if (int.TryParse(value.Trim(), out numeric) || !Enum.TryParse(value.Trim(), true, out parsed) ||
                (parsed != DocumentStatus.Draft && parsed != DocumentStatus.Pending &&
                 parsed != DocumentStatus.Completed && parsed != DocumentStatus.Cancelled))
                throw new ValidationException("Select a valid purchase-order status or leave status empty.");
            return parsed;
        }

        private static void ValidateUtcRange(DateTime? fromUtc, DateTime? toUtcExclusive)
        {
            if ((fromUtc.HasValue && fromUtc.Value.Kind != DateTimeKind.Utc) ||
                (toUtcExclusive.HasValue && toUtcExclusive.Value.Kind != DateTimeKind.Utc))
                throw new ValidationException("Purchase-order date filters must use UTC.");
            if (fromUtc.HasValue && toUtcExclusive.HasValue && fromUtc >= toUtcExclusive)
                throw new ValidationException("The end of the purchase-order range must be after its start.");
        }

        private static string CalculateRevision(PurchaseOrderDetailsDTO details)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    writer.Write(details.Order.Id); writer.Write(details.Order.OrderNumber);
                    writer.Write(details.Order.SupplierId); writer.Write(details.Order.Status);
                    writer.Write(details.Order.CreatedUtc.Ticks);
                    writer.Write(details.Order.OrderedUtc.HasValue);
                    if (details.Order.OrderedUtc.HasValue) writer.Write(details.Order.OrderedUtc.Value.Ticks);
                    writer.Write(details.Order.CancelledUtc.HasValue);
                    if (details.Order.CancelledUtc.HasValue) writer.Write(details.Order.CancelledUtc.Value.Ticks);
                    writer.Write(details.Order.CancelReason != null);
                    if (details.Order.CancelReason != null) writer.Write(details.Order.CancelReason);
                    writer.Write(details.Order.TotalAmount.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    foreach (var line in details.Items.OrderBy(l => l.ProductId).ThenBy(l => l.Id))
                    {
                        writer.Write(line.Id); writer.Write(line.ProductId);
                        writer.Write(line.OrderedQuantity); writer.Write(line.ReceivedQuantity);
                        writer.Write(line.UnitCost.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    }
                }
                using (var hash = SHA256.Create())
                    return Convert.ToBase64String(hash.ComputeHash(stream.ToArray()));
            }
        }

        private static PurchaseOrderDetailsDTO CreateRevisionDetails(PurchaseOrder order,
            System.Collections.Generic.IEnumerable<PurchaseOrderLine> lines)
        {
            return new PurchaseOrderDetailsDTO
            {
                Order = ToSummary(order.Id, order.OrderNumber, order.SupplierId, null, null,
                    order.Status, order.CreatedUtc, order.OrderedUtc, order.CancelledUtc,
                    order.CancelReason, order.TotalAmount, lines.Count()),
                Items = lines.OrderBy(l => l.ProductId).ThenBy(l => l.Id).Select(l => new PurchaseOrderLineDTO
                {
                    Id = l.Id, ProductId = l.ProductId, OrderedQuantity = l.OrderedQuantity,
                    ReceivedQuantity = l.ReceivedQuantity, UnitCost = l.UnitCost
                }).ToList()
            };
        }

        private static void ValidateDraft(PurchaseOrderDraftDTO request)
        {
            if (request == null || request.SupplierId <= 0 || string.IsNullOrWhiteSpace(request.OrderNumber) ||
                request.OrderNumber.Trim().Length > 30 || request.OrderNumber.Any(char.IsControl))
                throw new ValidationException("A valid supplier and order number of at most 30 characters are required.");
            ValidateLines(request.Items);
        }

        private static decimal CalculateTotal(PurchaseOrderDraftDTO request)
        {
            return CalculateTotal(request.Items);
        }

        private static decimal CalculateTotal(System.Collections.Generic.IEnumerable<PurchaseOrderLineInputDTO> items)
        {
            try
            {
                decimal total = 0;
                foreach (var item in items)
                {
                    total = checked(total + checked(item.UnitCost * item.Quantity));
                    if (total > 9999999999999999.99m)
                        throw new OverflowException();
                }
                return total;
            }
            catch (OverflowException exception)
            {
                throw new ValidationException("The purchase-order total exceeds the supported monetary range.", exception);
            }
        }

        private static void ValidateLines(System.Collections.Generic.ICollection<PurchaseOrderLineInputDTO> items)
        {
            if (items == null || items.Count < 1 || items.Count > 500 ||
                items.Any(i => i == null || i.ProductId <= 0 || i.Quantity <= 0 ||
                    i.UnitCost <= 0 || i.UnitCost > 9999999999999999.99m || decimal.Round(i.UnitCost, 2) != i.UnitCost) ||
                items.GroupBy(i => i.ProductId).Any(g => g.Count() > 1))
                throw new ValidationException("Provide 1–500 distinct products with positive quantities and two-decimal costs.");
        }

        private static void ThrowWriteConflict(Exception exception)
        {
            if (Inventory.InventoryConflict.IsRecognized(exception))
                throw new ValidationException(
                    "Another operation changed this purchase order. Reload and review it before trying again.", exception);
        }

        private void ClearTracking()
        {
            foreach (var entry in _context.ChangeTracker.Entries().ToList()) entry.State = EntityState.Detached;
        }
    }
}
