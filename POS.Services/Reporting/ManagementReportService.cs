using POS.Common.Enumerations;
using POS.Core.Abstractions;
using POS.Core.Security;
using POS.Data.Context;
using POS.Models.Operations;
using POS.Services.Security;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data.Entity;
using System.Linq;

namespace POS.Services.Reporting
{
    public sealed class ManagementReportService : IDisposable
    {
        private readonly POSContext _context;
        private readonly IAuthorizationService _authorization;
        private readonly IClock _clock;
        private readonly bool _ownsContext;

        public ManagementReportService() : this(new POSContext(), new ClaimsAuthorizationService(new CurrentUserAccessor()), new SystemClock(), true) { }
        public ManagementReportService(POSContext context, IAuthorizationService authorization, IClock clock)
            : this(context, authorization, clock, false) { }

        private ManagementReportService(POSContext context, IAuthorizationService authorization, IClock clock, bool ownsContext)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _ownsContext = ownsContext;
        }

        public DashboardDTO GetDashboard(ManagementFilterDTO filter)
        {
            PermissionGuard.Demand(_authorization, ResourceCodes.Dashboard, ClaimActionType.View);
            var scope = Validate(filter, 100);
            var financials = GetFinancials(scope);
            var items = GetItems(scope, 10);
            var inventory = GetInventory(scope, 100);
            var now = _clock.UtcNow;
            var openShifts = _context.CashierShifts.AsNoTracking().Where(x => x.Status == ShiftStatus.Open);
            if (scope.RegisterStationId.HasValue) openShifts = openShifts.Where(x => x.RegisterStationId == scope.RegisterStationId.Value);
            return new DashboardDTO
            {
                GeneratedUtc = now,
                Filter = scope,
                Financials = financials,
                TopProducts = items.OrderByDescending(x => x.GrossSales - x.Refunds).ThenBy(x => x.ProductName).Take(10).ToList(),
                StockAlerts = inventory.Where(x => x.QuantityOnHand <= x.AlertThreshold).OrderBy(x => x.QuantityOnHand).ThenBy(x => x.ProductName).Take(100).ToList(),
                ExpiringProductCount = inventory.Count(x => x.ExpiryDate.HasValue && x.ExpiryDate.Value >= now && x.ExpiryDate.Value < now.AddDays(30)),
                OpenShiftCount = openShifts.Count(),
                OpenShiftIssueCount = openShifts.Count(x => x.ExpectedCash < 0 || DbFunctions.DiffHours(x.OpenedUtc, now) >= 24)
            };
        }

        public ManagementReportDTO GetReport(ManagementFilterDTO filter)
        {
            PermissionGuard.Demand(_authorization, ResourceCodes.Reports, ClaimActionType.View);
            var scope = Validate(filter, 500);
            return new ManagementReportDTO
            {
                GeneratedUtc = _clock.UtcNow,
                Filter = scope,
                Financials = GetFinancials(scope),
                Sales = GetSales(scope),
                Items = GetItems(scope, scope.MaximumRows),
                Tenders = GetTenders(scope),
                Inventory = GetInventory(scope, scope.MaximumRows),
                Movements = GetMovements(scope),
                Purchasing = GetPurchasing(scope),
                Shifts = GetShifts(scope),
                AuditActivity = GetAuditActivity(scope)
            };
        }

        private FinancialSummaryDTO GetFinancials(ManagementFilterDTO f)
        {
            var sales = CompletedSales(f);
            var returns = PostedReturns(f);
            var saleTotals = sales.GroupBy(x => 1).Select(x => new
            {
                Gross = x.Sum(v => v.TotalAmount), Tax = x.Sum(v => v.TaxAmount),
                Discount = x.Sum(v => v.DiscountAmount), Count = x.Count()
            }).SingleOrDefault();
            var refundTotals = returns.GroupBy(x => 1).Select(x => new { Amount = x.Sum(v => v.TotalAmount), Count = x.Count() }).SingleOrDefault();
            var cost = (from item in _context.SaleItems.AsNoTracking()
                        join sale in sales on item.SaleId equals sale.Id
                        select (decimal?)(item.CostPrice * item.Quantity)).Sum() ?? 0m;
            var refundedCost = (from line in _context.SaleReturnLines.AsNoTracking()
                                join ret in returns on line.SaleReturnId equals ret.Id
                                join item in _context.SaleItems.AsNoTracking() on line.SaleItemId equals item.Id
                                select (decimal?)(item.CostPrice * line.Quantity)).Sum() ?? 0m;
            var gross = saleTotals?.Gross ?? 0m;
            var refunds = refundTotals?.Amount ?? 0m;
            var estimatedCost = cost - refundedCost;
            return new FinancialSummaryDTO
            {
                GrossSales = gross, Refunds = refunds, NetSales = gross - refunds,
                Tax = saleTotals?.Tax ?? 0m, Discounts = saleTotals?.Discount ?? 0m,
                EstimatedCost = estimatedCost, EstimatedMargin = gross - refunds - estimatedCost,
                TransactionCount = saleTotals?.Count ?? 0, ReturnCount = refundTotals?.Count ?? 0
            };
        }

        private IReadOnlyList<SalesReportRowDTO> GetSales(ManagementFilterDTO f)
        {
            var refunds = PostedReturns(f)
                .GroupBy(x => x.SaleId).Select(x => new { SaleId = x.Key, Amount = x.Sum(v => v.TotalAmount) });
            return (from sale in ScopedSales(f)
                    join refund in refunds on sale.Id equals refund.SaleId into refundRows
                    from refund in refundRows.DefaultIfEmpty()
                    orderby sale.SaleDate descending, sale.Id descending
                    select new SalesReportRowDTO
                    {
                        SaleId = sale.Id, SaleUtc = sale.SaleDate, ReceiptNumber = sale.ReceiptNumber, Status = sale.Status,
                        RegisterStationId = sale.RegisterStationId, RegisterCode = sale.RegisterCode,
                        CashierUserId = sale.CashierUserId, CashierName = sale.CashierName,
                        CustomerId = sale.CustomerId, CustomerName = sale.CustomerName,
                        Subtotal = sale.Subtotal, Discount = sale.DiscountAmount, Tax = sale.TaxAmount,
                        Total = sale.TotalAmount, Refunded = refund == null ? 0m : refund.Amount,
                        NetTotal = sale.TotalAmount - (refund == null ? 0m : refund.Amount)
                    }).Take(f.MaximumRows).ToList();
        }

        private IReadOnlyList<ItemSalesReportRowDTO> GetItems(ManagementFilterDTO f, int maximumRows)
        {
            var sold = from item in _context.SaleItems.AsNoTracking()
                       join sale in CompletedSales(f) on item.SaleId equals sale.Id
                       join product in _context.Set<POS.Domains.BusinessObjects.Product>().AsNoTracking() on item.ProductId equals product.Id
                       join category in _context.Set<POS.Domains.BusinessObjects.Category>().AsNoTracking() on product.CategoryId equals category.Id
                       where (!f.ProductId.HasValue || item.ProductId == f.ProductId.Value) &&
                             (!f.CategoryId.HasValue || product.CategoryId == f.CategoryId.Value)
                       group new { item, category } by new { item.ProductId, item.Sku, item.ProductName, CategoryName = category.Name } into rows
                       select new { rows.Key, Quantity = rows.Sum(x => x.item.Quantity), Gross = rows.Sum(x => x.item.Subtotal + x.item.TaxAmount - x.item.DiscountAmount), Cost = rows.Sum(x => x.item.CostPrice * x.item.Quantity) };
            var returned = from line in _context.SaleReturnLines.AsNoTracking()
                           join ret in PostedReturns(f) on line.SaleReturnId equals ret.Id
                           join item in _context.SaleItems.AsNoTracking() on line.SaleItemId equals item.Id
                           group new { line, item } by item.ProductId into rows
                           select new { ProductId = rows.Key, Quantity = rows.Sum(x => x.line.Quantity), Refund = rows.Sum(x => x.line.RefundAmount), Cost = rows.Sum(x => x.item.CostPrice * x.line.Quantity) };
            return (from row in sold
                    join ret in returned on row.Key.ProductId equals ret.ProductId into returnRows
                    from ret in returnRows.DefaultIfEmpty()
                    orderby row.Gross descending
                    select new ItemSalesReportRowDTO
                    {
                        ProductId = row.Key.ProductId, Sku = row.Key.Sku, ProductName = row.Key.ProductName, CategoryName = row.Key.CategoryName,
                        QuantitySold = row.Quantity, QuantityReturned = ret == null ? 0 : ret.Quantity,
                        GrossSales = row.Gross, Refunds = ret == null ? 0m : ret.Refund,
                        EstimatedCost = row.Cost - (ret == null ? 0m : ret.Cost),
                        EstimatedMargin = row.Gross - (ret == null ? 0m : ret.Refund) - row.Cost + (ret == null ? 0m : ret.Cost)
                    }).Take(maximumRows).ToList();
        }

        private IReadOnlyList<TenderReportRowDTO> GetTenders(ManagementFilterDTO f)
        {
            var payments = from payment in _context.Payments.AsNoTracking()
                           join sale in CompletedSales(f) on payment.SaleId equals sale.Id
                           where payment.Status == PaymentStatus.Completed
                           group payment by payment.TenderType into rows
                           select new { Type = rows.Key, Count = rows.Count(), Collected = rows.Sum(x => x.Amount) };
            var refunds = from refund in _context.RefundPayments.AsNoTracking()
                          join ret in PostedReturns(f) on refund.SaleReturnId equals ret.Id
                          group refund by refund.TenderType into rows
                          select new { Type = rows.Key, Amount = rows.Sum(x => x.Amount) };
            return (from payment in payments
                    join refund in refunds on payment.Type equals refund.Type into refundRows
                    from refund in refundRows.DefaultIfEmpty()
                    select new { payment.Type, payment.Count, payment.Collected, Refunded = refund == null ? 0m : refund.Amount }).ToList()
                    .Select(x => new TenderReportRowDTO { TenderType = x.Type.ToString(), PaymentCount = x.Count, Collected = x.Collected, Refunded = x.Refunded, NetCollected = x.Collected - x.Refunded })
                    .OrderBy(x => x.TenderType).ToList();
        }

        private IReadOnlyList<InventoryReportRowDTO> GetInventory(ManagementFilterDTO f, int maximumRows)
        {
            var query = from balance in _context.InventoryBalances.AsNoTracking()
                        join product in _context.Set<POS.Domains.BusinessObjects.Product>().AsNoTracking() on balance.ProductId equals product.Id
                        join category in _context.Set<POS.Domains.BusinessObjects.Category>().AsNoTracking() on product.CategoryId equals category.Id
                        where (f.IncludeInactiveOrCancelled || product.IsActive) && (!f.ProductId.HasValue || product.Id == f.ProductId.Value) && (!f.CategoryId.HasValue || product.CategoryId == f.CategoryId.Value)
                        orderby product.Name
                        select new InventoryReportRowDTO { ProductId = product.Id, Sku = product.Sku, ProductName = product.Name, CategoryName = category.Name, QuantityOnHand = balance.QuantityOnHand, AlertThreshold = product.BuyingThreshold, UnitCost = product.CostPrice, Valuation = product.CostPrice * balance.QuantityOnHand, ExpiryDate = product.ExpiryDate, IsActive = product.IsActive };
            return query.Take(maximumRows).ToList();
        }

        private IReadOnlyList<MovementReportRowDTO> GetMovements(ManagementFilterDTO f)
        {
            var query = from movement in _context.StockMovements.AsNoTracking()
                        join product in _context.Set<POS.Domains.BusinessObjects.Product>().AsNoTracking() on movement.ProductId equals product.Id
                        where movement.CreatedUtc >= f.FromUtc && movement.CreatedUtc < f.ToUtcExclusive && (!f.ProductId.HasValue || movement.ProductId == f.ProductId.Value) && (string.IsNullOrEmpty(f.UserId) || movement.UserId == f.UserId)
                        orderby movement.CreatedUtc descending, movement.Id descending
                        select new { movement, product.Name };
            return query.Take(f.MaximumRows).ToList().Select(x => new MovementReportRowDTO { MovementId = x.movement.Id, CreatedUtc = x.movement.CreatedUtc, ProductId = x.movement.ProductId, ProductName = x.Name, MovementType = x.movement.MovementType.ToString(), QuantityDelta = x.movement.QuantityDelta, ReferenceType = x.movement.ReferenceType, ReferenceId = x.movement.ReferenceId, UserId = x.movement.UserId, Reason = x.movement.Reason }).ToList();
        }

        private IReadOnlyList<PurchasingReportRowDTO> GetPurchasing(ManagementFilterDTO f)
        {
            var receipts = from receipt in _context.GoodsReceipts.AsNoTracking()
                           join line in _context.GoodsReceiptLines.AsNoTracking() on receipt.Id equals line.GoodsReceiptId
                           where receipt.ReceivedUtc >= f.FromUtc && receipt.ReceivedUtc < f.ToUtcExclusive && (f.IncludeInactiveOrCancelled || receipt.Status == DocumentStatus.Posted || receipt.Status == DocumentStatus.Completed || receipt.Status == DocumentStatus.PartiallyReturned || receipt.Status == DocumentStatus.Returned) && (!f.SupplierId.HasValue || receipt.SupplierId == f.SupplierId.Value)
                           group line by receipt.SupplierId into rows select new { SupplierId = rows.Key, Count = rows.Select(x => x.GoodsReceiptId).Distinct().Count(), Value = rows.Sum(x => x.UnitCost * x.Quantity) };
            var returns = from ret in _context.PurchaseReturns.AsNoTracking()
                          where ret.ReturnedUtc >= f.FromUtc && ret.ReturnedUtc < f.ToUtcExclusive && (ret.Status == DocumentStatus.Posted || ret.Status == DocumentStatus.Completed) && (!f.SupplierId.HasValue || ret.SupplierId == f.SupplierId.Value)
                          group ret by ret.SupplierId into rows select new { SupplierId = rows.Key, Value = rows.Sum(x => x.TotalAmount) };
            return (from receipt in receipts join supplier in _context.Suppliers.AsNoTracking() on receipt.SupplierId equals supplier.Id join ret in returns on receipt.SupplierId equals ret.SupplierId into returnRows from ret in returnRows.DefaultIfEmpty() orderby supplier.Name select new PurchasingReportRowDTO
            { SupplierId = supplier.Id, SupplierCode = supplier.Code, SupplierName = supplier.Name, ReceiptCount = receipt.Count, ReceivedValue = receipt.Value, ReturnedValue = ret == null ? 0m : ret.Value, NetPurchased = receipt.Value - (ret == null ? 0m : ret.Value) }).Take(f.MaximumRows).ToList();
        }

        private IReadOnlyList<ShiftReportRowDTO> GetShifts(ManagementFilterDTO f)
        {
            var query = from shift in _context.CashierShifts.AsNoTracking() join register in _context.RegisterStations.AsNoTracking() on shift.RegisterStationId equals register.Id where shift.OpenedUtc < f.ToUtcExclusive && (shift.ClosedUtc == null || shift.ClosedUtc >= f.FromUtc) &&
                        (!f.RegisterStationId.HasValue || shift.RegisterStationId == f.RegisterStationId.Value) && (string.IsNullOrEmpty(f.UserId) || shift.CashierUserId == f.UserId) orderby shift.OpenedUtc descending select new { shift, register.Code };
            return query.Take(f.MaximumRows).ToList().Select(x => new ShiftReportRowDTO { ShiftId = x.shift.Id, RegisterStationId = x.shift.RegisterStationId, RegisterCode = x.Code, CashierUserId = x.shift.CashierUserId, OpenedUtc = x.shift.OpenedUtc, ClosedUtc = x.shift.ClosedUtc, Status = x.shift.Status, OpeningCash = x.shift.OpeningCash, ExpectedCash = x.shift.ExpectedCash, CountedCash = x.shift.CountedCash, Variance = x.shift.Variance }).ToList();
        }

        private IReadOnlyList<AuditActivityReportRowDTO> GetAuditActivity(ManagementFilterDTO f)
        {
            var query = _context.AuditLogs.AsNoTracking().Where(x => x.DateLogged >= f.FromUtc && x.DateLogged < f.ToUtcExclusive);
            if (f.RegisterStationId.HasValue) query = query.Where(x => x.RegisterStationId == f.RegisterStationId.Value);
            if (!string.IsNullOrEmpty(f.UserId)) query = query.Where(x => x.UserId == f.UserId);
            return query.GroupBy(x => new { Day = DbFunctions.TruncateTime(x.DateLogged), x.TableName, x.Action }).Select(x => new { x.Key.Day, x.Key.TableName, x.Key.Action, Count = x.Count() }).OrderByDescending(x => x.Day).Take(f.MaximumRows).ToList().Select(x => new AuditActivityReportRowDTO { DayUtc = x.Day ?? f.FromUtc.Date, Entity = x.TableName, Action = x.Action, EventCount = x.Count }).ToList();
        }

        private IQueryable<POS.Domains.BusinessObjects.Sale> ScopedSales(ManagementFilterDTO f)
        {
            var query = _context.Sales.AsNoTracking().Where(x => x.SaleDate >= f.FromUtc && x.SaleDate < f.ToUtcExclusive);
            if (!f.IncludeInactiveOrCancelled) query = query.Where(x => x.Status == DocumentStatus.Completed || x.Status == DocumentStatus.PartiallyReturned || x.Status == DocumentStatus.Returned);
            if (f.RegisterStationId.HasValue) query = query.Where(x => x.RegisterStationId == f.RegisterStationId.Value);
            if (!string.IsNullOrEmpty(f.UserId)) query = query.Where(x => x.CashierUserId == f.UserId);
            if (f.CustomerId.HasValue) query = query.Where(x => x.CustomerId == f.CustomerId.Value);
            return query;
        }

        private IQueryable<POS.Domains.BusinessObjects.Sale> CompletedSales(ManagementFilterDTO f) => ScopedSales(f).Where(x => x.Status == DocumentStatus.Completed || x.Status == DocumentStatus.PartiallyReturned || x.Status == DocumentStatus.Returned);
        private IQueryable<POS.Domains.Operations.SaleReturn> PostedReturns(ManagementFilterDTO f)
        {
            var query = _context.SaleReturns.AsNoTracking().Where(x => x.CreatedUtc >= f.FromUtc && x.CreatedUtc < f.ToUtcExclusive && (x.Status == DocumentStatus.Posted || x.Status == DocumentStatus.Completed));
            if (f.RegisterStationId.HasValue) query = query.Where(x => x.RegisterStationId == f.RegisterStationId.Value);
            if (!string.IsNullOrEmpty(f.UserId)) query = query.Where(x => x.CreatedByUserId == f.UserId);
            if (f.CustomerId.HasValue) query = query.Where(x => x.Sale.CustomerId == f.CustomerId.Value);
            return query;
        }

        private static ManagementFilterDTO Validate(ManagementFilterDTO source, int defaultRows)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (source.FromUtc.Kind == DateTimeKind.Local || source.ToUtcExclusive.Kind == DateTimeKind.Local) throw new ValidationException("Report dates must be UTC.");
            if (source.FromUtc >= source.ToUtcExclusive) throw new ValidationException("The report end must be after its start.");
            if (source.ToUtcExclusive - source.FromUtc > TimeSpan.FromDays(366)) throw new ValidationException("A report range cannot exceed 366 days.");
            if (source.RegisterStationId <= 0 || source.CategoryId <= 0 || source.ProductId <= 0 || source.SupplierId <= 0 || source.CustomerId <= 0) throw new ValidationException("Filter identifiers must be positive.");
            if ((source.UserId?.Length ?? 0) > 128) throw new ValidationException("The user filter is too long.");
            return new ManagementFilterDTO { FromUtc = source.FromUtc, ToUtcExclusive = source.ToUtcExclusive, RegisterStationId = source.RegisterStationId, UserId = source.UserId?.Trim() ?? string.Empty, CategoryId = source.CategoryId,
                ProductId = source.ProductId, SupplierId = source.SupplierId, CustomerId = source.CustomerId, IncludeInactiveOrCancelled = source.IncludeInactiveOrCancelled, MaximumRows = source.MaximumRows <= 0 ? defaultRows : Math.Min(2000, source.MaximumRows) };
        }

        public void Dispose() { if (_ownsContext) _context.Dispose(); }
    }
}
