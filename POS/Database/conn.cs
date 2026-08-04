using System.Data;                 // Provides DataTable and other data-related classes.
using System.Data.SqlClient;       // Provides SQL Server classes like SqlConnection and SqlCommand.

namespace POS
{
    // Static means you don't need to create an object to use this class.
    public static class Database
    {
        // Stores the connection string used to connect to the SQL Server database.
        // readonly means it cannot be changed after the program starts.
        private static readonly string connectionString =
            @"Data Source=.\SQLEXPRESS;
              Initial Catalog=POSDB;
              Integrated Security=True";

        // Returns a new SqlConnection object whenever it's needed.
        public static SqlConnection GetConnection()
        {
            // Create a new SQL Server connection using the connection string.
            return new SqlConnection(connectionString);
        }

        // Used for SQL queries that RETURN DATA (e.g., SELECT).
        // Returns the result as a DataTable.
        public static DataTable ExecuteQuery(string query)
        {
            // Create a new database connection.
            using (SqlConnection conn = GetConnection())

            // Create a SQL command using the query and connection.
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                // Open the connection to the database.
                conn.Open();

                // Create an empty DataTable that will store the returned records.
                DataTable table = new DataTable();

                // SqlDataAdapter executes the SELECT query and fills the DataTable.
                using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                {
                    // Execute the query and store the results in the DataTable.
                    adapter.Fill(table);
                }

                // Return the DataTable containing the query results.
                return table;
            } // Connection is automatically closed here because of "using".
        }

        // Used for SQL commands that DO NOT return data.
        // Examples: INSERT, UPDATE, DELETE, CREATE TABLE.
        public static int ExecuteNonQuery(string query)
        {
            // Create a new database connection.
            using (SqlConnection conn = GetConnection())

            // Create a SQL command.
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                // Open the database connection.
                conn.Open();

                // Execute the command.
                // Returns the number of rows affected.
                return cmd.ExecuteNonQuery();
            } // Connection is automatically closed here.
        }
    }
}