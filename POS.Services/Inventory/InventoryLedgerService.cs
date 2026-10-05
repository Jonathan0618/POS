using POS.Common.Enumerations;
using POS.Core;
using POS.Core.Abstractions;
using POS.Data.Context;
using POS.Domains.Operations;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using POS.Core.Security;
using POS.Services.Security;
using POS.Models.Operations;

namespace POS.Services.Inventory
{
    public class InventoryLedgerService : IDisposable
    {
        private readonly POSContext _context;
        private readonly bool _ownsContext;
        private readonly IClock _clock;
        private readonly ICurrentUser _currentUser;
        private readonly IAuthorizationService _authorization;

        public InventoryLedgerService() : this(new POSContext(), new SystemClock(), new CurrentUserAccessor(), null, true) { }
        public InventoryLedgerService(POSContext context) : this(context, new SystemClock(), new CurrentUserAccessor(), null, false) { }
        public InventoryLedgerService(POSContext context, IClock clock, ICurrentUser currentUser) : this(context, clock, currentUser, null, false) { }
        public InventoryLedgerService(POSContext context, IClock clock, ICurrentUser currentUser, IAuthorizationService authorization)
            : this(context, clock, currentUser, authorization, false) { }

        private InventoryLedgerService(POSContext context, IClock clock, ICurrentUser currentUser, IAuthorizationService authorization, bool ownsContext)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _authorization = authorization ?? new ClaimsAuthorizationService(_currentUser);
            _ownsContext = ownsContext;
        }

        public InventoryAlertPageDTO SearchAlerts(InventoryAlertSearchDTO search)
        {
            PermissionGuard.Demand(_authorization, ResourceCodes.Inventory, ClaimActionType.View);
            if (search == null) throw new ArgumentNullException(nameof(search));
            if (!Enum.IsDefined(typeof(InventoryAlertKind), search.Kind))
                throw new ArgumentOutOfRangeException(nameof(search.Kind));
            if (search.CategoryId.HasValue && search.CategoryId.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(search.CategoryId));
            if (search.Kind == InventoryAlertKind.ExpiringSoon &&
                (search.ExpiringWithinDays < 1 || search.ExpiringWithinDays > 365))
                throw new ArgumentOutOfRangeException(nameof(search.ExpiringWithinDays),
                    "The expiry window must be between 1 and 365 days.");
            var term = search.Search?.Trim();
            if (term != null && term.Length > 100)
                throw new ArgumentException("Inventory search cannot exceed 100 characters.", nameof(search));

            // Starting from balances keeps missing inventory distinct from zero.
            // An inactive category does not disable its active products.
            var query = _context.InventoryBalances.AsNoTracking().Where(b => b.Product.IsActive);
            var utcToday = _clock.UtcNow.Date;
            if (search.Kind == InventoryAlertKind.OutOfStock)
                query = query.Where(b => b.QuantityOnHand <= 0);
            else if (search.Kind == InventoryAlertKind.ExpiringSoon)
            {
                var endExclusive = utcToday.AddDays(search.ExpiringWithinDays);
                query = query.Where(b => b.QuantityOnHand > 0 && b.Product.ExpiryDate.HasValue &&
                    b.Product.ExpiryDate.Value >= utcToday && b.Product.ExpiryDate.Value < endExclusive);
            }
            else if (search.Kind == InventoryAlertKind.Expired)
                query = query.Where(b => b.QuantityOnHand > 0 && b.Product.ExpiryDate.HasValue &&
                    b.Product.ExpiryDate.Value < utcToday);
            else
                query = query.Where(b => b.QuantityOnHand <= b.Product.BuyingThreshold);
            if (search.CategoryId.HasValue)
                query = query.Where(b => b.Product.CategoryId == search.CategoryId.Value);
            if (!string.IsNullOrEmpty(term))
                query = query.Where(b => b.Product.Name.Contains(term) ||
                    (b.Product.Sku != null && b.Product.Sku.Contains(term)) ||
                    (b.Product.Barcode != null && b.Product.Barcode.Contains(term)));

            var pageSize = Math.Max(1, Math.Min(200, search.PageSize));
            var totalCount = query.Count();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            var pageNumber = Math.Max(1, Math.Min(totalPages, search.PageNumber));
            var ordered = search.Kind == InventoryAlertKind.ExpiringSoon || search.Kind == InventoryAlertKind.Expired
                ? query.OrderBy(b => b.Product.ExpiryDate).ThenBy(b => b.ProductId)
                : query.OrderBy(b => b.QuantityOnHand).ThenBy(b => b.ProductId);
            var items = ordered
                .Skip((pageNumber - 1) * pageSize).Take(pageSize)
                .Select(b => new InventoryAlertItemDTO
                {
                    ProductId = b.ProductId, ProductName = b.Product.Name,
                    Sku = b.Product.Sku, Barcode = b.Product.Barcode, Unit = b.Product.Unit,
                    CategoryId = b.Product.CategoryId, CategoryName = b.Product.Category.Name,
                    QuantityOnHand = b.QuantityOnHand, ReorderLevel = b.Product.BuyingThreshold,
                    ExpiryDate = b.Product.ExpiryDate
                }).ToList();
            return new InventoryAlertPageDTO
            {
                Items = items, TotalCount = totalCount, TotalPages = totalPages,
                PageNumber = pageNumber, PageSize = pageSize
            };
        }

        public InventoryBalancePageDTO SearchBalances(InventoryBalanceSearchDTO search)
        {
            PermissionGuard.Demand(_authorization, ResourceCodes.Inventory, ClaimActionType.View);
            if (search == null) throw new ArgumentNullException(nameof(search));
            if (search.CategoryId.HasValue && search.CategoryId.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(search.CategoryId));
            var term = search.Search?.Trim();
            if (term != null && term.Length > 100)
                throw new ArgumentException("Inventory search cannot exceed 100 characters.", nameof(search));
            IQueryable<InventoryBalance> query = _context.InventoryBalances.AsNoTracking();
            if (search.IsActive.HasValue)
                query = query.Where(b => b.Product.IsActive == search.IsActive.Value);
            if (search.CategoryId.HasValue)
                query = query.Where(b => b.Product.CategoryId == search.CategoryId.Value);
            if (!string.IsNullOrEmpty(term))
                query = query.Where(b => b.Product.Name.Contains(term) ||
                    (b.Product.Sku != null && b.Product.Sku.Contains(term)) ||
                    (b.Product.Barcode != null && b.Product.Barcode.Contains(term)));
            var pageSize = Math.Max(1, Math.Min(200, search.PageSize));
            var totalCount = query.Count();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            var pageNumber = Math.Max(1, Math.Min(totalPages, search.PageNumber));
            return new InventoryBalancePageDTO
            {
                TotalCount = totalCount, TotalPages = totalPages, PageNumber = pageNumber, PageSize = pageSize,
                Items = query.OrderBy(b => b.Product.Name).ThenBy(b => b.ProductId)
                    .Skip((pageNumber - 1) * pageSize).Take(pageSize)
                    .Select(b => new InventoryBalanceItemDTO
                    {
                        BalanceId = b.Id, ProductId = b.ProductId, ProductName = b.Product.Name,
                        Sku = b.Product.Sku, Barcode = b.Product.Barcode, Unit = b.Product.Unit,
                        CategoryId = b.Product.CategoryId, CategoryName = b.Product.Category.Name,
                        IsActive = b.Product.IsActive, QuantityOnHand = b.QuantityOnHand,
                        ReorderLevel = b.Product.BuyingThreshold
                    }).ToList()
            };
        }

        public int GetQuantityOnHand(int productId)
        {
            PermissionGuard.Demand(_authorization, ResourceCodes.Inventory, ClaimActionType.View);
            if (productId <= 0) throw new ArgumentOutOfRangeException(nameof(productId));
            var quantity = _context.InventoryBalances.AsNoTracking()
                .Where(x => x.ProductId == productId)
                .Select(x => (int?)x.QuantityOnHand)
                .SingleOrDefault();
            if (!quantity.HasValue) throw new InvalidOperationException("Inventory balance is missing. Reconcile opening inventory before continuing.");
            return quantity.Value;
        }

        public IReadOnlyList<StockMovement> GetMovements(int productId, int take = 200)
        {
            PermissionGuard.Demand(_authorization, ResourceCodes.Inventory, ClaimActionType.View);
            if (productId <= 0) throw new ArgumentOutOfRangeException(nameof(productId));
            if (take < 1 || take > 1000) throw new ArgumentOutOfRangeException(nameof(take));
            return _context.StockMovements.AsNoTracking()
                .Where(x => x.ProductId == productId)
                .OrderByDescending(x => x.CreatedUtc)
                .ThenByDescending(x => x.Id)
                .Take(take)
                .ToList();
        }

        public StockMovementPageDTO SearchMovements(StockMovementSearchDTO search)
        {
            PermissionGuard.Demand(_authorization, ResourceCodes.Inventory, ClaimActionType.View);
            if (search == null) throw new ArgumentNullException(nameof(search));
            if (search.ProductId.HasValue && search.ProductId.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(search.ProductId));
            if (search.MovementType.HasValue && !Enum.IsDefined(typeof(StockMovementType), search.MovementType.Value))
                throw new ArgumentOutOfRangeException(nameof(search.MovementType));
            if ((search.FromUtc.HasValue && search.FromUtc.Value.Kind != DateTimeKind.Utc) ||
                (search.ToUtcExclusive.HasValue && search.ToUtcExclusive.Value.Kind != DateTimeKind.Utc))
                throw new ArgumentException("Movement date filters must use UTC.");
            if (search.FromUtc.HasValue && search.ToUtcExclusive.HasValue && search.FromUtc >= search.ToUtcExclusive)
                throw new ArgumentException("The end of the date range must be after its start.");
            var referenceType = TrimOrNull(search.ReferenceType);
            var referenceId = TrimOrNull(search.ReferenceId);
            if (referenceType?.Length > 40 || referenceId?.Length > 50)
                throw new ArgumentException("Movement reference type/ID cannot exceed 40/50 characters.");

            IQueryable<StockMovement> query = _context.StockMovements.AsNoTracking();
            if (search.ProductId.HasValue) query = query.Where(m => m.ProductId == search.ProductId.Value);
            if (search.MovementType.HasValue)
            {
                var type = (StockMovementType)search.MovementType.Value;
                query = query.Where(m => m.MovementType == type);
            }
            if (search.FromUtc.HasValue) query = query.Where(m => m.CreatedUtc >= search.FromUtc.Value);
            if (search.ToUtcExclusive.HasValue) query = query.Where(m => m.CreatedUtc < search.ToUtcExclusive.Value);
            if (referenceType != null) query = query.Where(m => m.ReferenceType == referenceType);
            if (referenceId != null) query = query.Where(m => m.ReferenceId == referenceId);
            var pageSize = Math.Max(1, Math.Min(200, search.PageSize));
            var count = query.Count();
            var pages = Math.Max(1, (int)Math.Ceiling(count / (double)pageSize));
            var page = Math.Max(1, Math.Min(pages, search.PageNumber));
            var records = query.OrderByDescending(m => m.CreatedUtc).ThenByDescending(m => m.Id)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(m => new
                {
                    m.Id, m.ProductId, ProductName = m.Product.Name, Sku = m.Product.Sku,
                    m.MovementType, m.QuantityDelta, m.Reason, m.ReferenceType, m.ReferenceId, m.UserId, m.CreatedUtc
                }).ToList();
            return new StockMovementPageDTO
            {
                TotalCount = count, TotalPages = pages, PageNumber = page, PageSize = pageSize,
                Items = records.Select(m => new StockMovementDTO
                {
                    Id = m.Id, ProductId = m.ProductId, CurrentProductName = m.ProductName, CurrentSku = m.Sku,
                    MovementType = m.MovementType.ToString(), QuantityDelta = m.QuantityDelta, Reason = m.Reason,
                    ReferenceType = m.ReferenceType, ReferenceId = m.ReferenceId, UserId = m.UserId,
                    CreatedUtc = DateTime.SpecifyKind(m.CreatedUtc, DateTimeKind.Utc)
                }).ToList()
            };
        }

        public InventoryReconciliationPageDTO GetReconciliation(InventoryReconciliationSearchDTO search)
        {
            PermissionGuard.Demand(_authorization, ResourceCodes.Inventory, ClaimActionType.View);
            if (search == null) throw new ArgumentNullException(nameof(search));
            if (search.ProductId.HasValue && search.ProductId.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(search.ProductId));

            IQueryable<Domains.BusinessObjects.Product> products = _context.Set<Domains.BusinessObjects.Product>().AsNoTracking();
            if (search.ProductId.HasValue)
                products = products.Where(p => p.Id == search.ProductId.Value);

            // Aggregate in SQL using bigint, so accumulated movement quantities do
            // not overflow SQL int. No balance is invented for missing records.
            var rows = products.Select(p => new InventoryReconciliationItemDTO
            {
                ProductId = p.Id, ProductName = p.Name, Sku = p.Sku,
                IsActive = p.IsActive, ProductQuantity = p.Quantity,
                BalanceQuantity = _context.InventoryBalances.Where(b => b.ProductId == p.Id)
                    .Select(b => (int?)b.QuantityOnHand).FirstOrDefault(),
                LedgerQuantity = _context.StockMovements.Where(m => m.ProductId == p.Id)
                    .Sum(m => (long?)m.QuantityDelta) ?? 0L
            });
            if (search.MismatchesOnly)
                rows = rows.Where(r => !r.BalanceQuantity.HasValue ||
                    r.BalanceQuantity.Value != r.LedgerQuantity ||
                    r.ProductQuantity != r.BalanceQuantity.Value);

            var pageSize = Math.Max(1, Math.Min(200, search.PageSize));
            var count = rows.Count();
            var totalPages = Math.Max(1, (int)Math.Ceiling(count / (double)pageSize));
            var page = Math.Max(1, Math.Min(totalPages, search.PageNumber));
            return new InventoryReconciliationPageDTO
            {
                TotalCount = count, TotalPages = totalPages, PageNumber = page, PageSize = pageSize,
                Items = rows.OrderBy(r => r.ProductId).Skip((page - 1) * pageSize).Take(pageSize).ToList()
            };
        }

        public StockMovement PostMovement(int productId, StockMovementType type, int quantityDelta, string reason, string referenceType = null, string referenceId = null)
        {
            PermissionGuard.Demand(_authorization, ResourceCodes.Inventory, ClaimActionType.Edit);
            if (type == StockMovementType.Adjustment)
                throw new InvalidOperationException("Use PostAdjustment with the reviewed quantity to post a manual adjustment.");
            if (type == StockMovementType.StockCount)
                throw new InvalidOperationException("Use the stock-count service to post physical count variances.");
            if (type == StockMovementType.Sale || type == StockMovementType.SaleReturn ||
                type == StockMovementType.PurchaseReceipt || type == StockMovementType.PurchaseReturn)
                throw new InvalidOperationException("Post this movement through its sale, receiving, or return document workflow.");
            if (type != StockMovementType.OpeningBalance)
                throw new ArgumentOutOfRangeException(nameof(type));
            if (quantityDelta <= 0)
                throw new ArgumentOutOfRangeException(nameof(quantityDelta), "Opening inventory must be positive; zero opening inventory needs no movement.");
            if (string.IsNullOrWhiteSpace(referenceType) || string.IsNullOrWhiteSpace(referenceId))
                throw new ArgumentException("Opening inventory requires a source reference type and ID.");
            return PostMovementCore(productId, type, quantityDelta, reason, referenceType, referenceId, null);
        }

        public StockMovement PostAdjustment(StockAdjustmentDTO adjustment)
        {
            PermissionGuard.Demand(_authorization, ResourceCodes.Inventory, ClaimActionType.Edit);
            if (adjustment == null) throw new ArgumentNullException(nameof(adjustment));
            if (adjustment.RequestId == Guid.Empty)
                throw new ArgumentException("An adjustment request ID is required and must be reused for retries.", nameof(adjustment));
            return PostMovementCore(adjustment.ProductId, StockMovementType.Adjustment,
                adjustment.QuantityDelta, adjustment.Reason,
                "ManualAdjustment:" + adjustment.ExpectedQuantityOnHand.ToString(System.Globalization.CultureInfo.InvariantCulture),
                adjustment.RequestId.ToString("D"),
                adjustment.ExpectedQuantityOnHand);
        }

        private StockMovement PostMovementCore(int productId, StockMovementType type, int quantityDelta,
            string reason, string referenceType, string referenceId, int? expectedQuantity)
        {
            using (var auditOperation = POS.Core.Abstractions.AuditOperation.Begin())
            {
                PermissionGuard.Demand(_authorization, ResourceCodes.Inventory, ClaimActionType.Edit);
                var userId = _currentUser.UserId;
                if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(userId))
                    throw new UnauthorizedAccessException("An authenticated user is required to post inventory movements.");
                InventoryMovementValidator.ValidateAndThrow(productId, quantityDelta, reason);
                if (!Enum.IsDefined(typeof(StockMovementType), type))
                    throw new ArgumentOutOfRangeException(nameof(type));
                if (referenceType?.Trim().Length > 40 || referenceId?.Trim().Length > 50)
                    throw new ArgumentException("Movement reference type/ID cannot exceed 40/50 characters.");
                if (_context.Database.CurrentTransaction != null || _context.ChangeTracker.HasChanges())
                    throw new InvalidOperationException("Post movements using a context without pending changes or an active transaction.");
                foreach (var entry in _context.ChangeTracker.Entries().ToList())
                    entry.State = EntityState.Detached;

                using (var transaction = _context.Database.BeginTransaction(System.Data.IsolationLevel.Serializable))
                {
                    try
                    {
                    if (type == StockMovementType.Adjustment)
                    {
                        // Serializable isolation retains the absence/read locks through
                        // posting. Concurrent duplicate submissions cannot both commit.
                        var previous = _context.StockMovements.AsNoTracking().SingleOrDefault(m =>
                            m.MovementType == StockMovementType.Adjustment && m.ReferenceId == referenceId);
                        if (previous != null)
                        {
                            if (previous.ProductId != productId || previous.QuantityDelta != quantityDelta ||
                                previous.Reason != reason.Trim() || previous.UserId != userId ||
                                previous.ReferenceType != referenceType)
                                throw new InvalidOperationException("This adjustment request ID was already used with different details. Review the original adjustment.");
                            transaction.Commit();
                            return previous;
                        }
                    }
                    var product = _context.Set<Domains.BusinessObjects.Product>().Find(productId);
                    if (product == null) throw new InvalidOperationException("The product no longer exists.");

                    var balance = _context.InventoryBalances.SingleOrDefault(x => x.ProductId == productId);
                    if (balance == null)
                        throw new InvalidOperationException("Inventory balance is missing. Reconcile opening inventory before posting movements.");
                    if (balance.QuantityOnHand != product.Quantity)
                        throw new InvalidOperationException("Product quantity and inventory balance disagree. Reconcile inventory before posting movements.");
                    if (type == StockMovementType.OpeningBalance &&
                        (balance.QuantityOnHand != 0 || _context.StockMovements.Any(m => m.ProductId == productId)))
                        throw new InvalidOperationException("Opening inventory can only be posted to a zero balance with no movement history. Use the appropriate workflow for later changes.");
                    if (expectedQuantity.HasValue && balance.QuantityOnHand != expectedQuantity.Value)
                        throw new InvalidOperationException("Stock changed after it was reviewed. Reload the quantity and review the adjustment again.");
                    var ledgerQuantity = _context.StockMovements.Where(m => m.ProductId == productId)
                        .Sum(m => (long?)m.QuantityDelta) ?? 0L;
                    if (ledgerQuantity != balance.QuantityOnHand)
                        throw new InvalidOperationException("The ledger and balance disagree. Reconcile inventory before posting movements.");

                    var proposedQuantity = (long)balance.QuantityOnHand + quantityDelta;
                    if (proposedQuantity > int.MaxValue || proposedQuantity < int.MinValue)
                        throw new System.ComponentModel.DataAnnotations.ValidationException(
                            "This movement would exceed the supported inventory quantity. Review the quantity before posting.");
                    var resultingQuantity = (int)proposedQuantity;
                    var policies = _context.StoreSettings.AsNoTracking().Select(x => x.AllowNegativeStock).Take(2).ToList();
                    if (policies.Count != 1)
                        throw new InvalidOperationException("Exactly one store configuration is required before posting inventory movements.");
                    var allowNegative = policies[0];
                    if (!allowNegative && resultingQuantity < 0)
                        throw new InvalidOperationException("This movement would make inventory negative.");

                    balance.QuantityOnHand = resultingQuantity;
                    product.Quantity = resultingQuantity;

                    var movement = new StockMovement
                    {
                        ProductId = productId,
                        MovementType = type,
                        QuantityDelta = quantityDelta,
                        Reason = reason.Trim(),
                        ReferenceType = TrimOrNull(referenceType),
                        ReferenceId = TrimOrNull(referenceId),
                        UserId = userId,
                        CreatedUtc = _clock.UtcNow
                    };
                    _context.StockMovements.Add(movement);
                    _context.SaveChanges();
                    transaction.Commit();
                    return movement;
                    }
                    catch (Exception exception)
                    {
                        try { transaction.Rollback(); }
                        finally
                        {
                            foreach (var entry in _context.ChangeTracker.Entries().ToList())
                                entry.State = EntityState.Detached;
                        }
                        if (type == StockMovementType.Adjustment &&
                            InventoryConflict.IsUniqueIndexViolation(exception, "UX_StockMovements_AdjustmentRequestId"))
                            throw new System.ComponentModel.DataAnnotations.ValidationException(
                                "This adjustment request ID was committed by another operation. Reload movement history and retry with the same request ID and details to retrieve the existing adjustment.",
                                exception);
                        if (InventoryConflict.IsRecognized(exception))
                            throw new System.ComponentModel.DataAnnotations.ValidationException(
                                type == StockMovementType.Adjustment
                                    ? "Inventory changed or another operation conflicted with this adjustment. Reload inventory and movement history. Retry the original adjustment with the same request ID and details; review any changed adjustment as a new request."
                                    : "Inventory changed or another operation conflicted with opening stock. Reload inventory and movement history before trying again; opening stock requires a zero balance with no prior movements.",
                                exception);
                        throw;
                    }
                }
            }
        }

        private static string TrimOrNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        public void Dispose() { if (_ownsContext) _context.Dispose(); }
    }

    public static class InventoryMovementValidator
    {
        public static void ValidateAndThrow(int productId, int quantityDelta, string reason)
        {
            if (productId <= 0) throw new ArgumentOutOfRangeException(nameof(productId));
            if (quantityDelta == 0) throw new ArgumentOutOfRangeException(nameof(quantityDelta), "Movement quantity cannot be zero.");
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A movement reason is required.", nameof(reason));
            if (reason.Trim().Length > 250) throw new ArgumentException("Movement reason cannot exceed 250 characters.", nameof(reason));
        }
    }
}
