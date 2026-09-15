using POS.Common.Enumerations;
using POS.Core.Abstractions;
using POS.Core.Security;
using POS.Data.Context;
using POS.Domains.Operations;
using POS.Models.Operations;
using POS.Services.Security;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace POS.Services.Customers
{
    public sealed class CustomerService
    {
        private readonly POSContext _context;
        private readonly IAuthorizationService _authorization;

        public CustomerService(POSContext context, IAuthorizationService authorization)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        }

        public CustomerPageDTO Search(string search = null, bool? active = null,
            int pageNumber = 1, int pageSize = 50)
        {
            Demand(ClaimActionType.View);
            var term = Clean(search);
            if (term?.Length > 150) throw new ValidationException("Customer search cannot exceed 150 characters.");
            IQueryable<Customer> query = _context.Customers.AsNoTracking();
            if (term != null) query = query.Where(c => c.Code.Contains(term) || c.Name.Contains(term) ||
                (c.Phone != null && c.Phone.Contains(term)) || (c.Email != null && c.Email.Contains(term)));
            if (active.HasValue) query = query.Where(c => c.IsActive == active.Value);
            var total = query.Count();
            pageSize = Math.Max(1, Math.Min(200, pageSize));
            var pages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            pageNumber = Math.Max(1, Math.Min(pages, pageNumber));
            var items = Project(query.OrderBy(c => c.Name).ThenBy(c => c.Id)
                .Skip((pageNumber - 1) * pageSize).Take(pageSize)).ToList();
            foreach (var item in items) item.Revision = Revision(item);
            return new CustomerPageDTO
            {
                TotalCount = total, TotalPages = pages, PageNumber = pageNumber,
                PageSize = pageSize, Items = items
            };
        }

        public CustomerDTO GetDetails(int customerId)
        {
            Demand(ClaimActionType.View);
            if (customerId <= 0) throw new ValidationException("Select an existing customer.");
            var result = Project(_context.Customers.AsNoTracking().Where(c => c.Id == customerId)).SingleOrDefault();
            if (result == null) throw new ValidationException("The customer does not exist.");
            result.Revision = Revision(result);
            return result;
        }

        public int Save(CustomerDTO dto)
        {
            Demand(dto != null && dto.Id > 0 ? ClaimActionType.Edit : ClaimActionType.Add);
            Validate(dto);
            var code = dto.Code.Trim().ToUpperInvariant();
            var id = 0;
            Write(() =>
            {
                var customer = dto.Id == 0 ? new Customer { IsActive = true } : _context.Customers.Find(dto.Id);
                if (customer == null) throw new ValidationException("The customer no longer exists. Reload customers.");
                if (dto.Id != 0) DemandRevision(customer, dto.Revision);
                if (_context.Customers.Any(c => c.Id != dto.Id && c.Code.Trim().ToUpper() == code))
                    throw new ValidationException("This customer code is already in use, including inactive customers.");
                customer.Code = code; customer.Name = dto.Name.Trim();
                customer.Phone = Clean(dto.Phone); customer.Email = Clean(dto.Email);
                customer.Address = Clean(dto.Address); customer.TaxIdentifier = Clean(dto.TaxIdentifier);
                if (dto.Id == 0) _context.Customers.Add(customer);
                _context.SaveChanges(); id = customer.Id;
            });
            return id;
        }

        public void SetActive(int customerId, bool active, string revision)
        {
            Demand(ClaimActionType.Edit);
            if (customerId <= 0) throw new ValidationException("Select an existing customer.");
            Write(() =>
            {
                var customer = _context.Customers.Find(customerId);
                if (customer == null) throw new ValidationException("The customer does not exist.");
                DemandRevision(customer, revision);
                if (customer.IsActive == active) return;
                customer.IsActive = active; _context.SaveChanges();
            });
        }

        public CustomerHistoryPageDTO GetHistory(CustomerHistorySearchDTO search)
        {
            Demand(ClaimActionType.View);
            PermissionGuard.Demand(_authorization, ResourceCodes.Sales, ClaimActionType.View);
            if (search == null || search.CustomerId <= 0)
                throw new ValidationException("Select an existing customer and provide history options.");
            if (search.Kind.HasValue && !Enum.IsDefined(typeof(CustomerHistoryKind), search.Kind.Value))
                throw new ValidationException("Select a valid customer-history document kind.");
            if (search.ProductId.HasValue && search.ProductId.Value <= 0)
                throw new ValidationException("Select a valid product or search all products.");
            if ((search.FromUtc.HasValue && search.FromUtc.Value.Kind != DateTimeKind.Utc) ||
                (search.ToUtcExclusive.HasValue && search.ToUtcExclusive.Value.Kind != DateTimeKind.Utc) ||
                (search.FromUtc.HasValue && search.ToUtcExclusive.HasValue && search.FromUtc >= search.ToUtcExclusive))
                throw new ValidationException("Customer-history dates must be a valid UTC range.");
            var receiptNumber = Clean(search.ReceiptNumber);
            if (receiptNumber?.Length > 30) throw new ValidationException("Receipt search cannot exceed 30 characters.");
            if (!_context.Customers.AsNoTracking().Any(c => c.Id == search.CustomerId))
                throw new ValidationException("The customer does not exist.");

            var sales = _context.Sales.AsNoTracking().Where(s => s.CustomerId == search.CustomerId);
            var returns = _context.SaleReturns.AsNoTracking().Where(r => r.Sale.CustomerId == search.CustomerId);
            if (search.ProductId.HasValue)
            {
                var productId = search.ProductId.Value;
                sales = sales.Where(s => s.SaleItems.Any(i => i.ProductId == productId));
                returns = returns.Where(r => r.Lines.Any(i => i.SaleItem.ProductId == productId));
            }
            if (receiptNumber != null)
            {
                sales = sales.Where(s => s.ReceiptNumber.Contains(receiptNumber));
                returns = returns.Where(r => r.Sale.ReceiptNumber.Contains(receiptNumber) ||
                    r.ReturnNumber.Contains(receiptNumber));
            }
            var saleRows = sales.Select(s => new CustomerHistoryRow
            {
                Kind = 0, DocumentId = s.Id, SaleId = s.Id, ReceiptNumber = s.ReceiptNumber,
                Status = (int)s.Status, EventUtc = s.SaleDate, Amount = s.TotalAmount,
                LineCount = s.SaleItems.Count()
            });
            var returnRows = returns.Select(r => new CustomerHistoryRow
            {
                Kind = 1, DocumentId = r.Id, SaleId = r.SaleId, ReceiptNumber = r.ReturnNumber,
                Status = (int)r.Status, EventUtc = r.CreatedUtc, Amount = r.TotalAmount,
                LineCount = r.Lines.Count()
            });
            IQueryable<CustomerHistoryRow> rows;
            if (search.Kind == CustomerHistoryKind.Sale) rows = saleRows;
            else if (search.Kind == CustomerHistoryKind.Return) rows = returnRows;
            else rows = saleRows.Concat(returnRows);
            if (search.FromUtc.HasValue) rows = rows.Where(r => r.EventUtc >= search.FromUtc.Value);
            if (search.ToUtcExclusive.HasValue) rows = rows.Where(r => r.EventUtc < search.ToUtcExclusive.Value);
            var pageSize = Math.Max(1, Math.Min(200, search.PageSize));
            var total = rows.Count();
            var pages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            var page = Math.Max(1, Math.Min(pages, search.PageNumber));
            var data = rows.OrderByDescending(r => r.EventUtc).ThenByDescending(r => r.Kind)
                .ThenByDescending(r => r.DocumentId).Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return new CustomerHistoryPageDTO
            {
                TotalCount = total, TotalPages = pages, PageNumber = page, PageSize = pageSize,
                Items = data.Select(r => new CustomerHistoryItemDTO
                {
                    Kind = (CustomerHistoryKind)r.Kind, DocumentId = r.DocumentId, SaleId = r.SaleId,
                    ReceiptNumber = r.ReceiptNumber,
                    Status = Enum.IsDefined(typeof(DocumentStatus), r.Status) ? ((DocumentStatus)r.Status).ToString() : "Unknown",
                    EventUtc = DateTime.SpecifyKind(r.EventUtc, DateTimeKind.Utc),
                    Amount = r.Amount, LineCount = r.LineCount
                }).ToList()
            };
        }

        private sealed class CustomerHistoryRow
        {
            public int Kind { get; set; }
            public int DocumentId { get; set; }
            public int SaleId { get; set; }
            public string ReceiptNumber { get; set; }
            public int Status { get; set; }
            public DateTime EventUtc { get; set; }
            public decimal Amount { get; set; }
            public int LineCount { get; set; }
        }

        private static void Validate(CustomerDTO dto)
        {
            if (dto == null || dto.Id < 0) throw new ValidationException("Valid customer details are required.");
            var errors = new List<ValidationResult>();
            Validator.TryValidateObject(dto, new ValidationContext(dto), errors, true);
            if (errors.Count > 0) throw new ValidationException(string.Join(Environment.NewLine, errors.Select(e => e.ErrorMessage)));
            if (dto.Code.Any(char.IsControl) || dto.Name.Any(char.IsControl) ||
                new[] { dto.Phone, dto.Email, dto.Address, dto.TaxIdentifier }.Any(v => v != null && v.Any(char.IsControl)))
                throw new ValidationException("Customer details cannot contain control characters.");
        }

        private void Write(Action action)
        {
            if (_context.Database.CurrentTransaction != null || _context.ChangeTracker.HasChanges())
                throw new InvalidOperationException("Save customers using a context without pending changes or an active transaction.");
            ClearTracking();
            using (var audit = AuditOperation.Begin())
            using (var transaction = _context.Database.BeginTransaction(System.Data.IsolationLevel.Serializable))
            {
                try { action(); transaction.Commit(); }
                catch
                {
                    try { transaction.Rollback(); } finally { ClearTracking(); }
                    throw;
                }
            }
        }

        private static IQueryable<CustomerDTO> Project(IQueryable<Customer> query) => query.Select(c => new CustomerDTO
        {
            Id = c.Id, Code = c.Code, Name = c.Name, Phone = c.Phone, Email = c.Email,
            Address = c.Address, TaxIdentifier = c.TaxIdentifier, IsActive = c.IsActive
        });

        private static void DemandRevision(Customer customer, string revision)
        {
            var current = Revision(new CustomerDTO
            {
                Id = customer.Id, Code = customer.Code, Name = customer.Name, Phone = customer.Phone,
                Email = customer.Email, Address = customer.Address,
                TaxIdentifier = customer.TaxIdentifier, IsActive = customer.IsActive
            });
            if (string.IsNullOrEmpty(revision) || !string.Equals(revision, current, StringComparison.Ordinal))
                throw new ValidationException("The customer changed or its revision is missing. Reload and review it before saving.");
        }

        private static string Revision(Customer customer) => Revision(new CustomerDTO
        {
            Id = customer.Id, Code = customer.Code, Name = customer.Name, Phone = customer.Phone,
            Email = customer.Email, Address = customer.Address,
            TaxIdentifier = customer.TaxIdentifier, IsActive = customer.IsActive
        });

        private static string Revision(CustomerDTO customer)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(customer.Id); writer.Write(customer.IsActive);
                WriteNullable(writer, customer.Code); WriteNullable(writer, customer.Name);
                WriteNullable(writer, customer.Phone); WriteNullable(writer, customer.Email);
                WriteNullable(writer, customer.Address); WriteNullable(writer, customer.TaxIdentifier);
                writer.Flush();
                using (var hash = SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(stream.ToArray()));
            }
        }

        private static void WriteNullable(BinaryWriter writer, string value)
        {
            writer.Write(value != null); if (value != null) writer.Write(value);
        }
        private void ClearTracking()
        {
            foreach (var entry in _context.ChangeTracker.Entries().ToList()) entry.State = EntityState.Detached;
        }
        private static string Clean(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        private void Demand(ClaimActionType action) => PermissionGuard.Demand(_authorization, ResourceCodes.Customers, action);
    }
}
