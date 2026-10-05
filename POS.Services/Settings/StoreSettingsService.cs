using POS.Data.Context;
using POS.Core.Abstractions;
using POS.Core.Security;
using POS.Common.Enumerations;
using POS.Services.Security;
using POS.Domains.Operations;
using POS.Models.Operations;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data.Entity;
using System.Linq;
using System.Globalization;
using System.Text;
using System.IO;
using System.Security.Cryptography;
using POS.Services.Point_of_Sale;

namespace POS.Services.Settings
{
    public class StoreSettingsService : IDisposable
    {
        private readonly POSContext _context;
        private readonly bool _ownsContext;
        private readonly IClock _clock;
        private readonly IAuthorizationService _authorization;
        private readonly IReceiptPrinter _printer;
        private readonly NumberSequenceService _sequences;

        public StoreSettingsService() : this(new POSContext(), new SystemClock(), new ClaimsAuthorizationService(new CurrentUserAccessor()), new WindowsReceiptPrinter(), true) { }

        public StoreSettingsService(POSContext context) : this(context, new SystemClock(), new ClaimsAuthorizationService(new CurrentUserAccessor()), new WindowsReceiptPrinter(), false) { }
        public StoreSettingsService(POSContext context, IClock clock) : this(context, clock, new ClaimsAuthorizationService(new CurrentUserAccessor()), new WindowsReceiptPrinter(), false) { }
        public StoreSettingsService(POSContext context, IClock clock, IAuthorizationService authorization) : this(context, clock, authorization, new WindowsReceiptPrinter(), false) { }
        public StoreSettingsService(POSContext context, IClock clock, IAuthorizationService authorization, IReceiptPrinter printer) : this(context, clock, authorization, printer, false) { }

        private StoreSettingsService(POSContext context, IClock clock, IAuthorizationService authorization, IReceiptPrinter printer, bool ownsContext)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
            _printer = printer ?? throw new ArgumentNullException(nameof(printer));
            _sequences = new NumberSequenceService(_context, _authorization);
            _ownsContext = ownsContext;
        }

        public StoreSettingsDTO GetSettings()
        {
            Demand(ClaimActionType.View);
            return ReadSettings();
        }

        private StoreSettingsDTO ReadSettings()
        {
            var stores = _context.StoreSettings.AsNoTracking().Take(2).ToList();
            if (stores.Count > 1)
                throw new InvalidOperationException("Multiple store configurations exist. Exactly one is allowed.");
            var store = stores.SingleOrDefault();
            var tax = store == null ? null : _context.TaxRates.AsNoTracking().FirstOrDefault(x => x.Id == store.DefaultTaxRateId);
            var register = _context.RegisterStations.AsNoTracking().OrderBy(x => x.Id).FirstOrDefault();
            var receiptSequence = _context.NumberSequences.AsNoTracking()
                .SingleOrDefault(x => x.DocumentType == NumberSequenceService.SaleReceipt);

            return new StoreSettingsDTO
            {
                Revision = GetRevision(store, tax, register, receiptSequence),
                StoreSettingId = store?.Id ?? 0,
                StoreName = store?.StoreName ?? "My Store",
                Address = store?.Address,
                Phone = store?.Phone,
                Email = store?.Email,
                TaxIdentifier = store?.TaxIdentifier,
                CurrencyCode = store?.CurrencyCode ?? "PHP",
                MoneyDecimalPlaces = store?.MoneyDecimalPlaces ?? 2,
                TimeZoneId = store?.TimeZoneId ?? TimeZoneInfo.Local.Id,
                ReceiptFooter = store?.ReceiptFooter ?? "Thank you for your purchase.",
                AllowNegativeStock = store?.AllowNegativeStock ?? false,
                TaxRateId = tax?.Id ?? 0,
                TaxName = tax?.Name ?? "VAT",
                TaxRate = tax?.Rate ?? 0.12m,
                TaxInclusive = tax?.IsInclusive ?? false,
                RegisterStationId = register?.Id ?? 0,
                RegisterCode = register?.Code ?? "REG-01",
                RegisterName = register?.Name ?? "Main Register",
                PrinterName = register?.PrinterName,
                ReceiptPrefix = receiptSequence?.Prefix ?? "R",
                NextReceiptNumber = receiptSequence?.NextNumber ?? 1
            };
        }

        public void SaveSettings(StoreSettingsDTO dto)
        {
            using (var auditOperation = POS.Core.Abstractions.AuditOperation.Begin())
            {
                Demand(ClaimActionType.Edit);
                StoreSettingsValidator.ValidateAndThrow(dto);

                // This operation performs intermediate saves to establish tax IDs. Do not
                // include another caller's pending changes in those saves or in recovery.
                if (_context.Database.CurrentTransaction != null || _context.ChangeTracker.HasChanges())
                    throw new InvalidOperationException("Save store settings using a context with no pending changes or active transaction.");
                ClearTrackedSettingsState();

                using (var transaction = _context.Database.BeginTransaction(System.Data.IsolationLevel.Serializable))
                {
                    try
                    {
                        // Serialize settings writers, including first-time creation. The
                        // serializable reads below hold related configuration until commit.
                        _context.Database.SqlQuery<int>(
                            "SELECT Id FROM dbo.StoreSettings WITH (UPDLOCK, HOLDLOCK)").ToList();
                        var currentSettings = ReadSettings();
                        if (string.IsNullOrEmpty(dto.Revision) ||
                            !string.Equals(dto.Revision, currentSettings.Revision, StringComparison.Ordinal))
                            throw new ValidationException("Settings have changed or were not loaded before editing. Reload settings before saving.");
                        if (dto.StoreSettingId != currentSettings.StoreSettingId ||
                            dto.RegisterStationId != currentSettings.RegisterStationId ||
                            dto.TaxRateId != currentSettings.TaxRateId)
                            throw new ValidationException("The selected store, register, or tax no longer matches the loaded settings. Reload settings before saving.");
                        var store = dto.StoreSettingId == 0 ? new StoreSetting() : _context.StoreSettings.Find(dto.StoreSettingId);
                        if (store == null) throw new InvalidOperationException("The store settings no longer exist.");
                        if (dto.StoreSettingId == 0 && _context.StoreSettings.Any())
                            throw new InvalidOperationException("Store settings already exist. Reload before saving.");
                        if (store.Id != 0 && store.DefaultTaxRateId != dto.TaxRateId)
                            throw new ValidationException("The default tax has changed since these settings were loaded. Reload settings before saving.");

                        var currentTax = dto.TaxRateId == 0 ? null : _context.TaxRates.Find(dto.TaxRateId);
                        if (dto.TaxRateId != 0 && currentTax == null)
                            throw new InvalidOperationException("The selected tax rate no longer exists.");

                        var taxChanged = currentTax == null ||
                            !currentTax.IsActive || currentTax.EffectiveToUtc.HasValue ||
                            currentTax.Name != dto.TaxName.Trim() ||
                            currentTax.Rate != dto.TaxRate ||
                            currentTax.IsInclusive != dto.TaxInclusive;
                        var tax = currentTax;
                        if (taxChanged)
                        {
                            if (currentTax != null)
                            {
                                currentTax.IsActive = false;
                                currentTax.EffectiveToUtc = _clock.UtcNow;
                            }
                            tax = new TaxRate
                            {
                                Name = dto.TaxName.Trim(),
                                Rate = dto.TaxRate,
                                IsInclusive = dto.TaxInclusive,
                                IsActive = true,
                                EffectiveFromUtc = _clock.UtcNow
                            };
                            _context.TaxRates.Add(tax);
                            _context.SaveChanges();
                        }

                        var register = dto.RegisterStationId == 0 ? new RegisterStation() : _context.RegisterStations.Find(dto.RegisterStationId);
                        if (register == null) throw new InvalidOperationException("The selected register no longer exists.");
                        register.Code = dto.RegisterCode.Trim().ToUpperInvariant();
                        register.Name = dto.RegisterName.Trim();
                        register.PrinterName = NullIfWhiteSpace(dto.PrinterName);
                        register.IsActive = true;
                        if (register.Id == 0) _context.RegisterStations.Add(register);

                        store.StoreName = dto.StoreName.Trim();
                        store.Address = NullIfWhiteSpace(dto.Address);
                        store.Phone = NullIfWhiteSpace(dto.Phone);
                        store.Email = NullIfWhiteSpace(dto.Email);
                        store.TaxIdentifier = NullIfWhiteSpace(dto.TaxIdentifier);
                        store.CurrencyCode = dto.CurrencyCode.Trim().ToUpperInvariant();
                        store.MoneyDecimalPlaces = dto.MoneyDecimalPlaces;
                        store.TimeZoneId = dto.TimeZoneId.Trim();
                        store.ReceiptFooter = NullIfWhiteSpace(dto.ReceiptFooter);
                        store.AllowNegativeStock = dto.AllowNegativeStock;
                        store.DefaultTaxRateId = tax.Id;
                        if (store.Id == 0) _context.StoreSettings.Add(store);

                        var sequenceResult = _sequences.ConfigureReceipt(
                            dto.ReceiptPrefix,
                            dto.NextReceiptNumber);
                        if (!sequenceResult.Succeeded)
                            throw new ValidationException(string.Join(Environment.NewLine, sequenceResult.Errors));

                        _context.SaveChanges();
                        transaction.Commit();
                    }
                    catch
                    {
                        try { transaction.Rollback(); }
                        finally
                        {
                            // SaveChanges marks entries Unchanged before the surrounding
                            // transaction commits. Clear those entries as well as pending
                            // changes, including generated audit rows and rolled-back IDs.
                            ClearTrackedSettingsState();
                        }
                        throw;
                    }
                }
            }
        }

        private void ClearTrackedSettingsState()
        {
            foreach (var entry in _context.ChangeTracker.Entries().ToList())
                entry.State = EntityState.Detached;
        }

        private static string GetRevision(StoreSetting store, TaxRate tax, RegisterStation register, NumberSequence sequence)
        {
            // Explicit fields and length-prefixed encoding keep nulls and embedded
            // separators unambiguous. This token is concurrency metadata, not a secret.
            object[] values =
            {
                store?.Id, store?.StoreName, store?.Address, store?.Phone, store?.Email,
                store?.TaxIdentifier, store?.CurrencyCode, store?.MoneyDecimalPlaces,
                store?.TimeZoneId, store?.ReceiptFooter, store?.DefaultTaxRateId, store?.AllowNegativeStock,
                tax?.Id, tax?.Name, tax?.Rate, tax?.IsInclusive, tax?.IsActive,
                tax?.EffectiveFromUtc.Ticks, tax?.EffectiveToUtc?.Ticks,
                register?.Id, register?.Code, register?.Name, register?.PrinterName, register?.IsActive,
                sequence?.Id, sequence?.Prefix, sequence?.NextNumber
            };
            using (var buffer = new MemoryStream())
            {
                using (var writer = new BinaryWriter(buffer, Encoding.UTF8, true))
                {
                    foreach (var value in values)
                    {
                        writer.Write(value != null);
                        if (value != null) writer.Write(Convert.ToString(value, CultureInfo.InvariantCulture));
                    }
                }
                using (var hash = SHA256.Create())
                    return Convert.ToBase64String(hash.ComputeHash(buffer.ToArray()));
            }
        }

        public IReadOnlyList<TaxRateHistoryDTO> GetTaxHistory()
        {
            Demand(ClaimActionType.View);
            return _context.TaxRates.AsNoTracking()
                .OrderByDescending(x => x.EffectiveFromUtc)
                .ThenByDescending(x => x.Id)
                .Select(x => new TaxRateHistoryDTO
                {
                    TaxRateId = x.Id,
                    Name = x.Name,
                    Rate = x.Rate,
                    IsInclusive = x.IsInclusive,
                    IsActive = x.IsActive,
                    EffectiveFromUtc = x.EffectiveFromUtc,
                    EffectiveToUtc = x.EffectiveToUtc
                })
                .ToList();
        }

        public IReadOnlyList<string> GetInstalledPrinters()
        {
            Demand(ClaimActionType.View);
            return _printer.GetInstalledPrinters();
        }

        public string BuildReceiptPreview(StoreSettingsDTO settings)
        {
            Demand(ClaimActionType.View);
            StoreSettingsValidator.ValidateAndThrow(settings);
            var calculator = new SalesCalculator();
            var amount = calculator.RoundMoney(100m, settings.MoneyDecimalPlaces);
            var tax = calculator.CalculateTax(amount, settings.TaxRate,
                settings.TaxInclusive, settings.MoneyDecimalPlaces);
            var subtotal = settings.TaxInclusive ? amount - tax : amount;
            var total = calculator.RoundMoney(subtotal + tax, settings.MoneyDecimalPlaces);
            var currency = settings.CurrencyCode.Trim().ToUpperInvariant();
            Func<decimal, string> money = value => currency + " " +
                value.ToString("N" + settings.MoneyDecimalPlaces, CultureInfo.InvariantCulture);
            var receipt = new StringBuilder();
            receipt.AppendLine("SAMPLE - NOT A SALE");
            receipt.AppendLine(settings.StoreName.Trim());
            if (!string.IsNullOrWhiteSpace(settings.Address)) receipt.AppendLine(settings.Address.Trim());
            if (!string.IsNullOrWhiteSpace(settings.Phone)) receipt.AppendLine(settings.Phone.Trim());
            if (!string.IsNullOrWhiteSpace(settings.Email)) receipt.AppendLine(settings.Email.Trim());
            if (!string.IsNullOrWhiteSpace(settings.TaxIdentifier))
                receipt.AppendLine("Tax ID: " + settings.TaxIdentifier.Trim());
            receipt.AppendLine();
            receipt.AppendLine("Register: " + settings.RegisterName.Trim());
            receipt.AppendLine("Code: " + settings.RegisterCode.Trim().ToUpperInvariant());
            receipt.AppendLine("Receipt format: " + settings.ReceiptPrefix.Trim().ToUpperInvariant() +
                settings.NextReceiptNumber.ToString("D8", CultureInfo.InvariantCulture));
            receipt.AppendLine("Cashier: Sample cashier");
            receipt.AppendLine();
            receipt.AppendLine("Sample item");
            receipt.AppendLine("1 x " + money(amount));
            receipt.AppendLine(settings.TaxInclusive ? "Price includes tax" : "Tax added to price");
            receipt.AppendLine();
            receipt.AppendLine("Subtotal: " + money(subtotal));
            receipt.AppendLine(settings.TaxName.Trim() + " (" +
                settings.TaxRate.ToString("P2", CultureInfo.InvariantCulture) + "): " + money(tax));
            receipt.AppendLine("Discount: " + money(0m));
            receipt.AppendLine("TOTAL: " + money(total));
            receipt.AppendLine("Cash: " + money(total));
            receipt.AppendLine("Change: " + money(0m));
            receipt.AppendLine();
            if (!string.IsNullOrWhiteSpace(settings.ReceiptFooter)) receipt.AppendLine(settings.ReceiptFooter.Trim());
            receipt.AppendLine("SAMPLE - NOT A SALE");
            return receipt.ToString();
        }

        public POS.Core.Results.OperationResult PrintTestPage(StoreSettingsDTO settings)
        {
            Demand(ClaimActionType.Edit);
            if (settings == null)
                return POS.Core.Results.OperationResult.Failure("Settings are required.");
            return _printer.PrintTestPage(
                settings.PrinterName,
                settings.StoreName,
                settings.RegisterName);
        }

        private static string NullIfWhiteSpace(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private void Demand(ClaimActionType action)
        {
            PermissionGuard.Demand(_authorization, ResourceCodes.Settings, action);
        }

        public void Dispose()
        {
            _sequences.Dispose();
            if (_ownsContext) _context.Dispose();
        }
    }

    public static class StoreSettingsValidator
    {
        public static IReadOnlyList<ValidationResult> Validate(StoreSettingsDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            var results = new List<ValidationResult>();
            Validator.TryValidateObject(dto, new ValidationContext(dto), results, true);
            if (!string.IsNullOrWhiteSpace(dto.TimeZoneId))
            {
                try { TimeZoneInfo.FindSystemTimeZoneById(dto.TimeZoneId.Trim()); }
                catch (TimeZoneNotFoundException)
                {
                    results.Add(new ValidationResult("Select a time zone installed on this computer.",
                        new[] { nameof(dto.TimeZoneId) }));
                }
                catch (InvalidTimeZoneException)
                {
                    results.Add(new ValidationResult("The selected time zone is unavailable or damaged. Select another time zone.",
                        new[] { nameof(dto.TimeZoneId) }));
                }
            }
            if (!string.IsNullOrWhiteSpace(dto.CurrencyCode) &&
                !string.Equals(dto.CurrencyCode.Trim(), "PHP", StringComparison.OrdinalIgnoreCase))
                results.Add(new ValidationResult("Currency must be Philippine peso (PHP).",
                    new[] { nameof(dto.CurrencyCode) }));
            return results;
        }

        public static void ValidateAndThrow(StoreSettingsDTO dto)
        {
            var results = Validate(dto);
            if (results.Count > 0)
                throw new ValidationException(string.Join(Environment.NewLine, results.Select(x => x.ErrorMessage)));
        }
    }
}
