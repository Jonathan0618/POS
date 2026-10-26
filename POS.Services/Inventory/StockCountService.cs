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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace POS.Services.Inventory
{
    // The caller owns the injected context. No forms or controls participate.
    public sealed class StockCountService
    {
        private readonly POSContext _context;
        private readonly IClock _clock;
        private readonly ICurrentUser _currentUser;
        private readonly IAuthorizationService _authorization;

        public StockCountService(POSContext context, IClock clock, ICurrentUser currentUser, IAuthorizationService authorization)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        }

        public int PostCount(StockCountPostingDTO request)
        {
            return SaveCount(request, true);
        }

        public int CreateDraft(StockCountPostingDTO request)
        {
            return SaveCount(request, false);
        }

        public void UpdateDraft(StockCountDraftUpdateDTO request)
        {
            PermissionGuard.Demand(_authorization, ResourceCodes.Inventory, ClaimActionType.Edit);
            if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUser.UserId))
                throw new UnauthorizedAccessException("An authenticated user is required to edit a stock count.");
            if (request == null || request.CountId <= 0 || request.ExpectedRowVersion == null || request.ExpectedRowVersion.Length != 8)
                throw new ValidationException("Load a stock count and its current version before editing.");
            var reviewedVersion = CopyReviewedVersion(request.ExpectedRowVersion);
            if (request.Items == null || request.Items.Count == 0 || request.Items.Count > 500 ||
                request.Items.Any(i => i == null || i.ProductId <= 0 || i.CountedQuantity < 0) ||
                request.Items.GroupBy(i => i.ProductId).Any(g => g.Count() > 1))
                throw new ValidationException("Provide 1–500 distinct existing products with nonnegative counted quantities.");
            if (_context.Database.CurrentTransaction != null || _context.ChangeTracker.HasChanges())
                throw new InvalidOperationException("Edit counts using a context without pending changes or an active transaction.");
            ClearTracking();
            using (var operation = AuditOperation.Begin())
            using (var transaction = _context.Database.BeginTransaction(System.Data.IsolationLevel.Serializable))
            {
                try
                {
                    var count = _context.StockCounts.SingleOrDefault(c => c.Id == request.CountId);
                    if (count == null || count.Status != DocumentStatus.Draft)
                        throw new ValidationException("Only an existing draft stock count can be edited.");
                    DemandReviewedVersion(count, reviewedVersion);
                    var ids = request.Items.Select(i => i.ProductId).ToList();
                    var lines = _context.StockCountLines.Where(line => line.StockCountId == count.Id && ids.Contains(line.ProductId)).ToList();
                    if (lines.Count != ids.Count || lines.GroupBy(line => line.ProductId).Any(g => g.Count() > 1))
                        throw new ValidationException("A selected product is missing or duplicated in this count. Reload the draft.");
                    var changed = false;
                    foreach (var item in request.Items)
                    {
                        var line = lines.Single(l => l.ProductId == item.ProductId);
                        if (line.ExpectedQuantity != item.ExpectedQuantity)
                            throw new ValidationException("Expected stock cannot be rewritten while editing a count. Reload the draft.");
                        if (line.CountedQuantity == item.CountedQuantity) continue;
                        line.CountedQuantity = item.CountedQuantity;
                        changed = true;
                    }
                    if (changed)
                    {
                        // Touch the header so line edits advance the document row version.
                        _context.Entry(count).Property(c => c.Status).IsModified = true;
                        _context.SaveChanges();
                    }
                    transaction.Commit();
                }
                catch (Exception exception)
                {
                    try { transaction.Rollback(); }
                    finally { ClearTracking(); }
                    ThrowCountConflict(exception);
                    throw;
                }
            }
        }

        public int PostDraft(int countId, string reason, byte[] expectedRowVersion)
        {
            PermissionGuard.Demand(_authorization, ResourceCodes.Inventory, ClaimActionType.Edit);
            if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUser.UserId))
                throw new UnauthorizedAccessException("An authenticated user is required to post a stock count.");
            if (countId <= 0) throw new ValidationException("Select an existing stock count.");
            var reviewedVersion = CopyReviewedVersion(expectedRowVersion);
            var items = _context.StockCountLines.AsNoTracking().Where(line => line.StockCountId == countId)
                .OrderBy(line => line.ProductId).Take(501)
                .Select(line => new StockCountQuantityDTO
                {
                    ProductId = line.ProductId, ExpectedQuantity = line.ExpectedQuantity,
                    CountedQuantity = line.CountedQuantity
                }).ToList();
            return SaveCount(new StockCountPostingDTO { Reason = reason, Items = items }, true, countId, reviewedVersion);
        }

        private int SaveCount(StockCountPostingDTO request, bool post, int? draftId = null, byte[] reviewedVersion = null)
        {
            PermissionGuard.Demand(_authorization, ResourceCodes.Inventory, ClaimActionType.Edit);
            var userId = _currentUser.UserId;
            if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(userId))
                throw new UnauthorizedAccessException("An authenticated user is required to post a stock count.");
            if (request?.Items == null || request.Items.Count == 0 || request.Items.Count > 500)
                throw new ValidationException("A stock count must contain between 1 and 500 products.");
            if (!draftId.HasValue && request.RequestId == Guid.Empty)
                throw new ValidationException("A stock-count request ID is required and must be retained for retries.");
            if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 250)
                throw new ValidationException("A stock count reason is required and cannot exceed 250 characters.");
            if (request.Items.Any(i => i == null || i.ProductId <= 0 || i.CountedQuantity < 0))
                throw new ValidationException("Every count line needs a valid product and a nonnegative physical quantity.");
            if (request.Items.GroupBy(i => i.ProductId).Any(g => g.Count() > 1))
                throw new ValidationException("A product may appear only once in a stock count.");
            if (post && request.Items.Any(i =>
                (long)i.CountedQuantity - i.ExpectedQuantity > int.MaxValue ||
                (long)i.CountedQuantity - i.ExpectedQuantity < int.MinValue))
                throw new ValidationException("A count variance exceeds the supported stock-movement quantity. Review the count and inventory before posting.");
            if (_context.Database.CurrentTransaction != null || _context.ChangeTracker.HasChanges())
                throw new InvalidOperationException("Post counts using a context without pending changes or an active transaction.");
            ClearTracking();
            var requestHash = draftId.HasValue ? null : CalculateRequestHash(request, post);

            using (var operation = AuditOperation.Begin())
            using (var transaction = _context.Database.BeginTransaction(System.Data.IsolationLevel.Serializable))
            {
                try
                {
                    var time = _clock.UtcNow;
                    StockCount count;
                    if (draftId.HasValue)
                    {
                        count = _context.StockCounts.SingleOrDefault(c => c.Id == draftId.Value);
                        if (count == null || count.Status != DocumentStatus.Draft)
                            throw new ValidationException("Only an existing draft stock count can be posted.");
                        DemandReviewedVersion(count, reviewedVersion);
                        var savedLines = _context.StockCountLines.AsNoTracking().Where(line => line.StockCountId == count.Id).ToList();
                        if (savedLines.Count != request.Items.Count || savedLines.Any(line =>
                            !request.Items.Any(item => item.ProductId == line.ProductId &&
                                item.ExpectedQuantity == line.ExpectedQuantity && item.CountedQuantity == line.CountedQuantity)))
                            throw new ValidationException("The draft changed while it was being loaded. Reload before posting.");
                        count.Status = DocumentStatus.Posted;
                        count.PostedUtc = time;
                    }
                    else
                    {
                        var prior = _context.StockCounts.AsNoTracking()
                            .SingleOrDefault(c => c.RequestId == request.RequestId);
                        if (prior != null)
                        {
                            if (prior.RequestHash != requestHash || prior.CreatedByUserId != userId)
                                throw new ValidationException("This stock-count request ID was already used with different details. Review the original count and create a new request ID for changed work.");
                            transaction.Commit();
                            return prior.Id;
                        }
                        count = new StockCount
                        {
                            RequestId = request.RequestId, RequestHash = requestHash,
                            Status = post ? DocumentStatus.Posted : DocumentStatus.Draft, CreatedByUserId = userId,
                            CreatedUtc = time, PostedUtc = post ? (DateTime?)time : null
                        };
                        _context.StockCounts.Add(count);
                    }
                    // Generate a document ID for movement references. This save remains
                    // inside the transaction and is rolled back on any later failure.
                    _context.SaveChanges();
                    foreach (var line in request.Items.OrderBy(i => i.ProductId))
                    {
                        var product = _context.Set<Product>().Find(line.ProductId);
                        var balance = _context.InventoryBalances.SingleOrDefault(b => b.ProductId == line.ProductId);
                        if (product == null || balance == null)
                            throw new ValidationException("A counted product or its inventory balance is missing. Reload inventory.");
                        var ledgerQuantity = _context.StockMovements.Where(m => m.ProductId == line.ProductId)
                            .Sum(m => (long?)m.QuantityDelta) ?? 0L;
                        if (balance.QuantityOnHand != product.Quantity || ledgerQuantity != balance.QuantityOnHand)
                            throw new ValidationException($"Inventory records disagree for '{product.Name}'. Reconcile them before posting a physical count.");
                        if (balance.QuantityOnHand != line.ExpectedQuantity)
                            throw new ValidationException($"Stock changed for '{product.Name}'. Reload and review the count before posting.");
                        var delta = (long)line.CountedQuantity - line.ExpectedQuantity;
                        if (!draftId.HasValue) _context.StockCountLines.Add(new StockCountLine
                        {
                            StockCountId = count.Id, ProductId = product.Id,
                            ExpectedQuantity = line.ExpectedQuantity, CountedQuantity = line.CountedQuantity
                        });
                        if (!post || delta == 0) continue;
                        balance.QuantityOnHand = line.CountedQuantity;
                        product.Quantity = line.CountedQuantity;
                        _context.StockMovements.Add(new StockMovement
                        {
                            ProductId = product.Id, MovementType = StockMovementType.StockCount,
                            QuantityDelta = checked((int)delta), ReferenceType = "StockCount",
                            ReferenceId = count.Id.ToString(CultureInfo.InvariantCulture),
                            Reason = request.Reason.Trim(), UserId = userId, CreatedUtc = time
                        });
                    }
                    // Preserve the reviewed reason even for drafts and zero-variance
                    // counts, where no stock movement exists to carry it.
                    _context.AuditLogs.Add(new POS.Domains.AuditEntry.AuditLog
                    {
                        TableName = "StockCount",
                        RecordId = count.Id.ToString(CultureInfo.InvariantCulture),
                        Action = post ? "CountPosted" : "CountDraftCreated",
                        UserId = userId, DateLogged = time,
                        NewValue = POS.Data.Auditing.AuditEventDetails.StockCount(
                            count.Id, request.Reason.Trim(), request.Items.Count, post)
                    });
                    _context.SaveChanges();
                    transaction.Commit();
                    return count.Id;
                }
                catch (Exception exception)
                {
                    try { transaction.Rollback(); }
                    finally { ClearTracking(); }
                    ThrowCountConflict(exception);
                    throw;
                }
            }
        }

        public void CancelDraft(int countId, byte[] expectedRowVersion)
        {
            PermissionGuard.Demand(_authorization, ResourceCodes.Inventory, ClaimActionType.Edit);
            if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUser.UserId))
                throw new UnauthorizedAccessException("An authenticated user is required to cancel a stock count.");
            if (countId <= 0) throw new ValidationException("Select an existing stock count.");
            var reviewedVersion = CopyReviewedVersion(expectedRowVersion);
            if (_context.Database.CurrentTransaction != null || _context.ChangeTracker.HasChanges())
                throw new InvalidOperationException("Cancel counts using a context without pending changes or an active transaction.");
            ClearTracking();
            using (var operation = AuditOperation.Begin())
            using (var transaction = _context.Database.BeginTransaction(System.Data.IsolationLevel.Serializable))
            {
                try
                {
                    var count = _context.StockCounts.SingleOrDefault(c => c.Id == countId);
                    if (count == null) throw new ValidationException("The stock count does not exist.");
                    if (count.Status != DocumentStatus.Draft)
                        throw new ValidationException("Only draft stock counts can be cancelled. Posted counts require a correcting operation.");
                    DemandReviewedVersion(count, reviewedVersion);
                    count.Status = DocumentStatus.Cancelled;
                    _context.SaveChanges();
                    transaction.Commit();
                }
                catch (Exception exception)
                {
                    try { transaction.Rollback(); }
                    finally { ClearTracking(); }
                    ThrowCountConflict(exception);
                    throw;
                }
            }
        }

        public StockCountHistoryDTO GetHistory(int pageNumber = 1, int pageSize = 50)
        {
            return SearchHistory(new StockCountSearchDTO { PageNumber = pageNumber, PageSize = pageSize });
        }

        public StockCountHistoryDTO SearchHistory(StockCountSearchDTO search)
        {
            PermissionGuard.Demand(_authorization, ResourceCodes.Inventory, ClaimActionType.View);
            if (search == null) throw new ValidationException("Stock-count search options are required.");
            DocumentStatus? status = null;
            if (!string.IsNullOrWhiteSpace(search.Status))
            {
                var text = search.Status.Trim();
                if (string.Equals(text, "Draft", StringComparison.OrdinalIgnoreCase)) status = DocumentStatus.Draft;
                else if (string.Equals(text, "Posted", StringComparison.OrdinalIgnoreCase)) status = DocumentStatus.Posted;
                else if (string.Equals(text, "Cancelled", StringComparison.OrdinalIgnoreCase)) status = DocumentStatus.Cancelled;
                else throw new ValidationException("Select Draft, Posted, or Cancelled, or leave status empty.");
            }
            if ((search.FromUtc.HasValue && search.FromUtc.Value.Kind != DateTimeKind.Utc) ||
                (search.ToUtcExclusive.HasValue && search.ToUtcExclusive.Value.Kind != DateTimeKind.Utc))
                throw new ValidationException("Stock-count date filters must use UTC.");
            if (search.FromUtc.HasValue && search.ToUtcExclusive.HasValue && search.FromUtc >= search.ToUtcExclusive)
                throw new ValidationException("The end of the date range must be after its start.");
            if (search.ProductId.HasValue && search.ProductId.Value <= 0)
                throw new ValidationException("Select a valid product or search all products.");
            var actor = search.CreatedByUserId?.Trim();
            if (actor != null && actor.Length > 128)
                throw new ValidationException("Creator ID cannot exceed 128 characters.");

            var pageSize = Math.Max(1, Math.Min(200, search.PageSize));
            IQueryable<StockCount> query = _context.StockCounts.AsNoTracking();
            if (status.HasValue) query = query.Where(c => c.Status == status.Value);
            if (search.FromUtc.HasValue) query = query.Where(c => c.CreatedUtc >= search.FromUtc.Value);
            if (search.ToUtcExclusive.HasValue) query = query.Where(c => c.CreatedUtc < search.ToUtcExclusive.Value);
            if (!string.IsNullOrEmpty(actor)) query = query.Where(c => c.CreatedByUserId == actor);
            if (search.ProductId.HasValue)
                query = query.Where(c => _context.StockCountLines.Any(line =>
                    line.StockCountId == c.Id && line.ProductId == search.ProductId.Value));
            var totalCount = query.Count();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            var pageNumber = Math.Max(1, Math.Min(totalPages, search.PageNumber));
            var records = query.OrderByDescending(c => c.CreatedUtc).ThenByDescending(c => c.Id)
                .Skip((pageNumber - 1) * pageSize).Take(pageSize)
                .Select(c => new
                {
                    c.Id, c.RequestId, c.Status, c.CreatedByUserId, c.CreatedUtc, c.PostedUtc, c.RowVersion,
                    ProductCount = _context.StockCountLines.Count(line => line.StockCountId == c.Id)
                }).ToList();
            return new StockCountHistoryDTO
            {
                TotalCount = totalCount, TotalPages = totalPages, PageNumber = pageNumber, PageSize = pageSize,
                Items = records.Select(c => new StockCountSummaryDTO
                {
                    Id = c.Id, RequestId = c.RequestId, Status = c.Status.ToString(), CreatedByUserId = c.CreatedByUserId,
                    CreatedUtc = DateTime.SpecifyKind(c.CreatedUtc, DateTimeKind.Utc),
                    PostedUtc = c.PostedUtc.HasValue ? (DateTime?)DateTime.SpecifyKind(c.PostedUtc.Value, DateTimeKind.Utc) : null,
                    ProductCount = c.ProductCount, RowVersion = c.RowVersion
                }).ToList()
            };
        }

        public StockCountDetailsDTO GetDetails(int countId, int pageNumber = 1, int pageSize = 100)
        {
            PermissionGuard.Demand(_authorization, ResourceCodes.Inventory, ClaimActionType.View);
            if (countId <= 0) throw new ValidationException("Select an existing stock count.");
            var count = _context.StockCounts.AsNoTracking().SingleOrDefault(c => c.Id == countId);
            if (count == null) throw new ValidationException("The stock count does not exist.");
            var lines = _context.StockCountLines.AsNoTracking().Where(line => line.StockCountId == countId);
            var totalCount = lines.Count();
            pageSize = Math.Max(1, Math.Min(200, pageSize));
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            pageNumber = Math.Max(1, Math.Min(totalPages, pageNumber));
            return new StockCountDetailsDTO
            {
                Count = new StockCountSummaryDTO
                {
                    Id = count.Id, RequestId = count.RequestId, Status = count.Status.ToString(), CreatedByUserId = count.CreatedByUserId,
                    CreatedUtc = DateTime.SpecifyKind(count.CreatedUtc, DateTimeKind.Utc),
                    PostedUtc = count.PostedUtc.HasValue ? (DateTime?)DateTime.SpecifyKind(count.PostedUtc.Value, DateTimeKind.Utc) : null,
                    ProductCount = totalCount, RowVersion = count.RowVersion
                },
                PageNumber = pageNumber, PageSize = pageSize, TotalPages = totalPages,
                Items = lines.OrderBy(line => line.ProductId).ThenBy(line => line.Id)
                    .Skip((pageNumber - 1) * pageSize).Take(pageSize)
                    .Select(line => new StockCountDetailLineDTO
                    {
                        Id = line.Id, ProductId = line.ProductId,
                        CurrentProductName = line.Product == null ? null : line.Product.Name,
                        CurrentSku = line.Product == null ? null : line.Product.Sku,
                        ExpectedQuantity = line.ExpectedQuantity, CountedQuantity = line.CountedQuantity
                    }).ToList()
            };
        }

        private static void ThrowCountConflict(Exception exception)
        {
            if (InventoryConflict.IsUniqueIndexViolation(exception, "UX_StockCounts_RequestId"))
                throw new ValidationException(
                    "This stock-count request ID was committed concurrently. Reload count history and retry the same request ID and unchanged details to retrieve the existing document.",
                    exception);
            for (var cause = exception; cause != null; cause = cause.InnerException)
            {
                if (cause is System.Data.Entity.Infrastructure.DbUpdateConcurrencyException)
                    throw new ValidationException(
                        "The stock count or inventory changed during this operation. Reload the count and inventory, review the values, and try again.", exception);
                var sql = cause as System.Data.SqlClient.SqlException;
                if (sql == null) continue;
                foreach (System.Data.SqlClient.SqlError error in sql.Errors)
                {
                    if (error.Number == 1205 || error.Number == 1222)
                        throw new ValidationException(
                            "Another operation conflicted with this stock count. Reload the count and inventory before trying again.", exception);
                }
            }
            // Connection/command timeouts and other failures can have uncertain
            // outcomes. Do not classify them as safe to replay automatically.
        }

        private static string CalculateRequestHash(StockCountPostingDTO request, bool post)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    writer.Write(post);
                    writer.Write(request.Reason.Trim());
                    foreach (var item in request.Items.OrderBy(i => i.ProductId))
                    {
                        writer.Write(item.ProductId);
                        writer.Write(item.ExpectedQuantity);
                        writer.Write(item.CountedQuantity);
                    }
                }
                using (var hash = SHA256.Create())
                    return Convert.ToBase64String(hash.ComputeHash(stream.ToArray()));
            }
        }

        private static byte[] CopyReviewedVersion(byte[] version)
        {
            if (version == null || version.Length != 8)
                throw new ValidationException("Load and review the stock count's current version before continuing.");
            return (byte[])version.Clone();
        }

        private static void DemandReviewedVersion(StockCount count, byte[] reviewedVersion)
        {
            if (reviewedVersion == null || count.RowVersion == null || !count.RowVersion.SequenceEqual(reviewedVersion))
                throw new ValidationException("The stock count changed after it was reviewed. Reload and review it before continuing.");
        }

        private void ClearTracking()
        {
            foreach (var entry in _context.ChangeTracker.Entries().ToList())
                entry.State = EntityState.Detached;
        }
    }
}
