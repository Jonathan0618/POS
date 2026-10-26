using System;
using System.Collections.Generic;

namespace POS.Models.Operations
{
    public enum HealthState { Healthy, Warning, Failed }

    public sealed class HealthCheckItemDTO
    {
        public string Code { get; set; }
        public string Description { get; set; }
        public HealthState State { get; set; }
        public string Detail { get; set; }
    }

    public sealed class MaintenanceHealthDTO
    {
        public DateTime CheckedUtc { get; set; }
        public string ApplicationVersion { get; set; }
        public string DatabaseName { get; set; }
        public string DatabaseVersion { get; set; }
        public string LatestApplicationMigration { get; set; }
        public string LogDirectory { get; set; }
        public IReadOnlyList<HealthCheckItemDTO> Checks { get; set; }
    }

    public sealed class BackupInfoDTO
    {
        public string Path { get; set; }
        public string DatabaseName { get; set; }
        public DateTime CompletedUtc { get; set; }
        public long SizeBytes { get; set; }
    }

    public sealed class RestoreInfoDTO
    {
        public string BackupPath { get; set; }
        public string DatabaseName { get; set; }
        public DateTime CompletedUtc { get; set; }
    }
}
