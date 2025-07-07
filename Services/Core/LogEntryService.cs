using Knowledge_Center_API.Models;
using Knowledge_Center_API.Services.Validation;
using Knowledge_Center_API.DataAccess;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Knowledge_Center_API.Services.Core
{
    public class LogEntryService
    {
        private readonly Database _database;

        public LogEntryService(Database database)
        {
            _database = database;
        }

        /* ===================== CRUD ===================== */

        // === CREATE ===
        public bool CreateLogEntry(LogEntry log)
        {
            // Validate Input Fields 
            FieldValidator.ValidateId(log.NodeId, "KnowledgeNode ID");
            FieldValidator.ValidateRequiredString(log.Content, "Log Content", 2000);
            FieldValidator.ValidateId(log.TagId, "Tag ID");


            // Set timestamps
            DateTime now = DateTime.Now;
            log.EntryDate = now;

            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@NodeId", SqlDbType.Int) { Value = log.NodeId },
                new SqlParameter("@EntryDate", SqlDbType.DateTime) { Value = log.EntryDate },
                new SqlParameter("@Content", SqlDbType.NVarChar, 2000) { Value = log.Content },
                new SqlParameter("@TagId", SqlDbType.Int) { Value = log.TagId },
                new SqlParameter("@ContributesToProgress", SqlDbType.Bit) { Value = log.ContributesToProgress }
            };


            // Run the INSERT query
            int result = _database.ExecuteNonQuery(LogEntryQueries.InsertLogEntry, parameters);

            // Return true to see if INSERT was successful
            return result > 0;
        }

        // === READ ===

        public List<LogEntry> GetAllLogEntries()
        {
            List<LogEntry> logEntries = new List<LogEntry>();

            // SELECT Query + Parameters to retrieve all LogEntries and maps results into LogEntry objects
            var rawDBResults = _database.ExecuteQuery(LogEntryQueries.GetAllLogs, null);

            if (rawDBResults.Count == 0)
            {
                return null;
            }

            foreach (var rawDBRow in rawDBResults)
            {
                logEntries.Add(ConvertDBRowToClassObj(rawDBRow));
            }

            return logEntries;
        }
        public List<LogEntry> GetAllLogEntriesByNodeId(int nodeId)
        {
            // Validate Input Fields 
            FieldValidator.ValidateId(nodeId, "KnowledgeNode ID");

            List<SqlParameter> parameters = new List<SqlParameter>
            {
                new SqlParameter("@NodeId", SqlDbType.Int) { Value = nodeId }
            };

            // SELECT Query + Parameters to retrieve all LogEntries from a specific Knowledge Node and maps results into LogEntry objects

            var rawDBResults = _database.ExecuteQuery(LogEntryQueries.GetLogsByNodeId, parameters);

            if (rawDBResults.Count == 0)
            {
                return null;
            }

            List<LogEntry> logEntries = new List<LogEntry>();

            foreach (var rawDBRow in rawDBResults)
            {
                logEntries.Add(ConvertDBRowToClassObj(rawDBRow));
            }

            return logEntries;
        }

        // This function is being used in the API
        public LogEntry GetLogEntryByLogId(int logId)
        {
            // Validate Input Fields 
            FieldValidator.ValidateId(logId, "Log Id");

            List<SqlParameter> parameters = new List<SqlParameter>
            {
                new SqlParameter("@LogId", SqlDbType.Int) { Value = logId }
            };
            // SELECT Query + Parameters to retrieve a specific LogEntry by LogID and map result into a LogEntry object
            var rawDBResults = _database.ExecuteQuery(LogEntryQueries.GetLogByLogId, parameters);
            if (rawDBResults.Count == 0)
            {
                return null;
            }
            return ConvertDBRowToClassObj(rawDBResults[0]);
        }

        // === DELETE ===
        public bool DeleteAllLogEntriesByNodeId(int nodeId)
        {
            // Validate Input Fields 
            FieldValidator.ValidateId(nodeId, "KnowledgeNode Id");

            List<SqlParameter> parameters = new List<SqlParameter>
            {
                new SqlParameter("@NodeId", SqlDbType.Int) { Value = nodeId }
            };

            // DELETE Query + Parameters to delete all LogEntries from a specific Knowledge Node
            int result = _database.ExecuteNonQuery(LogEntryQueries.DeleteAllLogsByNodeId, parameters);

            // Return true to see if DELETE was successful
            return result > 0;
        }

        /* ===================== DATA TYPE CONVERTERS (MAPPERS) ===================== */

        private LogEntry ConvertDBRowToClassObj(Dictionary<string, object> rawDBRow)
        {
            return new LogEntry
            {
                LogId = Convert.ToInt32(rawDBRow["LogId"]),
                NodeId = Convert.ToInt32(rawDBRow["NodeId"]),
                EntryDate = Convert.ToDateTime(rawDBRow["EntryDate"]),
                Content = rawDBRow["Content"].ToString(),
                TagId = Convert.ToInt32(rawDBRow["TagId"]),
                ContributesToProgress = (bool)rawDBRow["ContributesToProgress"]
            };
        }
    }
}
