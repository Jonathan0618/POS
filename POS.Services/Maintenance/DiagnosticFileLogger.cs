using POS.Core.Abstractions;
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace POS.Services.Maintenance
{
    public sealed class DiagnosticFileLogger : IDiagnosticLogger
    {
        private const long MaximumFileBytes = 5L * 1024L * 1024L;
        private const int RetainedFiles = 5;
        private readonly object _sync = new object();

        public DiagnosticFileLogger(string logDirectory = null)
        {
            LogDirectory = Path.GetFullPath(logDirectory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "POS", "Logs"));
        }

        public string LogDirectory { get; }

        public void Write(string level, string eventName, string message, Exception exception = null)
        {
            lock (_sync)
            {
                Directory.CreateDirectory(LogDirectory);
                RotateIfNeeded();
                var line = string.Join(" | ", DateTime.UtcNow.ToString("O"), Clean(level, 20),
                    Clean(eventName, 100), Redact(message, 2000),
                    exception == null ? string.Empty : Clean(exception.GetType().Name, 200) + ": " + Redact(exception.Message, 2000));
                File.AppendAllText(CurrentPath(), line + Environment.NewLine, new UTF8Encoding(false));
            }
        }

        private void RotateIfNeeded()
        {
            var current = CurrentPath();
            if (!File.Exists(current) || new FileInfo(current).Length < MaximumFileBytes) return;
            var oldest = Path.Combine(LogDirectory, "pos." + RetainedFiles + ".log");
            if (File.Exists(oldest)) File.Delete(oldest);
            for (var index = RetainedFiles - 1; index >= 1; index--)
            {
                var source = Path.Combine(LogDirectory, "pos." + index + ".log");
                if (File.Exists(source)) File.Move(source, Path.Combine(LogDirectory, "pos." + (index + 1) + ".log"));
            }
            File.Move(current, Path.Combine(LogDirectory, "pos.1.log"));
        }

        private string CurrentPath() => Path.Combine(LogDirectory, "pos.log");
        private static string Clean(string value, int limit)
        {
            value = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Replace('|', '/');
            return value.Length <= limit ? value : value.Substring(0, limit) + " [truncated]";
        }
        private static string Redact(string value, int limit)
        {
            value = Regex.Replace(value ?? string.Empty,
                "(?i)(password|pwd|token|secret|security stamp|user id|uid)\\s*=\\s*[^;\\s]+", "$1=[redacted]");
            return Clean(value, limit);
        }
    }
}
