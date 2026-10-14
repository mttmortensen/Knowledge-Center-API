using Npgsql;
using Xunit;

namespace Knowledge_Center_API.Tests
{
    /*
     * The DB-backed endpoint tests run against a throwaway Postgres database given
     * by KC_TEST_DB (a full Npgsql connection string). Reset() wipes and rebuilds
     * that database's public schema, so it refuses any database whose name doesn't
     * contain "test"; it can never be pointed at kc_db.
     */
    public static class TestDatabase
    {
        public static string? ConnectionString => Environment.GetEnvironmentVariable("KC_TEST_DB");

        public static void Reset()
        {
            var builder = new NpgsqlConnectionStringBuilder(ConnectionString);
            if (builder.Database == null || !builder.Database.Contains("test", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Refusing to reset database '{builder.Database}': its name must contain 'test'.");

            string sqlDir = Path.Combine(AppContext.BaseDirectory, "SQL");
            string[] scripts = { "postgres_schema.sql", "add_archiving.sql", "create_actions_table.sql", "add_action_follow_up.sql", "create_learn_tables.sql", "add_learn_entry_log_link.sql" };

            using var conn = new NpgsqlConnection(ConnectionString);
            conn.Open();

            Execute(conn, "DROP SCHEMA public CASCADE; CREATE SCHEMA public;");
            foreach (string script in scripts)
                Execute(conn, File.ReadAllText(Path.Combine(sqlDir, script)));
        }

        public static int Scalar(string sql)
        {
            using var conn = new NpgsqlConnection(ConnectionString);
            conn.Open();
            using var cmd = new NpgsqlCommand(sql, conn);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        private static void Execute(NpgsqlConnection conn, string sql)
        {
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>A [Fact] that is skipped unless KC_TEST_DB is set.</summary>
    public sealed class DbFactAttribute : FactAttribute
    {
        public DbFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(TestDatabase.ConnectionString))
                Skip = "Set KC_TEST_DB to a Postgres connection string for a database whose name contains 'test'.";
        }
    }
}
