using POS.Common.Enumerations;
using POS.Core.Abstractions;
using POS.Core.Security;
using POS.Data.Context;
using POS.Domains.AuditEntry;
using POS.Models.Operations;
using POS.Services.Repository;
using System;
using System.ComponentModel.DataAnnotations;
using System.Data.Entity;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace POS.Services.Security
{
    public sealed class AuditService
    {
        private readonly Func<POSContext> _createContext;
        private readonly IAuthorizationService _authorization;

        public AuditService(Func<POSContext> createContext, IAuthorizationService authorization)
        {
            _createContext = createContext ?? throw new ArgumentNullException(nameof(createContext));
            _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        }

        public async Task<AuditRetentionSummaryDTO> GetRetentionSummaryAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            PermissionGuard.Demand(_authorization, ResourceCodes.Audit, ClaimActionType.View);
            using (var context = _createContext())
            using (var repository = new BaseRepository<AuditLog, long>(context, false))
            {
                return await repository.GetAllAsQueryable().GroupBy(x => 1)
                    .Select(events => new AuditRetentionSummaryDTO
                    {
                        TotalEvents = events.LongCount(),
                        OldestEventUtc = events.Min(x => (DateTime?)x.DateLogged),
                        NewestEventUtc = events.Max(x => (DateTime?)x.DateLogged)
                    }).SingleOrDefaultAsync(cancellationToken) ?? new AuditRetentionSummaryDTO();
            }
        }

        public async Task<AuditPageDTO> SearchAsync(AuditSearchDTO filter, CancellationToken cancellationToken = default(CancellationToken))
        {
            PermissionGuard.Demand(_authorization, ResourceCodes.Audit, ClaimActionType.View);
            if (filter == null) throw new ArgumentNullException(nameof(filter));
            if (filter.FromUtc.HasValue && filter.ToUtcExclusive.HasValue && filter.FromUtc >= filter.ToUtcExclusive)
                throw new ValidationException("The end date must be on or after the start date.");
            if ((filter.Search?.Length ?? 0) > 200 || (filter.Entity?.Length ?? 0) > 100 || (filter.Action?.Length ?? 0) > 100)
                throw new ValidationException("Search text is too long.");
            var pageSize = Math.Max(1, Math.Min(200, filter.PageSize));
            var search = filter.Search?.Trim();
            var entity = filter.Entity?.Trim();
            var action = filter.Action?.Trim();
            var category = filter.Category?.Trim();
            var registerCode = filter.RegisterCode?.Trim();
            if ((registerCode?.Length ?? 0) > 30)
                throw new ValidationException("Register code cannot exceed 30 characters.");
            if (filter.UnattributedOnly && !string.IsNullOrEmpty(registerCode))
                throw new ValidationException("Clear the register code when viewing unattributed events.");
            if (!string.IsNullOrEmpty(category) && !AuditCategories.Names.Contains(category))
                throw new ValidationException("Select a valid audit category.");
            using (var context = _createContext())
            using (var repository = new BaseRepository<AuditLog, long>(context, false))
            {
                var query = from audit in repository.GetAllAsQueryable()
                            join user in context.Users.AsNoTracking() on audit.UserId equals user.Id into users
                            from user in users.DefaultIfEmpty()
                            select new AuditEventDTO
                            {
                                Id = audit.Id, DateLoggedUtc = audit.DateLogged,
                                Username = user.UserName, UserId = audit.UserId,
                                Entity = audit.TableName, RecordId = audit.RecordId,
                                Action = audit.Action, OldValue = audit.OldValue, NewValue = audit.NewValue,
                                CorrelationId = audit.CorrelationId,
                                RegisterStationId = audit.RegisterStationId, RegisterCode = audit.RegisterCode
                            };
                if (filter.FromUtc.HasValue) query = query.Where(x => x.DateLoggedUtc >= filter.FromUtc.Value);
                if (filter.ToUtcExclusive.HasValue) query = query.Where(x => x.DateLoggedUtc < filter.ToUtcExclusive.Value);
                if (!string.IsNullOrEmpty(entity)) query = query.Where(x => x.Entity.Contains(entity));
                if (!string.IsNullOrEmpty(action)) query = query.Where(x => x.Action.Contains(action));
                if (!string.IsNullOrEmpty(category))
                {
                    var categoryEntities = AuditCategories.GetEntities(category);
                    query = category == AuditCategories.Other
                        ? query.Where(x => x.Entity == null || !categoryEntities.Contains(x.Entity))
                        : query.Where(x => categoryEntities.Contains(x.Entity));
                }
                if (filter.CorrelationId.HasValue) query = query.Where(x => x.CorrelationId == filter.CorrelationId.Value);
                if (filter.UnattributedOnly) query = query.Where(x => x.RegisterStationId == null);
                if (!string.IsNullOrEmpty(registerCode)) query = query.Where(x => x.RegisterCode == registerCode);
                if (!string.IsNullOrEmpty(search)) query = query.Where(x =>
                    x.Username.Contains(search) || x.UserId.Contains(search) || x.RecordId.Contains(search));
                var total = await query.CountAsync(cancellationToken);
                var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
                var pageNumber = Math.Max(1, Math.Min(totalPages, filter.PageNumber));
                var items = await query.OrderByDescending(x => x.DateLoggedUtc).ThenByDescending(x => x.Id)
                    .Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
                foreach (var item in items) item.Category = AuditCategories.Classify(item.Entity);
                return new AuditPageDTO { Items = items, TotalCount = total, PageNumber = pageNumber, TotalPages = totalPages };
            }
        }
    }
}
