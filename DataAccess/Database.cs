using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Knowledge_Center_API.DataAccess
{
    public class Database
    {
        private readonly string _connectionString;
        public Database(string connectionString)
        {
            _connectionString = connectionString;
        }

        // === Connection Management ===

        private SqlConnection OpenConnection() 
        {
            // Opens and returns a new SQL connection
            SqlConnection connection = new SqlConnection(_connectionString);
            connection.Open();
            return connection;
        }

        // === Executing Write Operations (INSERT, UPDATE, DELETE) ===

        public int ExecuteNonQuery(string query, List<SqlParameter> parameters) 
        {
            // Executes a non-query SQL command (INSERT, UPDATE, DELETE) and returns a count of affected rows
            using (var connection = OpenConnection())
            using (var command = new SqlCommand(query, connection))
            {
                if (parameters != null)
                {
                    foreach (var param in parameters) 
                    {
                        command.Parameters.Add(param);
                    }
                }

                int rowsAffected = command.ExecuteNonQuery();
                return rowsAffected;
            }
        }

        // === Executing Read Operations (SELECT) ===

        public List<Dictionary<string, object>> ExecuteQuery(string sql, List<SqlParameter> parameters)
        {
            // Executes a SQL query and returns a list of db row data (as key/value pairs)
            var results = new List<Dictionary<string, object>>();

            using (var connection = OpenConnection())
            using (var command = new SqlCommand(sql, connection))
            {
                if (parameters != null)
                {
                    foreach (var param in parameters)
                    {
                        command.Parameters.Add(param);
                    }
                }

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var row = new Dictionary<string, object>();

                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            row[reader.GetName(i)] = reader.GetValue(i);
                        }

                        results.Add(row);
                    }
                }
            }

            return results;
        }

    }
}
