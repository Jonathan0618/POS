using System;

namespace POS.Core.Abstractions
{
    public interface IBackupService
    {
        string CreateBackup(string destinationPath);
        string RestoreToNewDatabase(string backupPath, string targetDatabaseName);
    }
}
