using System;

namespace POS.Core.Abstractions
{
    public interface IDiagnosticLogger
    {
        string LogDirectory { get; }
        void Write(string level, string eventName, string message, Exception exception = null);
    }
}
