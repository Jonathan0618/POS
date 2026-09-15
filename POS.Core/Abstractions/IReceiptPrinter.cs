using POS.Core.Results;
using System.Collections.Generic;

namespace POS.Core.Abstractions
{
    public interface IReceiptPrinter
    {
        IReadOnlyList<string> GetInstalledPrinters();
        OperationResult PrintTestPage(string printerName, string storeName, string registerName);
        OperationResult PrintReceipt(string printerName, string documentName, string content);
    }
}
