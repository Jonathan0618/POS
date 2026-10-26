using POS.Common.Enumerations;
using POS.Core.Abstractions;
using POS.Core.Security;
using POS.Data.Context;
using POS.Domains.AuditEntry;
using POS.Domains.BusinessObjects;
using POS.Models.Operations;
using POS.Services.Security;
using System;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Text;

namespace POS.Services.Point_of_Sale
{
    public sealed class SaleReceiptService : IDisposable
    {
        private readonly POSContext _context;
        private readonly ICurrentUser _currentUser;
        private readonly IAuthorizationService _authorization;
        private readonly IReceiptPrinter _printer;
        private readonly IClock _clock;

        public SaleReceiptService(POSContext context, ICurrentUser currentUser, IAuthorizationService authorization,
            IReceiptPrinter printer, IClock clock)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
            _printer = printer ?? throw new ArgumentNullException(nameof(printer));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public SaleReceiptPageDTO Search(SaleReceiptSearchDTO filter)
        {
            PermissionGuard.Demand(_authorization, ResourceCodes.Sales, ClaimActionType.View);
            if (filter == null) throw new ArgumentNullException(nameof(filter));
            if (filter.FromUtc.HasValue && filter.ToUtcExclusive.HasValue && filter.FromUtc >= filter.ToUtcExclusive)
                throw new InvalidOperationException("The receipt date range is invalid.");
            var search = filter.Search?.Trim();
            if ((search?.Length ?? 0) > 100 || (filter.CashierUserId?.Length ?? 0) > 128)
                throw new InvalidOperationException("Receipt search text is too long.");
            var size = Math.Max(1, Math.Min(200, filter.PageSize));
            var query = _context.Sales.AsNoTracking().Where(x =>
                x.Status == DocumentStatus.Completed || x.Status == DocumentStatus.PartiallyReturned || x.Status == DocumentStatus.Returned);
            if (!string.IsNullOrEmpty(search)) query = query.Where(x =>
                x.ReceiptNumber.Contains(search) || x.CustomerName.Contains(search) || x.RegisterCode.Contains(search));
            if (filter.FromUtc.HasValue) query = query.Where(x => x.SaleDate >= filter.FromUtc.Value);
            if (filter.ToUtcExclusive.HasValue) query = query.Where(x => x.SaleDate < filter.ToUtcExclusive.Value);
            if (filter.RegisterStationId.HasValue) query = query.Where(x => x.RegisterStationId == filter.RegisterStationId.Value);
            if (!string.IsNullOrWhiteSpace(filter.CashierUserId)) query = query.Where(x => x.CashierUserId == filter.CashierUserId);
            var count = query.Count();
            var pages = Math.Max(1, (int)Math.Ceiling(count / (double)size));
            var page = Math.Max(1, Math.Min(pages, filter.PageNumber));
            return new SaleReceiptPageDTO
            {
                TotalCount = count,
                TotalPages = pages,
                PageNumber = page,
                Items = query.OrderByDescending(x => x.SaleDate).ThenByDescending(x => x.Id)
                    .Skip((page - 1) * size).Take(size).Select(x => new SaleReceiptSummaryDTO
                    {
                        SaleId = x.Id, ReceiptNumber = x.ReceiptNumber, SaleDateUtc = x.SaleDate, Status = x.Status,
                        RegisterCode = x.RegisterCode, CashierName = x.CashierName, CustomerName = x.CustomerName,
                        TotalAmount = x.TotalAmount, CurrencyCode = x.CurrencyCode
                    }).ToList()
            };
        }

        public SaleReceiptDTO Get(int saleId)
        {
            PermissionGuard.Demand(_authorization, ResourceCodes.Sales, ClaimActionType.View);
            var sale = LoadCompleted(saleId);
            return Map(sale);
        }

        public void Print(int saleId, bool reprint)
        {
            PermissionGuard.Demand(_authorization, ResourceCodes.Sales, reprint ? ClaimActionType.Edit : ClaimActionType.View);
            if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUser.UserId))
                throw new UnauthorizedAccessException("An authenticated user is required to print a receipt.");
            var sale = LoadCompleted(saleId);
            var register = _context.RegisterStations.AsNoTracking().SingleOrDefault(x => x.Id == sale.RegisterStationId);
            if (register == null || string.IsNullOrWhiteSpace(register.PrinterName))
                throw new InvalidOperationException("The sale register has no configured receipt printer.");
            var content = Format(Map(sale), reprint);
            var result = _printer.PrintReceipt(register.PrinterName,
                (reprint ? "REPRINT " : "Receipt ") + sale.ReceiptNumber, content);
            if (!result.Succeeded)
                throw new InvalidOperationException(string.Join(" ", result.Errors));

            using (var audit = AuditOperation.Begin(sale.RegisterStationId))
            {
                _context.AuditLogs.Add(new AuditLog
                {
                    TableName = "Sale",
                    RecordId = sale.Id.ToString(CultureInfo.InvariantCulture),
                    Action = reprint ? "Reprinted" : "Printed",
                    NewValue = "{\"ReceiptNumber\":\"" + sale.ReceiptNumber + "\"}",
                    UserId = _currentUser.UserId,
                    DateLogged = _clock.UtcNow
                });
                _context.SaveChanges();
            }
        }

        private Sale LoadCompleted(int id)
        {
            if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
            var sale = _context.Sales.AsNoTracking().Include(x => x.SaleItems).Include(x => x.Payments)
                .SingleOrDefault(x => x.Id == id);
            if (sale == null || (sale.Status != DocumentStatus.Completed && sale.Status != DocumentStatus.PartiallyReturned && sale.Status != DocumentStatus.Returned))
                throw new InvalidOperationException("The completed sale receipt was not found.");
            return sale;
        }

        private static SaleReceiptDTO Map(Sale x) => new SaleReceiptDTO
        {
            SaleId = x.Id, ReceiptNumber = x.ReceiptNumber, SaleDateUtc = x.SaleDate, Status = x.Status,
            StoreName = x.StoreName, StoreAddress = x.StoreAddress, StoreTaxIdentifier = x.StoreTaxIdentifier,
            ReceiptFooter = x.ReceiptFooter, RegisterCode = x.RegisterCode, RegisterName = x.RegisterName,
            CashierUserId = x.CashierUserId, CashierName = x.CashierName, CustomerCode = x.CustomerCode,
            CustomerName = x.CustomerName, Subtotal = x.Subtotal, DiscountAmount = x.DiscountAmount,
            TaxAmount = x.TaxAmount, RoundingAmount = x.RoundingAmount, TotalAmount = x.TotalAmount,
            CashReceived = x.CashReceived, Change = x.Change, CurrencyCode = x.CurrencyCode,
            TaxName = x.TaxName, TaxInclusive = x.TaxInclusive, MoneyDecimalPlaces = x.MoneyDecimalPlaces,
            Items = x.SaleItems.OrderBy(i => i.Id).Select(i => new SaleReceiptLineDTO
            {
                ProductName = i.ProductName, Sku = i.Sku, Barcode = i.Barcode, Quantity = i.Quantity,
                UnitPrice = i.UnitPrice, DiscountAmount = i.DiscountAmount, TaxRate = i.TaxRate,
                TaxAmount = i.TaxAmount, LineTotal = i.Subtotal + i.TaxAmount
            }).ToList(),
            Payments = x.Payments.OrderBy(p => p.Id).Select(p => new SaleReceiptPaymentDTO
            {
                TenderType = p.TenderType, Status = p.Status, Amount = p.Amount,
                ExternalReference = p.ExternalReference, CreatedUtc = p.CreatedUtc
            }).ToList()
        };

        private static string Format(SaleReceiptDTO receipt, bool reprint)
        {
            var text = new StringBuilder();
            if (reprint) text.AppendLine("*** REPRINT ***");
            text.AppendLine(receipt.StoreName).AppendLine(receipt.StoreAddress).AppendLine("Receipt: " + receipt.ReceiptNumber)
                .AppendLine("Date: " + receipt.SaleDateUtc.ToString("u")).AppendLine("Register: " + receipt.RegisterCode)
                .AppendLine("Cashier: " + receipt.CashierName).AppendLine(new string('-', 32));
            foreach (var item in receipt.Items)
                text.AppendLine(item.ProductName).AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0} x {1:N2}  {2:N2}", item.Quantity, item.UnitPrice, item.LineTotal));
            text.AppendLine(new string('-', 32)).AppendLine("Total: " + receipt.TotalAmount.ToString("N2", CultureInfo.InvariantCulture));
            foreach (var payment in receipt.Payments)
                text.AppendLine(payment.TenderType + ": " + payment.Amount.ToString("N2", CultureInfo.InvariantCulture));
            if (receipt.Change > 0) text.AppendLine("Change: " + receipt.Change.ToString("N2", CultureInfo.InvariantCulture));
            text.AppendLine(receipt.ReceiptFooter);
            return text.ToString();
        }

        public void Dispose() { }
    }
}
