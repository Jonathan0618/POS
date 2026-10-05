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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace POS.Services.Purchasing
{
    // The caller owns the context and supplies its matching audit dependencies.
    public sealed class SupplierService
    {
        private readonly POSContext _context;
        private readonly IAuthorizationService _authorization;

        public SupplierService(POSContext context, IAuthorizationService authorization)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        }

        public SupplierPageDTO Search(string search = null, bool? active = null, int pageNumber = 1, int pageSize = 50)
        {
            Demand(ClaimActionType.View);
            var term = Clean(search);
            if (term?.Length > 150) throw new ValidationException("Supplier search cannot exceed 150 characters.");
            IQueryable<Supplier> query = _context.Suppliers.AsNoTracking();
            if (term != null) query = query.Where(s => s.Code.Contains(term) || s.Name.Contains(term));
            if (active.HasValue) query = query.Where(s => s.IsActive == active.Value);
            var count = query.Count();
            pageSize = Math.Max(1, Math.Min(200, pageSize));
            var pages = Math.Max(1, (int)Math.Ceiling(count / (double)pageSize));
            pageNumber = Math.Max(1, Math.Min(pages, pageNumber));
            var items = Project(query.OrderBy(s => s.Name).ThenBy(s => s.Id)
                .Skip((pageNumber - 1) * pageSize).Take(pageSize)).ToList();
            foreach (var item in items) item.Revision = SupplierRevision(item);
            return new SupplierPageDTO
            {
                TotalCount = count, TotalPages = pages, PageNumber = pageNumber, PageSize = pageSize,
                Items = items
            };
        }

        public SupplierDTO GetDetails(int supplierId)
        {
            Demand(ClaimActionType.View);
            if (supplierId <= 0) throw new ValidationException("Select an existing supplier.");
            var result = Project(_context.Suppliers.AsNoTracking().Where(s => s.Id == supplierId)).SingleOrDefault()
                ?? throw new ValidationException("The supplier does not exist.");
            result.Revision = SupplierRevision(result);
            return result;
        }

        public int Save(SupplierDTO dto)
        {
            Demand(dto != null && dto.Id > 0 ? ClaimActionType.Edit : ClaimActionType.Add);
            if (dto == null || dto.Id < 0) throw new ValidationException("Valid supplier details are required.");
            var errors = new List<ValidationResult>();
            Validator.TryValidateObject(dto, new ValidationContext(dto), errors, true);
            if (errors.Count > 0) throw new ValidationException(string.Join(Environment.NewLine, errors.Select(e => e.ErrorMessage)));
            var code = dto.Code.Trim().ToUpperInvariant();
            if (code.Any(char.IsControl)) throw new ValidationException("Supplier code cannot contain control characters.");
            var id = 0;
            Write(() =>
            {
                var supplier = dto.Id == 0 ? new Supplier { IsActive = true } : _context.Suppliers.Find(dto.Id);
                if (supplier == null) throw new ValidationException("The supplier no longer exists. Reload suppliers.");
                if (dto.Id != 0) DemandSupplierRevision(supplier, dto.Revision);
                if (_context.Suppliers.Any(s => s.Id != dto.Id && s.Code.Trim().ToUpper() == code))
                    throw new ValidationException("This supplier code is already in use, including inactive suppliers.");
                supplier.Code = code;
                supplier.Name = dto.Name.Trim();
                supplier.ContactName = Clean(dto.ContactName);
                supplier.Phone = Clean(dto.Phone);
                supplier.Email = Clean(dto.Email);
                supplier.Address = Clean(dto.Address);
                supplier.TaxIdentifier = Clean(dto.TaxIdentifier);
                if (dto.Id == 0) _context.Suppliers.Add(supplier);
                _context.SaveChanges();
                id = supplier.Id;
            });
            return id;
        }

        public void SetActive(int supplierId, bool active, string revision)
        {
            Demand(ClaimActionType.Edit);
            if (supplierId <= 0) throw new ValidationException("Select an existing supplier.");
            Write(() =>
            {
                var supplier = _context.Suppliers.Find(supplierId);
                if (supplier == null) throw new ValidationException("The supplier does not exist.");
                DemandSupplierRevision(supplier, revision);
                if (supplier.IsActive == active) return;
                supplier.IsActive = active;
                _context.SaveChanges();
            });
        }

        public int SaveProductLink(SupplierProductDTO dto)
        {
            Demand(ClaimActionType.Edit);
            if (dto == null) throw new ValidationException("Supplier product details are required.");
            var errors = new List<ValidationResult>();
            Validator.TryValidateObject(dto, new ValidationContext(dto), errors, true);
            if (errors.Count > 0) throw new ValidationException(string.Join(Environment.NewLine, errors.Select(e => e.ErrorMessage)));
            if (decimal.Round(dto.DefaultCost, 2) != dto.DefaultCost)
                throw new ValidationException("Default cost supports at most two decimal places.");
            var sku = Clean(dto.SupplierSku);
            if (sku != null && sku.Any(char.IsControl))
                throw new ValidationException("Supplier SKU cannot contain control characters.");
            var id = 0;
            Write(() =>
            {
                if (!_context.Suppliers.Any(s => s.Id == dto.SupplierId && s.IsActive))
                    throw new ValidationException("Select an active supplier before maintaining its products.");
                if (!_context.Set<POS.Domains.BusinessObjects.Product>().Any(p => p.Id == dto.ProductId && p.IsActive))
                    throw new ValidationException("Select an active product to link to this supplier.");
                var link = _context.SupplierProducts.SingleOrDefault(p => p.SupplierId == dto.SupplierId && p.ProductId == dto.ProductId);
                if (dto.Id < 0 || (dto.Id != 0 && (link == null || link.Id != dto.Id)))
                    throw new ValidationException("The supplier-product link changed. Reload before saving.");
                if (link != null && dto.Id == 0)
                    throw new ValidationException("This supplier-product link already exists. Reload it before editing.");
                if (link != null) DemandLinkRevision(link, dto.Revision);
                if (link == null)
                {
                    link = new SupplierProduct { SupplierId = dto.SupplierId, ProductId = dto.ProductId };
                    _context.SupplierProducts.Add(link);
                }
                link.SupplierSku = sku;
                link.DefaultCost = dto.DefaultCost;
                link.LeadTimeDays = dto.LeadTimeDays;
                _context.SaveChanges();
                id = link.Id;
            });
            return id;
        }

        public void UnlinkProduct(SupplierProductDTO reviewedLink)
        {
            Demand(ClaimActionType.Edit);
            if (reviewedLink == null || reviewedLink.Id <= 0 ||
                reviewedLink.SupplierId <= 0 || reviewedLink.ProductId <= 0)
                throw new ValidationException("Load an existing supplier-product link before unlinking.");
            Write(() =>
            {
                var link = _context.SupplierProducts.SingleOrDefault(p => p.Id == reviewedLink.Id);
                if (link == null)
                    throw new ValidationException("The supplier-product link no longer exists. Reload supplier products.");
                if (link.SupplierId != reviewedLink.SupplierId || link.ProductId != reviewedLink.ProductId)
                    throw new ValidationException("The supplier-product link changed. Reload it before unlinking.");
                DemandLinkRevision(link, reviewedLink.Revision);
                _context.SupplierProducts.Remove(link);
                _context.SaveChanges();
            });
        }

        public SupplierProductPageDTO GetProducts(int supplierId, int pageNumber = 1, int pageSize = 50)
        {
            Demand(ClaimActionType.View);
            PermissionGuard.Demand(_authorization, ResourceCodes.Products, ClaimActionType.View);
            if (supplierId <= 0) throw new ValidationException("Select an existing supplier.");
            if (!_context.Suppliers.AsNoTracking().Any(s => s.Id == supplierId))
                throw new ValidationException("The supplier does not exist.");
            var query = _context.SupplierProducts.AsNoTracking().Where(link => link.SupplierId == supplierId);
            var total = query.Count();
            pageSize = Math.Max(1, Math.Min(200, pageSize));
            var pages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            pageNumber = Math.Max(1, Math.Min(pages, pageNumber));
            var items = query.OrderBy(link => link.Product.Name).ThenBy(link => link.Id)
                .Skip((pageNumber - 1) * pageSize).Take(pageSize)
                .Select(link => new SupplierProductDTO
                {
                    Id = link.Id, SupplierId = link.SupplierId, ProductId = link.ProductId,
                    SupplierSku = link.SupplierSku, DefaultCost = link.DefaultCost,
                    LeadTimeDays = link.LeadTimeDays, ProductName = link.Product.Name,
                    ProductSku = link.Product.Sku, ProductIsActive = link.Product.IsActive
                }).ToList();
            foreach (var item in items)
                item.Revision = LinkRevision(item.Id, item.SupplierId, item.ProductId,
                    item.SupplierSku, item.DefaultCost, item.LeadTimeDays);
            return new SupplierProductPageDTO
            {
                TotalCount = total, TotalPages = pages, PageNumber = pageNumber, PageSize = pageSize,
                Items = items
            };
        }

        public SupplierPurchaseHistoryPageDTO GetPurchaseHistory(SupplierPurchaseHistorySearchDTO search)
        {
            Demand(ClaimActionType.View);
            PermissionGuard.Demand(_authorization, ResourceCodes.Purchasing, ClaimActionType.View);
            if (search == null || search.SupplierId <= 0)
                throw new ValidationException("Select an existing supplier and provide purchase-history options.");
            if (search.Kind.HasValue && !Enum.IsDefined(typeof(SupplierPurchaseDocumentKind), search.Kind.Value))
                throw new ValidationException("Select a valid purchase document kind.");
            if (search.ProductId.HasValue && search.ProductId.Value <= 0)
                throw new ValidationException("Select a valid product or search all products.");
            if ((search.FromUtc.HasValue && search.FromUtc.Value.Kind != DateTimeKind.Utc) ||
                (search.ToUtcExclusive.HasValue && search.ToUtcExclusive.Value.Kind != DateTimeKind.Utc))
                throw new ValidationException("Purchase-history date filters must use UTC.");
            if (search.FromUtc.HasValue && search.ToUtcExclusive.HasValue && search.FromUtc >= search.ToUtcExclusive)
                throw new ValidationException("The end of the purchase-history range must be after its start.");
            var documentNumber = Clean(search.DocumentNumber);
            if (documentNumber?.Length > 50)
                throw new ValidationException("Document-number search cannot exceed 50 characters.");
            if (!_context.Suppliers.AsNoTracking().Any(s => s.Id == search.SupplierId))
                throw new ValidationException("The supplier does not exist.");

            var orders = _context.PurchaseOrders.AsNoTracking().Where(o => o.SupplierId == search.SupplierId);
            var receipts = _context.GoodsReceipts.AsNoTracking().Where(r => r.SupplierId == search.SupplierId);
            if (search.ProductId.HasValue)
            {
                var productId = search.ProductId.Value;
                orders = orders.Where(o => o.Lines.Any(line => line.ProductId == productId));
                receipts = receipts.Where(r => r.Lines.Any(line => line.ProductId == productId));
            }
            if (documentNumber != null)
            {
                orders = orders.Where(o => o.OrderNumber.Contains(documentNumber));
                receipts = receipts.Where(r => r.ReceiptNumber.Contains(documentNumber) ||
                    (r.SupplierReference != null && r.SupplierReference.Contains(documentNumber)));
            }

            IQueryable<SupplierPurchaseHistoryRow> rows;
            var orderRows = orders.Select(o => new SupplierPurchaseHistoryRow
            {
                Kind = 0, DocumentId = o.Id, DocumentNumber = o.OrderNumber,
                Status = (int)o.Status, EventUtc = o.OrderedUtc ?? o.CreatedUtc,
                TotalAmount = o.TotalAmount, LineCount = o.Lines.Count(),
                PurchaseOrderId = (int?)o.Id, SupplierReference = null
            });
            var receiptRows = receipts.Select(r => new SupplierPurchaseHistoryRow
            {
                Kind = 1, DocumentId = r.Id, DocumentNumber = r.ReceiptNumber,
                Status = (int)r.Status, EventUtc = r.ReceivedUtc,
                TotalAmount = r.Lines.Sum(line => (decimal?)(line.UnitCost * line.Quantity)) ?? 0m,
                LineCount = r.Lines.Count(), PurchaseOrderId = r.PurchaseOrderId,
                SupplierReference = r.SupplierReference
            });
            if (search.Kind == SupplierPurchaseDocumentKind.PurchaseOrder) rows = orderRows;
            else if (search.Kind == SupplierPurchaseDocumentKind.GoodsReceipt) rows = receiptRows;
            else rows = orderRows.Concat(receiptRows);
            if (search.FromUtc.HasValue) rows = rows.Where(r => r.EventUtc >= search.FromUtc.Value);
            if (search.ToUtcExclusive.HasValue) rows = rows.Where(r => r.EventUtc < search.ToUtcExclusive.Value);

            var pageSize = Math.Max(1, Math.Min(200, search.PageSize));
            var totalCount = rows.Count();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            var pageNumber = Math.Max(1, Math.Min(totalPages, search.PageNumber));
            var page = rows.OrderByDescending(r => r.EventUtc).ThenByDescending(r => r.Kind)
                .ThenByDescending(r => r.DocumentId).Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
            return new SupplierPurchaseHistoryPageDTO
            {
                TotalCount = totalCount, TotalPages = totalPages, PageNumber = pageNumber, PageSize = pageSize,
                Items = page.Select(r => new SupplierPurchaseHistoryItemDTO
                {
                    Kind = (SupplierPurchaseDocumentKind)r.Kind, DocumentId = r.DocumentId,
                    DocumentNumber = r.DocumentNumber,
                    Status = Enum.IsDefined(typeof(DocumentStatus), r.Status)
                        ? ((DocumentStatus)r.Status).ToString() : "Unknown",
                    EventUtc = DateTime.SpecifyKind(r.EventUtc, DateTimeKind.Utc),
                    TotalAmount = r.TotalAmount, LineCount = r.LineCount,
                    PurchaseOrderId = r.PurchaseOrderId, SupplierReference = r.SupplierReference
                }).ToList()
            };
        }

        private sealed class SupplierPurchaseHistoryRow
        {
            public int Kind { get; set; }
            public int DocumentId { get; set; }
            public string DocumentNumber { get; set; }
            public int Status { get; set; }
            public DateTime EventUtc { get; set; }
            public decimal TotalAmount { get; set; }
            public int LineCount { get; set; }
            public int? PurchaseOrderId { get; set; }
            public string SupplierReference { get; set; }
        }

        private static void DemandLinkRevision(SupplierProduct link, string revision)
        {
            var current = LinkRevision(link.Id, link.SupplierId, link.ProductId,
                link.SupplierSku, link.DefaultCost, link.LeadTimeDays);
            if (string.IsNullOrEmpty(revision) || !string.Equals(revision, current, StringComparison.Ordinal))
                throw new ValidationException("The supplier-product link changed or its revision is missing. Reload and review it before saving.");
        }

        private static string LinkRevision(int id, int supplierId, int productId,
            string supplierSku, decimal defaultCost, int leadTimeDays)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    writer.Write(id); writer.Write(supplierId); writer.Write(productId);
                    writer.Write(supplierSku != null);
                    if (supplierSku != null) writer.Write(supplierSku);
                    writer.Write(defaultCost.ToString(CultureInfo.InvariantCulture));
                    writer.Write(leadTimeDays);
                }
                using (var hash = SHA256.Create())
                    return Convert.ToBase64String(hash.ComputeHash(stream.ToArray()));
            }
        }

        private void Write(Action action)
        {
            if (_context.Database.CurrentTransaction != null || _context.ChangeTracker.HasChanges())
                throw new InvalidOperationException("Save suppliers using a context without pending changes or an active transaction.");
            ClearTracking();
            using (var audit = AuditOperation.Begin())
            using (var transaction = _context.Database.BeginTransaction(System.Data.IsolationLevel.Serializable))
            {
                try { action(); transaction.Commit(); }
                catch
                {
                    try { transaction.Rollback(); }
                    finally { ClearTracking(); }
                    throw;
                }
            }
        }

        private static IQueryable<SupplierDTO> Project(IQueryable<Supplier> query) => query.Select(s => new SupplierDTO
        {
            Id = s.Id, Code = s.Code, Name = s.Name, ContactName = s.ContactName,
            Phone = s.Phone, Email = s.Email, Address = s.Address,
            TaxIdentifier = s.TaxIdentifier, IsActive = s.IsActive
        });

        private static void DemandSupplierRevision(Supplier supplier, string revision)
        {
            var current = SupplierRevision(new SupplierDTO
            {
                Id = supplier.Id, Code = supplier.Code, Name = supplier.Name,
                ContactName = supplier.ContactName, Phone = supplier.Phone,
                Email = supplier.Email, Address = supplier.Address,
                TaxIdentifier = supplier.TaxIdentifier, IsActive = supplier.IsActive
            });
            if (string.IsNullOrEmpty(revision) || !string.Equals(revision, current, StringComparison.Ordinal))
                throw new ValidationException("The supplier changed or its revision is missing. Reload and review it before saving.");
        }

        private static string SupplierRevision(SupplierDTO supplier)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    writer.Write(supplier.Id); writer.Write(supplier.IsActive);
                    WriteNullable(writer, supplier.Code); WriteNullable(writer, supplier.Name);
                    WriteNullable(writer, supplier.ContactName); WriteNullable(writer, supplier.Phone);
                    WriteNullable(writer, supplier.Email); WriteNullable(writer, supplier.Address);
                    WriteNullable(writer, supplier.TaxIdentifier);
                }
                using (var hash = SHA256.Create())
                    return Convert.ToBase64String(hash.ComputeHash(stream.ToArray()));
            }
        }

        private static void WriteNullable(BinaryWriter writer, string value)
        {
            writer.Write(value != null);
            if (value != null) writer.Write(value);
        }

        private void ClearTracking()
        {
            foreach (var entry in _context.ChangeTracker.Entries().ToList()) entry.State = EntityState.Detached;
        }
        private static string Clean(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        private void Demand(ClaimActionType action) => PermissionGuard.Demand(_authorization, ResourceCodes.Suppliers, action);
    }
}
