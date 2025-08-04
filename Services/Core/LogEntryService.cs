using Knowledge_Center_API.Models;
using Knowledge_Center_API.Models.DTOs;
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

        // This update method is what can allow to not have the 
        // Tag value. Won't break the FieldValidator
        public bool CreateLogEntryFromDto(LogEntryCreateDto dto)
        {
            var log = new LogEntry
            {
                NodeId = dto.NodeId,
                Content = dto.Content,
                ContributesToProgress = dto.ContributesToProgress,
                Tags = new List<Tags>()
            };

            int newLogId = InsertLogEntry(log);
            if (newLogId > 0) return false;



            return true; 
        }

        public int InsertLogEntry(LogEntry log)
        {
            // Validate Input Fields 
            FieldValidator.ValidateId(log.NodeId, "KnowledgeNode ID");
            FieldValidator.ValidateRequiredString(log.Content, "Log Content", 2000);


            // Set timestamp
            log.EntryDate = DateTime.Now;

            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@NodeId", SqlDbType.Int) { Value = log.NodeId },
                new SqlParameter("@EntryDate", SqlDbType.DateTime) { Value = log.EntryDate },
                new SqlParameter("@Content", SqlDbType.NVarChar, 2000) { Value = log.Content },
                new SqlParameter("@ContributesToProgress", SqlDbType.Bit) { Value = log.ContributesToProgress }
            };


            // Run the ExecuteScaler command 
            int newLogId = _database.ExecuteScalar<int>
                (
                    LogEntryQueries.InsertLogEntry,
                    parameters
                );

            return newLogId;
        }

        // === READ ===

        public List<LogEntry> GetAllLogEntries()
        {
            List<LogEntry> logEntries = new List<LogEntry>();

            // SELECT Query + Parameters to retrieve all LogEntries and maps results into LogEntry objects
            var rawDBResults = _database.ExecuteQuery(LogEntryQueries.GetAllLogsWithTags, null);

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

        /*
         * Retrieves all log entries associated with a specific Knowledge Node by its Id
         * and maps the result rows to LogEntry objects, and returns them as a list. 
        */

        public List<LogEntry> GetLogsForKnowledgeNode(int nodeId)
        {
            List<SqlParameter> parameters = new List<SqlParameter>
            {
                new SqlParameter("@NodeId", nodeId)
            };

            var rawResults = _database.ExecuteQuery(LogEntryQueries.GetLogsByNodeId, parameters);

            return rawResults.Select(row => new LogEntry
            {
                LogId = Convert.ToInt32(row["LogId"]),
                NodeId = Convert.ToInt32(row["NodeId"]),
                EntryDate = Convert.ToDateTime(row["EntryDate"]),
                Content = row["Content"].ToString(),
                ContributesToProgress = Convert.ToBoolean(row["ContributesToProgress"])
            })
            .ToList();
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
                ContributesToProgress = (bool)rawDBRow["ContributesToProgress"],
                TagId = Convert.ToInt32(rawDBRow["TagId"]),
                Tag = new Tags 
                {
                    TagId = Convert.ToInt32(rawDBRow["TagId"]),
                    Name = rawDBRow["TagName"].ToString()
                }
            };
        }
    }
}
