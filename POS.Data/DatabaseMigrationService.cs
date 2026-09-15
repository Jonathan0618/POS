using POS.Data.Migrations;
using System.Data.Entity.Migrations;
using System.Collections.Generic;
using System.Linq;
using System;

namespace POS.Data
{
    public static class DatabaseMigrationService
    {
        public static void ApplyPendingMigrations()
        {
            var migrator = new DbMigrator(new Configuration());
            var local = migrator.GetLocalMigrations().ToList();
            var incompatible = GetIncompatibleDatabaseMigrations(migrator, local);
            if (incompatible.Count > 0)
                throw new InvalidOperationException(
                    "The database schema is newer than this application. Deploy compatible application binaries before startup. " +
                    "Unknown migration(s): " + string.Join(", ", incompatible));
            migrator.Update();
        }

        public static IReadOnlyList<string> GetPendingMigrations()
        {
            return new DbMigrator(new Configuration()).GetPendingMigrations().ToList();
        }

        public static string GetCurrentMigration()
        {
            return new DbMigrator(new Configuration()).GetDatabaseMigrations().FirstOrDefault();
        }

        public static string GetLatestApplicationMigration()
        {
            return new DbMigrator(new Configuration()).GetLocalMigrations().LastOrDefault();
        }

        public static IReadOnlyList<string> GetUnexpectedDatabaseMigrations()
        {
            var migrator = new DbMigrator(new Configuration());
            var local = migrator.GetLocalMigrations().ToList();
            return GetUnexpectedDatabaseMigrations(migrator, local);
        }

        public static IReadOnlyList<string> GetIncompatibleDatabaseMigrations()
        {
            var migrator = new DbMigrator(new Configuration());
            var local = migrator.GetLocalMigrations().ToList();
            return GetIncompatibleDatabaseMigrations(migrator, local);
        }

        private static IReadOnlyList<string> GetIncompatibleDatabaseMigrations(
            DbMigrator migrator,
            IReadOnlyCollection<string> localMigrations)
        {
            var latestLocalMigration = localMigrations
                .Where(x => !IsAutomaticMigration(x))
                .OrderBy(x => x, StringComparer.Ordinal)
                .LastOrDefault();

            return GetUnexpectedDatabaseMigrations(migrator, localMigrations)
                .Where(x => latestLocalMigration == null ||
                    string.CompareOrdinal(x, latestLocalMigration) > 0)
                .ToList();
        }

        private static IReadOnlyList<string> GetUnexpectedDatabaseMigrations(
            DbMigrator migrator,
            IReadOnlyCollection<string> localMigrations)
        {
            return migrator.GetDatabaseMigrations()
                .Where(x => !localMigrations.Contains(x) && !IsAutomaticMigration(x))
                .ToList();
        }

        private static bool IsAutomaticMigration(string migrationId)
        {
            const string suffix = "_AutomaticMigration";
            const int timestampLength = 15;

            if (string.IsNullOrWhiteSpace(migrationId) ||
                migrationId.Length != timestampLength + suffix.Length ||
                !migrationId.EndsWith(suffix, StringComparison.Ordinal))
                return false;

            return migrationId.Take(timestampLength).All(char.IsDigit);
        }
    }
}
