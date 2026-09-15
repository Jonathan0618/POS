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

namespace POS.Services.Settings
{
    public sealed class RegisterStationService
    {
        private readonly POSContext _context;
        private readonly IAuthorizationService _authorization;

        public RegisterStationService(POSContext context, IAuthorizationService authorization)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        }

        public RegisterStationPageDTO Search(string search = null, bool? active = null,
            int pageNumber = 1, int pageSize = 50)
        {
            Demand(ClaimActionType.View);
            var term = Clean(search);
            if (term?.Length > 100) throw new ValidationException("Register search cannot exceed 100 characters.");
            IQueryable<RegisterStation> query = _context.RegisterStations.AsNoTracking();
            if (term != null) query = query.Where(r => r.Code.Contains(term) || r.Name.Contains(term));
            if (active.HasValue) query = query.Where(r => r.IsActive == active.Value);
            var total = query.Count();
            pageSize = Math.Max(1, Math.Min(200, pageSize));
            var pages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            pageNumber = Math.Max(1, Math.Min(pages, pageNumber));
            var items = Project(query.OrderBy(r => r.Code).ThenBy(r => r.Id)
                .Skip((pageNumber - 1) * pageSize).Take(pageSize)).ToList();
            foreach (var item in items) item.Revision = Revision(item);
            return new RegisterStationPageDTO
            {
                TotalCount = total, TotalPages = pages, PageNumber = pageNumber,
                PageSize = pageSize, Items = items
            };
        }

        public RegisterStationDTO GetDetails(int registerStationId)
        {
            Demand(ClaimActionType.View);
            if (registerStationId <= 0) throw new ValidationException("Select an existing register.");
            var result = Project(_context.RegisterStations.AsNoTracking()
                .Where(r => r.Id == registerStationId)).SingleOrDefault();
            if (result == null) throw new ValidationException("The register does not exist.");
            result.Revision = Revision(result);
            return result;
        }

        public int Save(RegisterStationDTO dto)
        {
            Demand(dto != null && dto.Id > 0 ? ClaimActionType.Edit : ClaimActionType.Add);
            Validate(dto);
            var code = dto.Code.Trim().ToUpperInvariant();
            var id = 0;
            Write(() =>
            {
                var register = dto.Id == 0 ? new RegisterStation { IsActive = true } :
                    _context.RegisterStations.Find(dto.Id);
                if (register == null) throw new ValidationException("The register no longer exists. Reload registers.");
                if (dto.Id != 0) DemandRevision(register, dto.Revision);
                if (_context.RegisterStations.Any(r => r.Id != dto.Id && r.Code.Trim().ToUpper() == code))
                    throw new ValidationException("This register code is already in use, including inactive registers.");
                register.Code = code; register.Name = dto.Name.Trim();
                register.PrinterName = Clean(dto.PrinterName);
                if (dto.Id == 0) _context.RegisterStations.Add(register);
                _context.SaveChanges(); id = register.Id;
            });
            return id;
        }

        public void SetActive(int registerStationId, bool active, string revision)
        {
            Demand(ClaimActionType.Edit);
            if (registerStationId <= 0) throw new ValidationException("Select an existing register.");
            Write(() =>
            {
                var register = _context.RegisterStations.Find(registerStationId);
                if (register == null) throw new ValidationException("The register does not exist.");
                DemandRevision(register, revision);
                if (register.IsActive == active) return;
                if (!active && _context.CashierShifts.Any(s => s.RegisterStationId == registerStationId &&
                    s.Status == ShiftStatus.Open))
                    throw new ValidationException("Close the register's open cashier shift before deactivating it.");
                register.IsActive = active; _context.SaveChanges();
            });
        }

        private static void Validate(RegisterStationDTO dto)
        {
            if (dto == null || dto.Id < 0) throw new ValidationException("Valid register details are required.");
            var errors = new List<ValidationResult>();
            Validator.TryValidateObject(dto, new ValidationContext(dto), errors, true);
            if (errors.Count > 0) throw new ValidationException(string.Join(Environment.NewLine, errors.Select(e => e.ErrorMessage)));
            if (dto.Code.Any(char.IsControl) || dto.Name.Any(char.IsControl) ||
                (dto.PrinterName != null && dto.PrinterName.Any(char.IsControl)))
                throw new ValidationException("Register details cannot contain control characters.");
        }

        private void Write(Action action)
        {
            if (_context.Database.CurrentTransaction != null || _context.ChangeTracker.HasChanges())
                throw new InvalidOperationException("Save registers using a context without pending changes or an active transaction.");
            ClearTracking();
            using (var audit = AuditOperation.Begin())
            using (var transaction = _context.Database.BeginTransaction(System.Data.IsolationLevel.Serializable))
            {
                try { action(); transaction.Commit(); }
                catch (Exception exception)
                {
                    try { transaction.Rollback(); } finally { ClearTracking(); }
                    if (Inventory.InventoryConflict.IsUniqueIndexViolation(exception, "IX_Code"))
                        throw new ValidationException("This register code was saved concurrently. Reload registers.", exception);
                    throw;
                }
            }
        }

        private static IQueryable<RegisterStationDTO> Project(IQueryable<RegisterStation> query) =>
            query.Select(r => new RegisterStationDTO
            {
                Id = r.Id, Code = r.Code, Name = r.Name,
                PrinterName = r.PrinterName, IsActive = r.IsActive
            });

        private static void DemandRevision(RegisterStation register, string revision)
        {
            var current = Revision(new RegisterStationDTO
            {
                Id = register.Id, Code = register.Code, Name = register.Name,
                PrinterName = register.PrinterName, IsActive = register.IsActive
            });
            if (string.IsNullOrEmpty(revision) || !string.Equals(revision, current, StringComparison.Ordinal))
                throw new ValidationException("The register changed or its revision is missing. Reload and review it before saving.");
        }

        private static string Revision(RegisterStationDTO register)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(register.Id); writer.Write(register.IsActive);
                WriteNullable(writer, register.Code); WriteNullable(writer, register.Name);
                WriteNullable(writer, register.PrinterName); writer.Flush();
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
        private void Demand(ClaimActionType action) => PermissionGuard.Demand(_authorization, ResourceCodes.Settings, action);
    }
}
