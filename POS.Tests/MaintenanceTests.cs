using POS.Services.Maintenance;
using System;
using System.IO;

namespace POS.Tests
{
    internal static class MaintenanceTests
    {
        public static void RunAll()
        {
            DiagnosticLoggerRedactsAndBoundsSensitiveValues();
        }

        private static void DiagnosticLoggerRedactsAndBoundsSensitiveValues()
        {
            var directory = Path.Combine(Path.GetTempPath(), "POS_MaintenanceTests_" + Guid.NewGuid().ToString("N"));
            try
            {
                var logger = new DiagnosticFileLogger(directory);
                logger.Write("Information", "CredentialCheck",
                    "password=do-not-store; token=also-secret\r\nnext-line",
                    new InvalidOperationException("pwd=exception-secret"));

                var content = File.ReadAllText(Path.Combine(directory, "pos.log"));
                if (content.Contains("do-not-store") || content.Contains("also-secret") ||
                    content.Contains("exception-secret"))
                    throw new InvalidOperationException("Diagnostic logging retained a sensitive value.");
                if (!content.Contains("password=[redacted]") || !content.Contains("token=[redacted]") ||
                    !content.Contains("pwd=[redacted]") || content.Contains("\r\nnext-line"))
                    throw new InvalidOperationException("Diagnostic logging did not redact or normalize its output.");
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }
    }
}
