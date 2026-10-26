using POS.Models.Operations;
using POS.Services.Settings;
using POS.Core.Abstractions;
using POS.Core.Results;
using POS.Common.Enumerations;
using POS.Data.Context;
using System;
using System.Collections.Generic;

namespace POS.Tests
{
    internal static class StoreSettingsValidatorTests
    {
        public static void RunAll()
        {
            ValidSettingsPass();
            InvalidRateAndRequiredFieldsFail();
            PrinterDiscoveryAndTestingUseInjectedAdapter();
            ReceiptPreviewUsesCurrentTaxAndCurrencyWithoutPrinting();
        }

        private static void ValidSettingsPass()
        {
            var result = StoreSettingsValidator.Validate(CreateValid());
            if (result.Count != 0) throw new InvalidOperationException("Valid store settings were rejected.");
        }

        private static void InvalidRateAndRequiredFieldsFail()
        {
            var dto = CreateValid();
            dto.StoreName = "";
            if (StoreSettingsValidator.Validate(dto).Count == 0)
                throw new InvalidOperationException("Missing store name was not rejected.");

            dto = CreateValid();
            dto.TaxRate = 1.1m;
            if (StoreSettingsValidator.Validate(dto).Count == 0)
                throw new InvalidOperationException("Invalid tax rate was not rejected.");

            dto = CreateValid();
            dto.CurrencyCode = "USD";
            if (StoreSettingsValidator.Validate(dto).Count == 0)
                throw new InvalidOperationException("A currency other than Philippine peso was accepted.");
        }

        private static StoreSettingsDTO CreateValid()
        {
            return new StoreSettingsDTO
            {
                StoreName = "Test Store",
                CurrencyCode = "PHP",
                TimeZoneId = TimeZoneInfo.Local.Id,
                TaxName = "VAT",
                TaxRate = 0.12m,
                RegisterCode = "REG-01",
                RegisterName = "Main Register"
            };
        }

        private static void PrinterDiscoveryAndTestingUseInjectedAdapter()
        {
            var printer = new FakePrinter();
            using (var context = new POSContext(
                "Data Source=(LocalDB)\\MSSQLLocalDB;Initial Catalog=POS_PrinterAbstraction;Integrated Security=True"))
            using (var service = new StoreSettingsService(
                context,
                new SystemClock(),
                new AllowAuthorization(),
                printer))
            {
                var installed = service.GetInstalledPrinters();
                if (installed.Count != 2 || installed[0] != "Receipt A")
                    throw new InvalidOperationException("Printer discovery did not use the injected adapter.");

                var settings = CreateValid();
                settings.PrinterName = "Receipt B";
                var result = service.PrintTestPage(settings);
                if (!result.Succeeded || printer.PrintedPrinter != "Receipt B" ||
                    printer.PrintedStore != settings.StoreName)
                    throw new InvalidOperationException("Test printing did not use the selected settings.");
            }
        }

        private static void ReceiptPreviewUsesCurrentTaxAndCurrencyWithoutPrinting()
        {
            var printer = new FakePrinter();
            // An unusable endpoint proves preview does not query or allocate a sequence.
            using (var context = new POSContext(
                "Data Source=127.0.0.1,1;Initial Catalog=PreviewMustNotConnect;Integrated Security=True;Connect Timeout=1"))
            using (var service = new StoreSettingsService(
                context, new SystemClock(), new ViewAuthorization(), printer))
            {
                var settings = CreateValid();
                settings.StoreName = "Unsaved store name";
                settings.ReceiptFooter = "Sample footer";
                settings.ReceiptPrefix = "TEST-";
                settings.NextReceiptNumber = 42;
                var preview = service.BuildReceiptPreview(settings);
                if (!preview.Contains("SAMPLE - NOT A SALE") ||
                    !preview.Contains("Unsaved store name") || !preview.Contains("Sample footer") ||
                    !preview.Contains("TEST-00000042") ||
                    !preview.Contains("Subtotal: PHP 100.00") || !preview.Contains("TOTAL: PHP 112.00"))
                    throw new InvalidOperationException("Exclusive-tax preview did not reflect current settings.");

                settings.TaxInclusive = true;
                settings.MoneyDecimalPlaces = 4;
                settings.CurrencyCode = "php";
                preview = service.BuildReceiptPreview(settings);
                if (!preview.Contains("Subtotal: PHP 89.2857") ||
                    !preview.Contains("PHP 10.7143") || !preview.Contains("TOTAL: PHP 100.0000"))
                    throw new InvalidOperationException("Inclusive-tax preview did not use configured precision.");
                if (settings.NextReceiptNumber != 42 || printer.PrintedPrinter != null)
                    throw new InvalidOperationException("Preview consumed a number or sent a print job.");

                settings.TaxRate = 2m;
                try { service.BuildReceiptPreview(settings); }
                catch (System.ComponentModel.DataAnnotations.ValidationException) { return; }
                throw new InvalidOperationException("Preview accepted invalid tax settings.");
            }
        }

        private sealed class ViewAuthorization : IAuthorizationService
        {
            public bool HasPermission(string resource, ClaimActionType action) =>
                resource == POS.Core.Security.ResourceCodes.Settings && action == ClaimActionType.View;
        }

        private sealed class FakePrinter : IReceiptPrinter
        {
            public string PrintedPrinter { get; private set; }
            public string PrintedStore { get; private set; }

            public IReadOnlyList<string> GetInstalledPrinters()
            {
                return new[] { "Receipt A", "Receipt B" };
            }

            public OperationResult PrintTestPage(string printerName, string storeName, string registerName)
            {
                PrintedPrinter = printerName;
                PrintedStore = storeName;
                return OperationResult.Success();
            }

            public OperationResult PrintReceipt(string printerName, string documentName, string content)
            {
                PrintedPrinter = printerName;
                return OperationResult.Success();
            }
        }

        private sealed class AllowAuthorization : IAuthorizationService
        {
            public bool HasPermission(string resource, ClaimActionType action) => true;
        }
    }
}
