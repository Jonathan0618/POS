using POS.Common.Enumerations;
using POS.Core.Abstractions;
using POS.Core.Security;
using POS.Data.Context;
using POS.Domains.AuditEntry;
using POS.Services.Repository;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
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
        private readonly Func<POSContext> _createContext;
        private readonly IAuthorizationService _authorization;
        private readonly IClock _clock;
        private readonly ICurrentUser _currentUser;

        public AuditArchiveService(Func<POSContext> createContext, IAuthorizationService authorization,
            IClock clock, ICurrentUser currentUser)
        {
            _createContext = createContext ?? throw new ArgumentNullException(nameof(createContext));
            _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        }

        public bool CanExport => _authorization.HasPermission(ResourceCodes.Audit, ClaimActionType.View) &&
            _authorization.HasPermission(ResourceCodes.Audit, ClaimActionType.Add);

        public Task<long> ExportAsync(string destination, IProgress<long> progress, CancellationToken cancellationToken)
        {
            PermissionGuard.Demand(_authorization, ResourceCodes.Audit, ClaimActionType.View);
            PermissionGuard.Demand(_authorization, ResourceCodes.Audit, ClaimActionType.Add);
            if (string.IsNullOrWhiteSpace(destination)) throw new ArgumentException("Choose an archive file.", nameof(destination));
            var fullPath = Path.GetFullPath(destination);
            if (File.Exists(fullPath)) throw new IOException("Choose a new filename; existing archives are not overwritten.");
            var actor = _currentUser.UserId;
            var createdUtc = _clock.UtcNow;
            return Task.Run(() => WriteArchiveAsync(fullPath, actor, createdUtc, progress, cancellationToken), cancellationToken);
        }

        private async Task<long> WriteArchiveAsync(string destination, string actor, DateTime createdUtc,
            IProgress<long> progress, CancellationToken cancellationToken)
        {
            var archiveId = Guid.NewGuid();
            var temporary = Path.Combine(Path.GetDirectoryName(destination), ".pos-audit-" + archiveId.ToString("N") + ".tmp");
            var ownsTemporary = false;
            try
            {
                long count = 0;
                long maxId;
                DateTime? oldestUtc = null;
                DateTime? newestUtc = null;
                using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    ownsTemporary = true;
                    using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
                    using (var context = _createContext())
                    using (var repository = new BaseRepository<AuditLog, long>(context, false))
                    using (var transaction = context.Database.BeginTransaction(IsolationLevel.Serializable))
                    {
                        // Hold a consistent read across all batches. The read transaction
                        // can delay audit writers until the archive data has been copied.
                        maxId = await repository.GetAllAsQueryable().MaxAsync(x => (long?)x.Id, cancellationToken) ?? 0;
                        long lastId = 0;
                        string checksum;
                        using (var hash = SHA256.Create())
                        using (var records = zip.CreateEntry("audit-events.jsonl", CompressionLevel.Optimal).Open())
                        {
                            while (lastId < maxId)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                var batch = await repository.GetAllAsQueryable().Where(x => x.Id > lastId && x.Id <= maxId)
                                    .OrderBy(x => x.Id).Take(500).ToListAsync(cancellationToken);
                                if (batch.Count == 0) break;
                                foreach (var audit in batch)
                                {
                                    cancellationToken.ThrowIfCancellationRequested();
                                    var fields = new Dictionary<string, string>
                                    {
                                        { "Id", audit.Id.ToString(CultureInfo.InvariantCulture) },
                                        { "TableName", audit.TableName }, { "RecordId", audit.RecordId },
                                        { "Action", audit.Action }, { "OldValue", audit.OldValue }, { "NewValue", audit.NewValue },
                                        { "UserId", audit.UserId }, { "DateLoggedUtc", Utc(audit.DateLogged) },
                                        { "CorrelationId", audit.CorrelationId?.ToString("D") },
                                        { "RegisterStationId", audit.RegisterStationId?.ToString(CultureInfo.InvariantCulture) },
                                        { "RegisterCode", audit.RegisterCode }
                                    };
                                    var bytes = Encoding.UTF8.GetBytes(Json(fields) + "\n");
                                    await records.WriteAsync(bytes, 0, bytes.Length, cancellationToken);
                                    hash.TransformBlock(bytes, 0, bytes.Length, bytes, 0);
                                    lastId = audit.Id;
                                    count++;
                                    if (!oldestUtc.HasValue || audit.DateLogged < oldestUtc.Value) oldestUtc = audit.DateLogged;
                                    if (!newestUtc.HasValue || audit.DateLogged > newestUtc.Value) newestUtc = audit.DateLogged;
                                }
                                progress?.Report(count);
                            }
                            hash.TransformFinalBlock(new byte[0], 0, 0);
                            checksum = BitConverter.ToString(hash.Hash).Replace("-", "").ToLowerInvariant();
                        }
                        transaction.Commit();
                        var manifest = new Dictionary<string, string>
                        {
                            { "Format", "POS audit archive" }, { "Version", "1" },
                            { "ArchiveId", archiveId.ToString("D") }, { "CreatedUtc", Utc(createdUtc) },
                            { "ExportedByUserId", actor }, { "Scope", "All retained audit events through MaximumEventId" },
                            { "EventCount", count.ToString(CultureInfo.InvariantCulture) },
                            { "MaximumEventId", maxId.ToString(CultureInfo.InvariantCulture) },
                            { "OldestEventUtc", oldestUtc.HasValue ? Utc(oldestUtc.Value) : null },
                            { "NewestEventUtc", newestUtc.HasValue ? Utc(newestUtc.Value) : null },
                            { "DataFile", "audit-events.jsonl" }, { "Encoding", "UTF-8 without BOM; LF line endings" },
                            { "Sha256", checksum }, { "SourceRecordsDeleted", "False" }
                        };
                        using (var manifestStream = zip.CreateEntry("manifest.json").Open())
                        {
                            var bytes = Encoding.UTF8.GetBytes(Json(manifest));
                            await manifestStream.WriteAsync(bytes, 0, bytes.Length, cancellationToken);
                        }
                    }
                }
                cancellationToken.ThrowIfCancellationRequested();
                File.Move(temporary, destination);
                ownsTemporary = false;
                return count;
            }
            finally
            {
                if (ownsTemporary)
                {
                    try { File.Delete(temporary); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
        }

        private static string Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture);

        private static string Json(Dictionary<string, string> fields)
        {
            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(Dictionary<string, string>),
                    new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true }).WriteObject(stream, fields);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }
    }
}
