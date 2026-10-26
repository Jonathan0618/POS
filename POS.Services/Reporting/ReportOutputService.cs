using POS.Common.Enumerations;
using POS.Core.Abstractions;
using POS.Core.Security;
using POS.Data.Context;
using POS.Models.Operations;
using POS.Services.Security;
using POS.Services.Settings;
using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace POS.Services.Reporting
{
    public sealed class ReportOutputService : IDisposable
    {
        private readonly POSContext _context;
        private readonly IAuthorizationService _authorization;
        private readonly IClock _clock;
        private readonly IReceiptPrinter _printer;
        private readonly bool _ownsContext;

        public ReportOutputService() : this(new POSContext(), new ClaimsAuthorizationService(new CurrentUserAccessor()), new SystemClock(), new WindowsReceiptPrinter(), true) { }
        public ReportOutputService(POSContext context, IAuthorizationService authorization, IClock clock, IReceiptPrinter printer)
            : this(context, authorization, clock, printer, false) { }

        private ReportOutputService(POSContext context, IAuthorizationService authorization, IClock clock, IReceiptPrinter printer, bool ownsContext)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _printer = printer ?? throw new ArgumentNullException(nameof(printer));
            _ownsContext = ownsContext;
        }

        public ReportExportDTO ExportCsv(ManagementFilterDTO filter)
        {
            PermissionGuard.Demand(_authorization, ResourceCodes.Reports, ClaimActionType.Add);
            ManagementReportDTO report;
            using (var service = new ManagementReportService(_context, _authorization, _clock)) report = service.GetReport(filter);
            var csv = new StringBuilder();
            csv.AppendLine("Report,POS Management Report");
            csv.AppendLine("Generated UTC," + Csv(report.GeneratedUtc.ToString("O", CultureInfo.InvariantCulture)));
            csv.AppendLine("From UTC," + Csv(report.Filter.FromUtc.ToString("O", CultureInfo.InvariantCulture)));
            csv.AppendLine("To UTC Exclusive," + Csv(report.Filter.ToUtcExclusive.ToString("O", CultureInfo.InvariantCulture)));
            csv.AppendLine("Register ID," + Csv(report.Filter.RegisterStationId));
            csv.AppendLine();
            csv.AppendLine("SUMMARY,Gross Sales,Refunds,Net Sales,Tax,Discounts,Estimated Cost,Estimated Margin,Transactions,Returns");
            csv.AppendLine("SUMMARY," + Join(report.Financials.GrossSales, report.Financials.Refunds, report.Financials.NetSales, report.Financials.Tax, report.Financials.Discounts, report.Financials.EstimatedCost, report.Financials.EstimatedMargin, report.Financials.TransactionCount, report.Financials.ReturnCount));
            csv.AppendLine();
            csv.AppendLine("SALES,Sale ID,UTC,Receipt,Status,Register,Cashier,Customer,Subtotal,Discount,Tax,Total,Refunded,Net");
            foreach (var x in report.Sales) csv.AppendLine("SALES," + Join(x.SaleId, x.SaleUtc.ToString("O"), x.ReceiptNumber, x.Status, x.RegisterCode, x.CashierName, x.CustomerName, x.Subtotal, x.Discount, x.Tax, x.Total, x.Refunded, x.NetTotal));
            csv.AppendLine();
            csv.AppendLine("ITEMS,Product ID,SKU,Product,Category,Sold,Returned,Gross,Refunds,Estimated Cost,Estimated Margin");
            foreach (var x in report.Items) csv.AppendLine("ITEMS," + Join(x.ProductId, x.Sku, x.ProductName, x.CategoryName, x.QuantitySold, x.QuantityReturned, x.GrossSales, x.Refunds, x.EstimatedCost, x.EstimatedMargin));
            csv.AppendLine();
            csv.AppendLine("TENDERS,Type,Payment Count,Collected,Refunded,Net Collected");
            foreach (var x in report.Tenders) csv.AppendLine("TENDERS," + Join(x.TenderType, x.PaymentCount, x.Collected, x.Refunded, x.NetCollected));
            csv.AppendLine();
            csv.AppendLine("INVENTORY,Product ID,SKU,Product,Category,On Hand,Threshold,Unit Cost,Valuation,Expiry UTC,Active");
            foreach (var x in report.Inventory) csv.AppendLine("INVENTORY," + Join(x.ProductId, x.Sku, x.ProductName, x.CategoryName, x.QuantityOnHand, x.AlertThreshold, x.UnitCost, x.Valuation, x.ExpiryDate?.ToString("O"), x.IsActive));
            csv.AppendLine();
            csv.AppendLine("MOVEMENTS,Movement ID,UTC,Product ID,Product,Type,Delta,Reference Type,Reference ID,User ID,Reason");
            foreach (var x in report.Movements) csv.AppendLine("MOVEMENTS," + Join(x.MovementId, x.CreatedUtc.ToString("O"), x.ProductId, x.ProductName, x.MovementType, x.QuantityDelta, x.ReferenceType, x.ReferenceId, x.UserId, x.Reason));
            csv.AppendLine();
            csv.AppendLine("PURCHASING,Supplier ID,Code,Supplier,Receipt Count,Received Value,Returned Value,Net Purchased");
            foreach (var x in report.Purchasing) csv.AppendLine("PURCHASING," + Join(x.SupplierId, x.SupplierCode, x.SupplierName, x.ReceiptCount, x.ReceivedValue, x.ReturnedValue, x.NetPurchased));
            csv.AppendLine();
            csv.AppendLine("SHIFTS,Shift ID,Register ID,Register,Cashier ID,Opened UTC,Closed UTC,Status,Opening,Expected,Counted,Variance");
            foreach (var x in report.Shifts) csv.AppendLine("SHIFTS," + Join(x.ShiftId, x.RegisterStationId, x.RegisterCode, x.CashierUserId, x.OpenedUtc.ToString("O"), x.ClosedUtc?.ToString("O"), x.Status, x.OpeningCash, x.ExpectedCash, x.CountedCash, x.Variance));
            csv.AppendLine();
            csv.AppendLine("AUDIT,Day UTC,Entity,Action,Event Count");
            foreach (var x in report.AuditActivity) csv.AppendLine("AUDIT," + Join(x.DayUtc.ToString("yyyy-MM-dd"), x.Entity, x.Action, x.EventCount));
            var utf8 = new UTF8Encoding(true);
            return new ReportExportDTO { FileName = "pos-management-" + report.GeneratedUtc.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".csv", ContentType = "text/csv; charset=utf-8", Content = utf8.GetBytes(csv.ToString()), GeneratedUtc = report.GeneratedUtc };
        }

        public void PrintSummary(ManagementFilterDTO filter, int registerStationId)
        {
            PermissionGuard.Demand(_authorization, ResourceCodes.Reports, ClaimActionType.Add);
            if (registerStationId <= 0) throw new ArgumentOutOfRangeException(nameof(registerStationId));
            DashboardDTO dashboard;
            using (var service = new ManagementReportService(_context, _authorization, _clock))
            {
                PermissionGuard.Demand(_authorization, ResourceCodes.Dashboard, ClaimActionType.View);
                dashboard = service.GetDashboard(filter);
            }
            var register = _context.RegisterStations.AsNoTracking().SingleOrDefault(x => x.Id == registerStationId && x.IsActive);
            if (register == null || string.IsNullOrWhiteSpace(register.PrinterName)) throw new InvalidOperationException("The selected register has no active configured printer.");
            var f = dashboard.Financials;
            var content = string.Join(Environment.NewLine, new[] { "POS MANAGEMENT SUMMARY", "Generated UTC: " + dashboard.GeneratedUtc.ToString("u"), "Period: " + dashboard.Filter.FromUtc.ToString("u") + " to " + dashboard.Filter.ToUtcExclusive.ToString("u") + " (exclusive)", "Gross sales: " + f.GrossSales.ToString("N2"), "Refunds: " + f.Refunds.ToString("N2"), "Net sales: " + f.NetSales.ToString("N2"), "Tax: " + f.Tax.ToString("N2"), "Discounts: " + f.Discounts.ToString("N2"), "Estimated margin: " + f.EstimatedMargin.ToString("N2"), "Transactions: " + f.TransactionCount, "Returns: " + f.ReturnCount });
            var result = _printer.PrintReceipt(register.PrinterName, "POS Management Summary", content);
            if (!result.Succeeded) throw new InvalidOperationException(string.Join(" ", result.Errors));
        }

        private static string Join(params object[] values) => string.Join(",", values.Select(Csv));
        private static string Csv(object value)
        {
            if (value == null) return string.Empty;
            var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            if (text.Length > 0 && "=+-@".IndexOf(text[0]) >= 0) text = "'" + text;
            return "\"" + text.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ") + "\"";
        }

        public void Dispose() { if (_ownsContext) _context.Dispose(); }
    }
}
