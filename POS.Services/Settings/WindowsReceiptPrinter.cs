using POS.Core.Abstractions;
using POS.Core.Results;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Linq;

namespace POS.Services.Settings
{
    public sealed class WindowsReceiptPrinter : IReceiptPrinter
    {
        public IReadOnlyList<string> GetInstalledPrinters()
        {
            return PrinterSettings.InstalledPrinters.Cast<string>()
                .OrderBy(x => x)
                .ToList();
        }

        public OperationResult PrintTestPage(string printerName, string storeName, string registerName)
        {
            if (string.IsNullOrWhiteSpace(printerName))
                return OperationResult.Failure("Select a printer first.");

            using (var document = new PrintDocument())
            {
                document.PrinterSettings.PrinterName = printerName;
                if (!document.PrinterSettings.IsValid)
                    return OperationResult.Failure("The selected printer is unavailable.");

                document.DocumentName = "POS Printer Test";
                document.PrintPage += (sender, args) =>
                {
                    using (var heading = new Font("Segoe UI", 12, FontStyle.Bold))
                    using (var text = new Font("Segoe UI", 9))
                    {
                        args.Graphics.DrawString(storeName ?? "POS", heading, Brushes.Black, 20, 20);
                        args.Graphics.DrawString("Printer test", text, Brushes.Black, 20, 52);
                        args.Graphics.DrawString("Register: " + (registerName ?? string.Empty), text, Brushes.Black, 20, 72);
                        args.Graphics.DrawString(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), text, Brushes.Black, 20, 92);
                    }
                };

                try
                {
                    document.Print();
                    return OperationResult.Success();
                }
                catch (Exception exception)
                {
                    return OperationResult.Failure("Test printing failed: " + exception.Message);
                }
            }
        }

        public OperationResult PrintReceipt(string printerName, string documentName, string content)
        {
            if (string.IsNullOrWhiteSpace(printerName)) return OperationResult.Failure("A receipt printer is not configured.");
            if (string.IsNullOrWhiteSpace(content)) return OperationResult.Failure("Receipt content is empty.");
            using (var document = new PrintDocument())
            {
                document.PrinterSettings.PrinterName = printerName;
                if (!document.PrinterSettings.IsValid) return OperationResult.Failure("The configured receipt printer is unavailable.");
                document.DocumentName = string.IsNullOrWhiteSpace(documentName) ? "POS Receipt" : documentName;
                document.PrintPage += (sender, args) =>
                {
                    using (var font = new Font("Consolas", 9))
                        args.Graphics.DrawString(content, font, Brushes.Black, args.MarginBounds);
                };
                try
                {
                    document.Print();
                    return OperationResult.Success();
                }
                catch (Exception exception)
                {
                    return OperationResult.Failure("Receipt printing failed: " + exception.Message);
                }
            }
        }
    }
}
