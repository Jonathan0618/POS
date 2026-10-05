# POS Deployment and Recovery Operations

## Supported deployment

- Windows with .NET Framework 4.8.
- SQL Server or SQL Server Express reachable through the `POSConnection` connection string.
- A separate administrator identity for maintenance. Only the System Administrator role is seeded with the `Maintenance` resource.

Keep database credentials out of source control and diagnostic messages. Grant the SQL Server service account write access to the approved backup directory; access by the desktop user alone is insufficient for `BACKUP DATABASE`.

## Upgrade procedure

1. Stop all POS instances and confirm there are no active cashier operations.
2. Take a full `COPY_ONLY` backup with `CHECKSUM`, then run `RESTORE VERIFYONLY ... WITH CHECKSUM`.
3. Preserve the application binaries and configuration being replaced.
4. Deploy the new binaries and retain the protected connection configuration.
5. Start one application instance. Startup applies pending non-destructive EF migrations and refuses to continue if migration fails.
6. Review schema compatibility and connectivity through the maintenance health contract when its UI is delivered.
7. Reopen other workstations only after the first instance starts successfully.

Never enable automatic data-loss migrations. Review generated SQL, data preflight, backup location, and forward-recovery steps before applying a destructive or transforming migration.

## Backup policy

- Take at least one full checked backup every business day and before each upgrade or migration.
- Keep at least one protected copy outside the POS computer and retain backups according to approved policy.
- Monitor disk space and the age of the latest full backup.
- Use unique file names; do not overwrite the last known-good backup.

`DatabaseMaintenanceService.CreateBackup` accepts a rooted `.bak` path, performs a full `COPY_ONLY` backup with checksum, verifies the set, writes a safe diagnostic event, and audits only the database and file names.

## Restore procedure

The application backend restores only to a new database and never overwrites live `POSV1`.

1. Stop application instances for a controlled recovery exercise.
2. Select a checked backup and a new database name containing only letters, numbers, and underscores.
3. Call `DatabaseMaintenanceService.RestoreToNewDatabase` through an authorized administrator session.
4. The service relocates the logical data/log files to SQL Server's default directories and runs `DBCC CHECKDB`.
5. Point a separately configured application copy at the restored database for acceptance checks.
6. If restoration fails after target creation, a DBA must inspect and remove only that named incomplete recovery database.

Live-database replacement remains a manual DBA operation because it requires coordinated exclusive access, a pre-restore backup, connection control, and explicit business-owner approval.

## Diagnostics and ownership

Diagnostics default to `%LOCALAPPDATA%\POS\Logs`. Files rotate at 5 MiB with five archives retained. The logger redacts common credential assignments and bounds every field; callers must still avoid customer, payment, credential, token, or connection-string data.

Health reporting covers database connectivity, EF migration compatibility, configured receipt printers, log-drive free space, and latest SQL full-backup age. Warning results do not certify printer hardware or backup restorability.

The system administrator owns daily backup review. A DBA owns failed migration recovery and incomplete restore cleanup. The business owner approves production cutover to recovered data. Perform and document a separate-database restore drill before production acceptance and on the organization's recurring schedule.
