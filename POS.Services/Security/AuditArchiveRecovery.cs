using POS.Common.Enumerations;
using POS.Core.Security;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace POS.Services.Security
{
    public sealed partial class AuditArchiveService
    {
        private static readonly string[] ArchiveFields =
        {
            "Id", "TableName", "RecordId", "Action", "OldValue", "NewValue", "UserId",
            "DateLoggedUtc", "CorrelationId", "RegisterStationId", "RegisterCode"
        };
        private const long MaximumArchiveDataBytes = 2L * 1024 * 1024 * 1024;
        private const int MaximumRecordCharacters = 4 * 1024 * 1024;

        public bool CanRestore => CanExport && _authorization.HasPermission(ResourceCodes.Audit, ClaimActionType.Edit);

        public Task<string> VerifyAsync(string source, IProgress<long> progress, CancellationToken cancellationToken)
        {
            PermissionGuard.Demand(_authorization, ResourceCodes.Audit, ClaimActionType.View);
            return Task.Run(() =>
            {
                using (var file = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var zip = new ZipArchive(file, ZipArchiveMode.Read))
                {
                    var manifest = Verify(zip, progress, cancellationToken);
                    return "Archive integrity verified: " + manifest["EventCount"] + " events. Archive ID: " + manifest["ArchiveId"];
                }
            }, cancellationToken);
        }

        public Task<string> RestoreAsync(string source, IProgress<long> progress, CancellationToken cancellationToken)
        {
            PermissionGuard.Demand(_authorization, ResourceCodes.Audit, ClaimActionType.View);
            PermissionGuard.Demand(_authorization, ResourceCodes.Audit, ClaimActionType.Add);
            PermissionGuard.Demand(_authorization, ResourceCodes.Audit, ClaimActionType.Edit);
            var actor = _currentUser.UserId;
            var restoredUtc = _clock.UtcNow;
            return Task.Run(() =>
            {
                // Keep the source handle open without write sharing through verification
                // and import, so the file cannot be replaced between the two passes.
                using (var file = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var zip = new ZipArchive(file, ZipArchiveMode.Read))
                {
                    var manifest = Verify(zip, progress, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    string connectionString;
                    using (var context = _createContext()) connectionString = context.Database.Connection.ConnectionString;
                    var builder = new SqlConnectionStringBuilder(connectionString);
                    builder.Remove("AttachDBFilename");
                    builder.InitialCatalog = "master";
                    var databaseName = "POS_AuditRestore_" + Guid.NewGuid().ToString("N");
                    using (var master = new SqlConnection(builder.ConnectionString))
                    {
                        master.Open();
                        cancellationToken.ThrowIfCancellationRequested();
                        using (var create = master.CreateCommand())
                        {
                            // Only the generated identifier is used; no existing target can be selected.
                            create.CommandText = "CREATE DATABASE [" + databaseName + "]";
                            create.ExecuteNonQuery();
                        }
                    }
                    try
                    {
                        builder.InitialCatalog = databaseName;
                        using (var target = new SqlConnection(builder.ConnectionString))
                        {
                            target.Open();
                            using (var transaction = target.BeginTransaction())
                            {
                                using (var create = new SqlCommand(
                                    "CREATE TABLE dbo.AuditLogs (Id bigint NOT NULL PRIMARY KEY, TableName nvarchar(20) NULL, " +
                                    "RecordId nvarchar(max) NULL, Action nvarchar(max) NULL, OldValue nvarchar(max) NULL, NewValue nvarchar(max) NULL, " +
                                    "UserId nvarchar(36) NULL, DateLogged datetime2(7) NOT NULL, CorrelationId uniqueidentifier NULL, " +
                                    "RegisterStationId int NULL, RegisterCode nvarchar(30) NULL); " +
                                    "CREATE TABLE dbo.ArchiveManifest (ManifestJson nvarchar(max) NOT NULL, RestoredUtc datetime2(7) NOT NULL, RestoredByUserId nvarchar(128) NULL);",
                                    target, transaction)) create.ExecuteNonQuery();
                                using (var table = CreateArchiveTable())
                                using (var bulk = new SqlBulkCopy(target, SqlBulkCopyOptions.CheckConstraints, transaction))
                                {
                                    bulk.DestinationTableName = "dbo.AuditLogs";
                                    bulk.BatchSize = 500;
                                    foreach (DataColumn column in table.Columns) bulk.ColumnMappings.Add(column.ColumnName, column.ColumnName);
                                    foreach (var record in ReadRecords(zip.GetEntry("audit-events.jsonl"), cancellationToken))
                                    {
                                        table.Rows.Add(record);
                                        if (table.Rows.Count == 500)
                                        {
                                            bulk.WriteToServer(table);
                                            table.Clear();
                                        }
                                    }
                                    if (table.Rows.Count > 0) bulk.WriteToServer(table);
                                }
                                using (var save = new SqlCommand("INSERT INTO dbo.ArchiveManifest VALUES (@manifest, @utc, @actor)", target, transaction))
                                {
                                    save.Parameters.Add("@manifest", SqlDbType.NVarChar, -1).Value = Json(manifest);
                                    save.Parameters.Add("@utc", SqlDbType.DateTime2).Value = restoredUtc;
                                    save.Parameters.Add("@actor", SqlDbType.NVarChar, 128).Value = (object)actor ?? DBNull.Value;
                                    save.ExecuteNonQuery();
                                }
                                using (var count = new SqlCommand("SELECT COUNT_BIG(*) FROM dbo.AuditLogs", target, transaction))
                                    if ((long)count.ExecuteScalar() != Number(manifest["EventCount"]))
                                        throw new InvalidDataException("Restored event count does not match the manifest.");
                                cancellationToken.ThrowIfCancellationRequested();
                                transaction.Commit();
                            }
                        }
                        return databaseName;
                    }
                    catch (Exception exception)
                    {
                        throw new InvalidOperationException("Restore did not finish. Separate database " + databaseName +
                            " may remain empty after rollback; the live POS database was not modified.", exception);
                    }
                }
            }, cancellationToken);
        }

        private static Dictionary<string, string> Verify(ZipArchive zip, IProgress<long> progress, CancellationToken token)
        {
            if (zip.Entries.Count != 2 || zip.Entries.Count(x => x.FullName == "manifest.json") != 1 ||
                zip.Entries.Count(x => x.FullName == "audit-events.jsonl") != 1)
                throw new InvalidDataException("The file is not a supported POS audit archive.");
            var data = zip.GetEntry("audit-events.jsonl");
            var manifestEntry = zip.GetEntry("manifest.json");
            if (manifestEntry.Length > 65536 || data.Length > MaximumArchiveDataBytes)
                throw new InvalidDataException("The archive exceeds the supported size limits.");
            Dictionary<string, string> manifest;
            using (var stream = manifestEntry.Open())
            using (var reader = new StreamReader(stream, new UTF8Encoding(false, true), false))
                manifest = Parse(ReadBoundedText(reader, 65536, token));
            var required = new[] { "Format", "Version", "ArchiveId", "CreatedUtc", "ExportedByUserId", "Scope", "EventCount", "MaximumEventId",
                "OldestEventUtc", "NewestEventUtc", "DataFile", "Encoding", "Sha256", "SourceRecordsDeleted" };
            if (manifest.Count != required.Length || required.Any(x => !manifest.ContainsKey(x)) ||
                manifest["Format"] != "POS audit archive" || manifest["Version"] != "1" ||
                manifest["DataFile"] != "audit-events.jsonl" || manifest["SourceRecordsDeleted"] != "False" ||
                manifest["Encoding"] != "UTF-8 without BOM; LF line endings")
                throw new InvalidDataException("The archive manifest is unsupported or incomplete.");
            Guid.Parse(manifest["ArchiveId"]);
            Date(manifest["CreatedUtc"]);
            using (var stream = data.Open())
            using (var hash = SHA256.Create())
            {
                var buffer = new byte[65536];
                long bytesRead = 0;
                int length;
                while ((length = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    token.ThrowIfCancellationRequested();
                    bytesRead += length;
                    if (bytesRead > MaximumArchiveDataBytes) throw new InvalidDataException("Archive data is too large.");
                    hash.TransformBlock(buffer, 0, length, buffer, 0);
                }
                hash.TransformFinalBlock(new byte[0], 0, 0);
                if (bytesRead != data.Length || !string.Equals(BitConverter.ToString(hash.Hash).Replace("-", ""), manifest["Sha256"], StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Archive checksum does not match its data.");
            }
            long count = 0, lastId = 0;
            DateTime? oldest = null, newest = null;
            foreach (var record in ReadRecords(data, token))
            {
                var id = (long)record[0];
                var date = (DateTime)record[7];
                if (id <= lastId) throw new InvalidDataException("Archive IDs must be unique and ascending.");
                lastId = id;
                count++;
                if (!oldest.HasValue || date < oldest) oldest = date;
                if (!newest.HasValue || date > newest) newest = date;
                if (count % 500 == 0) progress?.Report(count);
            }
            if (count != Number(manifest["EventCount"]) || lastId != Number(manifest["MaximumEventId"]) ||
                (oldest.HasValue ? Utc(oldest.Value) : null) != manifest["OldestEventUtc"] ||
                (newest.HasValue ? Utc(newest.Value) : null) != manifest["NewestEventUtc"])
                throw new InvalidDataException("Archive count or date coverage does not match the manifest.");
            progress?.Report(count);
            return manifest;
        }

        private static IEnumerable<object[]> ReadRecords(ZipArchiveEntry entry, CancellationToken token)
        {
            using (var stream = entry.Open())
            using (var reader = new StreamReader(stream, new UTF8Encoding(false, true), false))
            {
                while (reader.Peek() >= 0)
                {
                    var line = new StringBuilder();
                    int character;
                    while ((character = reader.Read()) != -1 && character != '\n')
                    {
                        if ((line.Length & 4095) == 0) token.ThrowIfCancellationRequested();
                        if (line.Length >= MaximumRecordCharacters) throw new InvalidDataException("An archive record is too large.");
                        line.Append((char)character);
                    }
                    if (character != '\n') throw new InvalidDataException("Archive data has an incomplete final record.");
                    var fields = Parse(line.ToString());
                    if (fields.Count != ArchiveFields.Length || ArchiveFields.Any(x => !fields.ContainsKey(x)))
                        throw new InvalidDataException("An archive record has unsupported fields.");
                    yield return new object[]
                    {
                        Number(fields["Id"]), Text(fields["TableName"], 20), Text(fields["RecordId"]), Text(fields["Action"]),
                        Text(fields["OldValue"]), Text(fields["NewValue"]), Text(fields["UserId"], 36), Date(fields["DateLoggedUtc"]),
                        fields["CorrelationId"] == null ? (object)DBNull.Value : Guid.Parse(fields["CorrelationId"]),
                        fields["RegisterStationId"] == null ? (object)DBNull.Value : checked((int)Number(fields["RegisterStationId"])),
                        Text(fields["RegisterCode"], 30)
                    };
                }
            }
        }

        private static DataTable CreateArchiveTable()
        {
            var table = new DataTable();
            var types = new[] { typeof(long), typeof(string), typeof(string), typeof(string), typeof(string), typeof(string),
                typeof(string), typeof(DateTime), typeof(Guid), typeof(int), typeof(string) };
            for (var index = 0; index < ArchiveFields.Length; index++)
                table.Columns.Add(ArchiveFields[index] == "DateLoggedUtc" ? "DateLogged" : ArchiveFields[index], types[index]);
            table.Columns["DateLogged"].DateTimeMode = DataSetDateTime.Utc;
            return table;
        }

        private static string ReadBoundedText(TextReader reader, int limit, CancellationToken token)
        {
            var result = new StringBuilder();
            int character;
            while ((character = reader.Read()) != -1)
            {
                token.ThrowIfCancellationRequested();
                if (result.Length >= limit) throw new InvalidDataException("Archive manifest is too large.");
                result.Append((char)character);
            }
            return result.ToString();
        }

        private static Dictionary<string, string> Parse(string json)
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                return (Dictionary<string, string>)new DataContractJsonSerializer(typeof(Dictionary<string, string>),
                    new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true }).ReadObject(stream)
                    ?? throw new InvalidDataException("Missing archive JSON object.");
        }

        private static long Number(string value)
        {
            if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
                throw new InvalidDataException("An archive number is invalid.");
            return number;
        }

        private static DateTime Date(string value)
        {
            if (!DateTime.TryParseExact(value, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date) || date.Kind != DateTimeKind.Utc)
                throw new InvalidDataException("An archive timestamp must be UTC.");
            return date;
        }

        private static object Text(string value, int maximumLength = int.MaxValue)
        {
            if (value != null && value.Length > maximumLength) throw new InvalidDataException("An archive field exceeds its schema limit.");
            return (object)value ?? DBNull.Value;
        }
    }
}
