using System;
using System.Threading;

namespace POS.Core.Abstractions
{
    public sealed class AuditOperation : IDisposable
    {
        private static readonly AsyncLocal<Guid?> AmbientId = new AsyncLocal<Guid?>();
        private static readonly AsyncLocal<int?> AmbientRegister = new AsyncLocal<int?>();
        private readonly Guid? _previous;
        private readonly int? _previousRegister;
        private bool _disposed;

        private AuditOperation(int? registerStationId)
        {
            _previous = AmbientId.Value;
            _previousRegister = AmbientRegister.Value;
            AmbientId.Value = _previous ?? Guid.NewGuid();
            AmbientRegister.Value = registerStationId ?? _previousRegister;
        }

        public static Guid? CurrentId => AmbientId.Value;
        public static int? CurrentRegisterId => AmbientRegister.Value;
        public static AuditOperation Begin(int? registerStationId = null)
        {
            if (registerStationId.HasValue && registerStationId.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(registerStationId));
            return new AuditOperation(registerStationId);
        }

        public void Dispose()
        {
            if (_disposed) return;
            AmbientId.Value = _previous;
            AmbientRegister.Value = _previousRegister;
            _disposed = true;
        }
    }
}
