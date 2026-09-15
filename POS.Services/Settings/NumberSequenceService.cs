using POS.Common.Enumerations;
using POS.Core.Abstractions;
using POS.Core.Results;
using POS.Core.Security;
using POS.Data.Context;
using POS.Domains.Operations;
using POS.Services.Repository;
using POS.Services.Security;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace POS.Services.Settings
{
    public sealed class NumberSequenceService : IDisposable
    {
        public const string SaleReceipt = "SALE";
        public const string SaleReturn = "RETURN";
        private readonly POSContext _context;
        private readonly BaseRepository<NumberSequence, int> _repository;
        private readonly IAuthorizationService _authorization;
        private readonly bool _ownsContext;

        public NumberSequenceService()
            : this(new POSContext(), new ClaimsAuthorizationService(new CurrentUserAccessor()), true) { }

        public NumberSequenceService(POSContext context, IAuthorizationService authorization)
            : this(context, authorization, false) { }

        private NumberSequenceService(POSContext context, IAuthorizationService authorization, bool ownsContext)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
            _ownsContext = ownsContext;
            _repository = new BaseRepository<NumberSequence, int>(_context, false);
        }

        public NumberSequence GetReceiptSettings()
        {
            PermissionGuard.Demand(_authorization, ResourceCodes.Settings, ClaimActionType.View);
            return _repository.GetAllAsQueryable().SingleOrDefault(x => x.DocumentType == SaleReceipt);
        }

        public OperationResult ConfigureReceipt(string prefix, long nextNumber)
        {
            using (var auditOperation = POS.Core.Abstractions.AuditOperation.Begin())
            {
                PermissionGuard.Demand(_authorization, ResourceCodes.Settings, ClaimActionType.Edit);
                if (string.IsNullOrWhiteSpace(prefix) || prefix.Trim().Length > 20)
                    return OperationResult.Failure("Receipt prefix is required and cannot exceed 20 characters.");
                if (nextNumber < 1)
                    return OperationResult.Failure("Next receipt number must be at least 1.");
                if ((prefix.Trim() + nextNumber.ToString("D8")).Length > 30)
                    return OperationResult.Failure("The receipt prefix and number cannot exceed 30 characters.");

                using (var transaction = _context.Database.CurrentTransaction == null
                    ? _context.Database.BeginTransaction(IsolationLevel.Serializable)
                    : null)
                {
                    // Lock the current value through the save, including when settings were
                    // loaded before another register allocated a receipt.
                    var currentNumber = _context.Database.SqlQuery<long?>(
                        "SELECT NextNumber FROM dbo.NumberSequences WITH (UPDLOCK, HOLDLOCK) " +
                        "WHERE DocumentType = @documentType",
                        new SqlParameter("@documentType", SaleReceipt)).SingleOrDefault();
                    if (currentNumber.HasValue && nextNumber < currentNumber.Value)
                        return OperationResult.Failure(
                            "The next receipt number cannot move backward. Reload settings before saving.");

                    var sequence = _repository.GetAllAsQueryable().SingleOrDefault(x => x.DocumentType == SaleReceipt);
                    if (sequence == null)
                    {
                        sequence = new NumberSequence
                        {
                            DocumentType = SaleReceipt,
                            Prefix = prefix.Trim().ToUpperInvariant(),
                            NextNumber = nextNumber
                        };
                        _repository.Add(sequence);
                    }
                    else
                    {
                        sequence = _repository.GetById(sequence.Id);
                        _context.Entry(sequence).Reload();
                        sequence.Prefix = prefix.Trim().ToUpperInvariant();
                        sequence.NextNumber = nextNumber;
                        _repository.Update(sequence);
                    }
                    _context.SaveChanges();
                    transaction?.Commit();
                    return OperationResult.Success();
                }
            }
        }

        internal string AllocateReceiptNumber()
        {
            return Allocate(SaleReceipt, "R");
        }

        internal string AllocateReturnNumber()
        {
            return Allocate(SaleReturn, "RT");
        }

        private string Allocate(string documentType, string defaultPrefix)
        {
            var ownsTransaction = _context.Database.CurrentTransaction == null;
            var transaction = ownsTransaction
                ? _context.Database.BeginTransaction(IsolationLevel.Serializable)
                : null;
            try
            {
                var allocation = _context.Database.SqlQuery<SequenceAllocation>(
                    "UPDATE dbo.NumberSequences WITH (UPDLOCK, HOLDLOCK) " +
                    "SET NextNumber = NextNumber + 1 " +
                    "OUTPUT deleted.Prefix AS Prefix, deleted.NextNumber AS Number " +
                    "WHERE DocumentType = @documentType",
                    new SqlParameter("@documentType", documentType)).SingleOrDefault();

                if (allocation == null)
                {
                    _context.Database.ExecuteSqlCommand(
                        "INSERT INTO dbo.NumberSequences (DocumentType, Prefix, NextNumber) " +
                        "VALUES (@documentType, @prefix, @nextNumber)",
                        new SqlParameter("@documentType", documentType),
                        new SqlParameter("@prefix", defaultPrefix),
                        new SqlParameter("@nextNumber", 2L));
                    allocation = new SequenceAllocation { Prefix = defaultPrefix, Number = 1 };
                }

                var receiptNumber = allocation.Prefix + allocation.Number.ToString("D8");
                if (receiptNumber.Length > 30)
                    throw new InvalidOperationException("The configured receipt sequence exceeds 30 characters.");
                transaction?.Commit();
                return receiptNumber;
            }
            catch
            {
                transaction?.Rollback();
                throw;
            }
            finally
            {
                transaction?.Dispose();
            }
        }

        public void Dispose()
        {
            _repository.Dispose();
            if (_ownsContext) _context.Dispose();
        }

        private sealed class SequenceAllocation
        {
            public string Prefix { get; set; }
            public long Number { get; set; }
        }
    }
}
