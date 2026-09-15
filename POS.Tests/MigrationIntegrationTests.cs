using POS.Data.Migrations;
using System;
using System.Data;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Migrations;
using System.Data.SqlClient;
using System.Linq;

namespace POS.Tests
{
    internal static class MigrationIntegrationTests
    {
        public static void RunAll()
        {
            var databaseName = "POS_FreshMigration_" + Guid.NewGuid().ToString("N");
            var connectionString =
                "Data Source=(LocalDB)\\MSSQLLocalDB;Initial Catalog=" + databaseName +
                ";Integrated Security=True;MultipleActiveResultSets=True";

            try
            {
                var configuration = new Configuration
                {
                    TargetDatabase = new DbConnectionInfo(connectionString, "System.Data.SqlClient")
                };
                var migrator = new DbMigrator(configuration);

                migrator.Update();

                if (migrator.GetPendingMigrations().Any())
                    throw new InvalidOperationException("A fresh database still has pending migrations.");

                VerifySchemaAndIdentitySeed(connectionString);
            }
            finally
            {
                DropVerificationDatabase(databaseName);
            }
        }

        private static void VerifySchemaAndIdentitySeed(string connectionString)
        {
            using (var connection = new SqlConnection(connectionString))
            using (var command = connection.CreateCommand())
            {
                connection.Open();
                command.CommandText = @"
SELECT
    CASE WHEN OBJECT_ID('dbo.AuditLogs', 'U') IS NOT NULL THEN 1 ELSE 0 END,
    CASE WHEN OBJECT_ID('dbo.Users', 'U') IS NOT NULL THEN 1 ELSE 0 END,
    CASE WHEN OBJECT_ID('dbo.Roles', 'U') IS NOT NULL THEN 1 ELSE 0 END,
    CASE WHEN EXISTS (
        SELECT 1
        FROM dbo.Users AS u
        INNER JOIN dbo.UserRoles AS ur ON ur.UserId = u.Id
        INNER JOIN dbo.Roles AS r ON r.Id = ur.RoleId
        WHERE u.UserName = 'admin' AND r.Name = 'System Administrator'
    ) THEN 1 ELSE 0 END;";

                using (var reader = command.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (!reader.Read() || reader.GetInt32(0) != 1)
                        throw new InvalidOperationException("Fresh migration did not create dbo.AuditLogs.");
                    if (reader.GetInt32(1) != 1 || reader.GetInt32(2) != 1)
                        throw new InvalidOperationException("Fresh migration did not create the Identity tables.");
                    if (reader.GetInt32(3) != 1)
                        throw new InvalidOperationException(
                            "Fresh migration did not seed the admin user in the System Administrator role.");
                }
            }
        }

        private static void DropVerificationDatabase(string databaseName)
        {
            SqlConnection.ClearAllPools();
            var masterConnection =
                "Data Source=(LocalDB)\\MSSQLLocalDB;Initial Catalog=master;Integrated Security=True";

            using (var connection = new SqlConnection(masterConnection))
            using (var command = connection.CreateCommand())
            {
                connection.Open();
                command.CommandText = @"
IF DB_ID(@databaseName) IS NOT NULL
BEGIN
    DECLARE @quotedName nvarchar(258) = QUOTENAME(@databaseName);
    EXEC(N'ALTER DATABASE ' + @quotedName + N' SET SINGLE_USER WITH ROLLBACK IMMEDIATE');
    EXEC(N'DROP DATABASE ' + @quotedName);
END;";
                command.Parameters.AddWithValue("@databaseName", databaseName);
                command.ExecuteNonQuery();
            }
        }
    }
}
