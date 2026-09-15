using POS.Common.Enumerations;
using POS.Core.Abstractions;
using POS.Core.Security;
using POS.Data;
using POS.Data.Context;
using POS.Domains.AuditEntry;
using POS.Models.Operations;
using POS.Services.Security;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace POS.Services.Maintenance
{
    public sealed class DatabaseMaintenanceService : IBackupService
    {
        private static readonly Regex DatabaseName = new Regex("^[A-Za-z][A-Za-z0-9_]{0,127}$", RegexOptions.Compiled);
        private readonly POSContext _context;
        private readonly IAuthorizationService _authorization;
        private readonly IClock _clock;
        private readonly ICurrentUser _currentUser;
        private readonly IDiagnosticLogger _logger;

        public DatabaseMaintenanceService(POSContext context, IAuthorizationService authorization,
            IClock clock, ICurrentUser currentUser, IDiagnosticLogger logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public MaintenanceHealthDTO GetHealth()
        {
            Demand(ClaimActionType.View);
            var checks = new List<HealthCheckItemDTO>();
            var builder = ConnectionBuilder();
            AddCheck(checks, "DatabaseConnectivity", "Database connectivity", () =>
            {
                _context.Database.Connection.Open();
                _context.Database.Connection.Close();
                return "Connection succeeded.";
            });
            string currentMigration = null, latestMigration = null;
            AddCheck(checks, "SchemaCompatibility", "Database schema compatibility", () =>
            {
                var pending = DatabaseMigrationService.GetPendingMigrations();
                var incompatible = DatabaseMigrationService.GetIncompatibleDatabaseMigrations();
                currentMigration = DatabaseMigrationService.GetCurrentMigration();
                latestMigration = DatabaseMigrationService.GetLatestApplicationMigration();
                if (incompatible.Count > 0) throw new InvalidOperationException("The database was migrated by a newer application version.");
                if (pending.Count > 0) throw new InvalidOperationException(pending.Count + " migration(s) are pending.");
                return "Database and application migrations match.";
            });
            AddCheck(checks, "PrinterConfiguration", "Receipt printer configuration", () =>
            {
                var configured = _context.RegisterStations.AsNoTracking().Count(x => x.IsActive && x.PrinterName != null && x.PrinterName != "");
                if (configured == 0) throw new InvalidOperationException("No active register has a receipt printer configured.");
                return configured + " active register(s) have a configured printer.";
            }, HealthState.Warning);
            AddCheck(checks, "LogDiskSpace", "Diagnostic log disk space", () =>
            {
                Directory.CreateDirectory(_logger.LogDirectory);
                var root = Path.GetPathRoot(Path.GetFullPath(_logger.LogDirectory));
                var drive = new DriveInfo(root);
                if (drive.AvailableFreeSpace < 512L * 1024L * 1024L)
                    throw new InvalidOperationException("Less than 512 MiB is available on the log drive.");
                return (drive.AvailableFreeSpace / 1024L / 1024L) + " MiB available.";
            }, HealthState.Warning);
            AddCheck(checks, "BackupAge", "Database backup age", () => GetBackupAge(builder), HealthState.Warning);

            return new MaintenanceHealthDTO
            {
                CheckedUtc = _clock.UtcNow,
                ApplicationVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? Assembly.GetExecutingAssembly().GetName().Version.ToString(),
                DatabaseName = builder.InitialCatalog,
                DatabaseVersion = currentMigration,
                LatestApplicationMigration = latestMigration,
                LogDirectory = _logger.LogDirectory,
                Checks = checks
            };
        }

        public string CreateBackup(string destinationPath)
        {
            Demand(ClaimActionType.Add);
            var path = ValidateBackupDestination(destinationPath);
            var builder = ConnectionBuilder();
            try
            {
                using (var connection = OpenMaster(builder))
                using (var command = connection.CreateCommand())
                {
                    command.CommandTimeout = 0;
                    command.CommandText = "BACKUP DATABASE " + Quote(builder.InitialCatalog) +
                        " TO DISK = @path WITH COPY_ONLY, CHECKSUM, INIT, NAME = @name; RESTORE VERIFYONLY FROM DISK = @path WITH CHECKSUM;";
                    command.Parameters.AddWithValue("@path", path);
                    command.Parameters.AddWithValue("@name", "POS authorized backup " + _clock.UtcNow.ToString("O"));
                    command.ExecuteNonQuery();
                }
                Audit("BackupCompleted", path, builder.InitialCatalog);
                _logger.Write("Information", "BackupCompleted", "Database backup and checksum verification completed.");
                return path;
            }
            catch (Exception exception)
            {
                _logger.Write("Error", "BackupFailed", "Database backup failed.", exception);
                throw;
            }
        }

        public string RestoreToNewDatabase(string backupPath, string targetDatabaseName)
        {
            Demand(ClaimActionType.Edit);
            var source = ValidateBackupSource(backupPath);
            if (!DatabaseName.IsMatch(targetDatabaseName ?? string.Empty))
                throw new ValidationException("The target database name must start with a letter and contain only letters, numbers, or underscores.");
            var builder = ConnectionBuilder();
            if (string.Equals(builder.InitialCatalog, targetDatabaseName, StringComparison.OrdinalIgnoreCase))
                throw new ValidationException("Restore must target a new database, never the live POS database.");
            try
            {
                using (var connection = OpenMaster(builder))
                {
                    if (DatabaseExists(connection, targetDatabaseName))
                        throw new ValidationException("The restore target database already exists.");
                    var files = ReadBackupFiles(connection, source);
                    var dataRoot = Convert.ToString(Scalar(connection, "SELECT CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(4000));"));
                    var logRoot = Convert.ToString(Scalar(connection, "SELECT CAST(SERVERPROPERTY('InstanceDefaultLogPath') AS nvarchar(4000));"));
                    if (string.IsNullOrWhiteSpace(dataRoot) || string.IsNullOrWhiteSpace(logRoot))
                        throw new InvalidOperationException("SQL Server did not report its default data and log paths.");
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandTimeout = 0;
                        command.CommandText = "RESTORE DATABASE " + Quote(targetDatabaseName) + " FROM DISK = @backup WITH CHECKSUM, RECOVERY, " +
                            "MOVE @dataLogical TO @dataPath, MOVE @logLogical TO @logPath; DBCC CHECKDB (" + Quote(targetDatabaseName) + ") WITH NO_INFOMSGS;";
                        command.Parameters.AddWithValue("@backup", source);
                        command.Parameters.AddWithValue("@dataLogical", files.Item1);
                        command.Parameters.AddWithValue("@logLogical", files.Item2);
                        command.Parameters.AddWithValue("@dataPath", Path.Combine(dataRoot, targetDatabaseName + ".mdf"));
                        command.Parameters.AddWithValue("@logPath", Path.Combine(logRoot, targetDatabaseName + "_log.ldf"));
                        command.ExecuteNonQuery();
                    }
                }
                Audit("RestoreCompleted", source, targetDatabaseName);
                _logger.Write("Information", "RestoreCompleted", "Backup restored to a new database and DBCC CHECKDB completed.");
                return targetDatabaseName;
            }
            catch (Exception exception)
            {
                _logger.Write("Error", "RestoreFailed", "Database restore failed. A partially created target may require administrator review.", exception);
                throw;
            }
        }

        private void Audit(string action, string path, string database)
        {
            using (AuditOperation.Begin())
            {
                _context.AuditLogs.Add(new AuditLog
                {
                    TableName = "Maintenance", Action = action, DateLogged = _clock.UtcNow,
                    UserId = _currentUser.UserId,
                    NewValue = "{\"Database\":\"" + Json(database) + "\",\"FileName\":\"" + Json(Path.GetFileName(path)) + "\"}"
                });
                _context.SaveChanges();
            }
        }

        private string GetBackupAge(SqlConnectionStringBuilder builder)
        {
            using (var connection = OpenMaster(builder))
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT MAX(backup_finish_date) FROM msdb.dbo.backupset WHERE database_name = @database AND type = 'D';";
                command.Parameters.AddWithValue("@database", builder.InitialCatalog);
                var value = command.ExecuteScalar();
                if (value == null || value == DBNull.Value) throw new InvalidOperationException("No full backup is recorded by this SQL Server instance.");
                var completed = DateTime.SpecifyKind(Convert.ToDateTime(value), DateTimeKind.Local).ToUniversalTime();
                var age = _clock.UtcNow - completed;
                if (age > TimeSpan.FromDays(1)) throw new InvalidOperationException("The latest full backup is " + Math.Floor(age.TotalHours) + " hours old.");
                return "Latest full backup completed " + Math.Floor(age.TotalHours) + " hour(s) ago.";
            }
        }

        private void AddCheck(List<HealthCheckItemDTO> checks, string code, string description,
            Func<string> check, HealthState failureState = HealthState.Failed)
        {
            try { checks.Add(new HealthCheckItemDTO { Code = code, Description = description, State = HealthState.Healthy, Detail = check() }); }
            catch (Exception exception)
            {
                checks.Add(new HealthCheckItemDTO { Code = code, Description = description, State = failureState, Detail = exception.Message });
                _logger.Write(failureState == HealthState.Failed ? "Error" : "Warning", "HealthCheck." + code, description + " failed.", exception);
            }
        }

        private SqlConnectionStringBuilder ConnectionBuilder()
        {
            var connectionString = _context.Database.Connection.ConnectionString;
            var builder = new SqlConnectionStringBuilder(connectionString);
            if (string.IsNullOrWhiteSpace(builder.InitialCatalog)) throw new InvalidOperationException("The POS connection must name a database.");
            return builder;
        }
        private static SqlConnection OpenMaster(SqlConnectionStringBuilder source)
        {
            var builder = new SqlConnectionStringBuilder(source.ConnectionString) { InitialCatalog = "master" };
            var connection = new SqlConnection(builder.ConnectionString); connection.Open(); return connection;
        }
        private static bool DatabaseExists(SqlConnection connection, string name)
        {
            using (var command = connection.CreateCommand()) { command.CommandText = "SELECT COUNT(*) FROM sys.databases WHERE name = @name;"; command.Parameters.AddWithValue("@name", name); return Convert.ToInt32(command.ExecuteScalar()) != 0; }
        }
        private static Tuple<string, string> ReadBackupFiles(SqlConnection connection, string source)
        {
            string data = null, log = null;
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "RESTORE FILELISTONLY FROM DISK = @path;"; command.Parameters.AddWithValue("@path", source);
                using (var reader = command.ExecuteReader()) while (reader.Read())
                { var type = Convert.ToString(reader["Type"]); if (type == "D" && data == null) data = Convert.ToString(reader["LogicalName"]); if (type == "L" && log == null) log = Convert.ToString(reader["LogicalName"]); }
            }
            if (data == null || log == null) throw new InvalidDataException("The backup does not contain one primary data file and one log file.");
            return Tuple.Create(data, log);
        }
        private static object Scalar(SqlConnection connection, string sql) { using (var command = connection.CreateCommand()) { command.CommandText = sql; return command.ExecuteScalar(); } }
        private static string ValidateBackupDestination(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path)) throw new ValidationException("A rooted backup destination is required.");
            path = Path.GetFullPath(path); if (!string.Equals(Path.GetExtension(path), ".bak", StringComparison.OrdinalIgnoreCase)) throw new ValidationException("The backup destination must use the .bak extension.");
            var directory = Path.GetDirectoryName(path); if (!Directory.Exists(directory)) throw new ValidationException("The backup destination directory does not exist.");
            return path;
        }
        private static string ValidateBackupSource(string path)
        {
            path = ValidateBackupDestination(path); if (!File.Exists(path)) throw new ValidationException("The backup file does not exist."); return path;
        }
        private static string Quote(string identifier) => "[" + identifier.Replace("]", "]]" ) + "]";
        private static string Json(string value) => (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        private void Demand(ClaimActionType action) => PermissionGuard.Demand(_authorization, ResourceCodes.Maintenance, action);
    }
}
