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

            BulkInsertLogEntryTagRelation(newLogId, dto.TagIds);

            return true; 
        }

        private int InsertLogEntry(LogEntry log)
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

        public List<int> AddNewTagsToLog(int logId, List<int> tagIds) 
        {
            // Validate the LogId
            FieldValidator.ValidateId(logId, "Log ID");

            // Fetch current tagIDs on this log
            var parameters = new List<SqlParameter> { new SqlParameter(@"LogId", SqlDbType.Int) { Value = logId } };
            var existingRows = _database.ExecuteQuery(LogEntryQueries.GetLogTagRelationsByLogId, parameters);

            // Take existing rows and make a hash set for looking up before adding to a hash
            var existingTagIds = existingRows
                .Select(r => Convert.ToInt32(r["TagId"]))
                .ToHashSet();

            // Filter out duplicates
            var toAdd = existingTagIds
                .Where(id => !existingTagIds.Contains(id))
                .Distinct()
                .ToList();

            // Another round of validation for tag ID
            foreach(int tagId in toAdd) 
            {
                FieldValidator.ValidateId(tagId, "Tag ID");
                SingleInsertLogEntryTagRelation(logId, tagId);
            }

            return toAdd;
        }

        private void BulkInsertLogEntryTagRelation(int logId, List<int> tagIds) 
        {
            foreach(int tagId in tagIds) 
            {
                FieldValidator.ValidateId(tagId, "Tag ID");

                var parameters = new List<SqlParameter>
                {
                    new SqlParameter("@LogId", SqlDbType.Int) {Value = logId},
                    new SqlParameter("@TagId", SqlDbType.Int) {Value = tagId},
                };

                _database.ExecuteNonQuery(LogEntryQueries.InsertLogTagRelation, parameters);
            }
        }

        private void SingleInsertLogEntryTagRelation(int logId, int tagId) 
        {
            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@LogId", SqlDbType.Int) {Value = logId},
                new SqlParameter("@TagId", SqlDbType.Int) {Value = tagId},
            };

            _database.ExecuteNonQuery(LogEntryQueries.InsertLogTagRelation, parameters);
        }

        // === READ ===

        public List<LogEntry> GetAllLogEntries()
        {
            // Building out the log without tags
            List<LogEntry> logEntries = new List<LogEntry>();

            // SELECT Query + Parameters to retrieve all LogEntries and maps results into LogEntry objects
            var rawDBResults = _database.ExecuteQuery(LogEntryQueries.GetAllLogsWithoutTags, null);
            if (rawDBResults.Count == 0) return logEntries;

            foreach (var rawDBRow in rawDBResults)
            {
                logEntries.Add(new LogEntry
                {
                    LogId = Convert.ToInt32(rawDBRow["LogId"]),
                    NodeId = Convert.ToInt32(rawDBRow["NodeId"]),
                    EntryDate = Convert.ToDateTime(rawDBRow["EntryDate"]),
                    Content = rawDBRow["Content"].ToString(),
                    ContributesToProgress = Convert.ToBoolean(rawDBRow["ContributesToProgress"]),
                    Tags = new() // Placeholder
                });
            }

            // Now we map out the tags to the log. 
            // Fetching all tag relations 
            var tagResults = _database.ExecuteQuery(LogEntryQueries.GetAllLogTagRelations, null);

            // Group the tags by LogId
            var logTagMap = tagResults
                .GroupBy(r => Convert.ToInt32(r["LogId"]))
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(t => new Tags
                    {
                        TagId = Convert.ToInt32(t["TagId"]),
                        Name = t["TagName"].ToString()
                    }).ToList()
                );

            foreach(var log in logEntries) 
            {
                if (logTagMap.ContainsKey(log.LogId))
                    log.Tags = logTagMap[log.LogId];
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
            var rawDBResults = _database.ExecuteQuery(LogEntryQueries.GetLogByIdWithoutTags, parameters);
            if (rawDBResults.Count == 0) return null;

            // Build out the log, no tags
            var rawDBRow = rawDBResults[0];
            var log = new LogEntry
            {
                LogId = Convert.ToInt32(rawDBRow["LogId"]),
                NodeId = Convert.ToInt32(rawDBRow["NodeId"]),
                EntryDate = Convert.ToDateTime(rawDBRow["EntryDate"]),
                Content = rawDBRow["Content"].ToString(),
                ContributesToProgress = Convert.ToBoolean(rawDBRow["ContributesToProgress"]),
                Tags = new List<Tags>()
            };

            // Get it's tags
            var tagRows = _database.ExecuteQuery(LogEntryQueries.GetLogTagRelationsByLogId, parameters);

            // Add the tags to the log
            foreach(var tag in tagRows) 
            {
                log.Tags.Add(new Tags
                {
                    TagId = Convert.ToInt32(tag["TagId"]),
                    Name = tag["TagName"].ToString()
                });
            }

            return log;
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
    }
}
