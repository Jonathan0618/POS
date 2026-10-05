using System;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;

namespace POS.Services.Inventory
{
    internal static class InventoryConflict
    {
        // Call only after transaction rollback and tracked-state cleanup.
        // Connection/command timeouts can have uncertain outcomes and are excluded.
        public static bool IsRecognized(Exception exception)
        {
            for (var cause = exception; cause != null; cause = cause.InnerException)
            {
                if (cause is DbUpdateConcurrencyException) return true;
                var sql = cause as SqlException;
                if (sql == null) continue;
                foreach (SqlError error in sql.Errors)
                    if (error.Number == 1205 || error.Number == 1222) return true;
            }
            return false;
        }

        public static bool IsUniqueIndexViolation(Exception exception, string indexName)
        {
            for (var cause = exception; cause != null; cause = cause.InnerException)
            {
                var sql = cause as SqlException;
                if (sql == null) continue;
                foreach (SqlError error in sql.Errors)
                    if ((error.Number == 2601 || error.Number == 2627) &&
                        sql.Message.IndexOf(indexName, StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
            }
            return false;
        }
    }
}
