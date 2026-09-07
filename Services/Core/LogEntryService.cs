using Knowledge_Center_API.Services.Validation;
using Knowledge_Center_API.DataAccess;
using Npgsql;
using NpgsqlTypes;
using Knowledge_Center_API.Models.LogEntries;
using Knowledge_Center_API.Models.TagEntries;

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
        public int CreateLogEntryAndReturnId(LogEntryCreateDto dto)
        {
            var log = new LogEntry
            {
                NodeId = dto.NodeId,
                Title = dto.Title,
                Content = dto.Content,
                ContributesToProgress = dto.ContributesToProgress,
                ChatURL = dto.ChatURL,
                Tags = new List<Tags>()
            };

            int newLogId = InsertLogEntry(log);
            if (newLogId <= 0) return -1;

            BulkInsertLogEntryTagRelation(newLogId, dto.TagIds);

            return newLogId;
        }

        private int InsertLogEntry(LogEntry log)
        {
            // Validate Input Fields
            // Content limit raised well past the old 2000-char cap: entries are now
            // full markdown docs/guides authored in a rich editor, not short log lines.
            // "Content" is a Postgres TEXT column (unbounded), so the parameter below
            // uses NpgsqlDbType.Text rather than a length-limited Varchar.
            FieldValidator.ValidateId(log.NodeId, "KnowledgeNode ID");
            FieldValidator.ValidateRequiredString(log.Content, "Log Content", 200_000);
            FieldValidator.ValidateOptionalString(log.Title, "Title", 200);
            FieldValidator.ValidateOptionalChatURL(log.ChatURL, "Chat URL", 2000);


            // Set timestamp
            log.EntryDate = DateTime.Now;

            var parameters = new List<NpgsqlParameter>
            {
                new NpgsqlParameter("@NodeId", NpgsqlDbType.Integer) { Value = log.NodeId },
                new NpgsqlParameter("@EntryDate", NpgsqlDbType.Timestamp) { Value = log.EntryDate },
                new NpgsqlParameter("@Title", NpgsqlDbType.Varchar, 200) { Value = string.IsNullOrWhiteSpace(log.Title) ? DBNull.Value : log.Title },
                new NpgsqlParameter("@Content", NpgsqlDbType.Text) { Value = log.Content },
                new NpgsqlParameter("@ContributesToProgress", NpgsqlDbType.Boolean) { Value = log.ContributesToProgress },
                new NpgsqlParameter("@ChatURL", NpgsqlDbType.Varchar, 2000) { Value = string.IsNullOrWhiteSpace(log.ChatURL) ? DBNull.Value : log.ChatURL }
            };


            // Run the ExecuteScaler command 
            int newLogId = _database.ExecuteScalar<int>
                (
                    LogEntryQueries.InsertLogEntry,
                    parameters
                );

            return newLogId;
        }

        // === UPDATE ===

        public List<int> AddNewTagsToLog(int logId, List<int> tagIds)
        {
            FieldValidator.ValidateId(logId, "Log ID");

            // Fetch current tagIDs on this log
            var parameters = new List<NpgsqlParameter> { new NpgsqlParameter("@LogId", NpgsqlDbType.Integer) { Value = logId } };
            var existingRows = _database.ExecuteQuery(LogEntryQueries.GetLogTagRelationsByLogId, parameters);

            var existingTagIds = existingRows
                .Select(r => Convert.ToInt32(r["TagId"]))
                .ToHashSet();

            // Only add new tags that aren't already linked
            var toAdd = tagIds
                .Where(id => !existingTagIds.Contains(id))
                .Distinct()
                .ToList();

            foreach (int tagId in toAdd)
            {
                FieldValidator.ValidateId(tagId, "Tag ID");

                var insertParams = new List<NpgsqlParameter>
                {
                    new NpgsqlParameter("@LogId", NpgsqlDbType.Integer) { Value = logId },
                    new NpgsqlParameter("@TagId", NpgsqlDbType.Integer) { Value = tagId }
                };

                _database.ExecuteNonQuery(LogEntryQueries.InsertLogTagRelation, insertParams);
            }

            return toAdd;
        }

        public bool UpdateLogEntryContent(int logId, LogEntryContentUpdateDto dto)
        {
            FieldValidator.ValidateId(logId, "Log ID");

            LogEntry existing = GetLogEntryByLogId(logId);
            if (existing == null) return false;

            // Merge-style update, mirroring KnowledgeNodeService.UpdateKnowledgeNodeFromDto:
            // only fields the caller actually sent replace what's already stored.
            string newTitle = dto.Title ?? existing.Title;
            string newContent = !string.IsNullOrWhiteSpace(dto.Content) ? dto.Content : existing.Content;
            bool newContributesToProgress = dto.ContributesToProgress ?? existing.ContributesToProgress;

            FieldValidator.ValidateRequiredString(newContent, "Log Content", 200_000);
            FieldValidator.ValidateOptionalString(newTitle, "Title", 200);

            var parameters = new List<NpgsqlParameter>
            {
                new NpgsqlParameter("@LogId", NpgsqlDbType.Integer) { Value = logId },
                new NpgsqlParameter("@Title", NpgsqlDbType.Varchar, 200) { Value = string.IsNullOrWhiteSpace(newTitle) ? DBNull.Value : newTitle },
                new NpgsqlParameter("@Content", NpgsqlDbType.Text) { Value = newContent },
                new NpgsqlParameter("@ContributesToProgress", NpgsqlDbType.Boolean) { Value = newContributesToProgress }
            };

            int rowsAffected = _database.ExecuteNonQuery(LogEntryQueries.UpdateLogEntryContent, parameters);
            return rowsAffected > 0;
        }

        public bool UpdateChatURL(int logId, string? chatURL)
        {
            FieldValidator.ValidateId(logId, "Log ID");
            FieldValidator.ValidateOptionalChatURL(chatURL, "Chat URL", 2000);

            var parameters = new List<NpgsqlParameter> 
            {
                new NpgsqlParameter("@LogId", NpgsqlDbType.Integer) { Value = logId },
                new NpgsqlParameter("@ChatURL", NpgsqlDbType.Varchar, 2000) 
                {
                    Value = string.IsNullOrWhiteSpace(chatURL) ? DBNull.Value : chatURL
                }
            };

            int rowsAffected = _database.ExecuteNonQuery(LogEntryQueries.UpdateChatURLByLogId, parameters);

            return rowsAffected > 0;
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
                    Title = rawDBRow["Title"]?.ToString(),
                    Content = rawDBRow["Content"].ToString(),
                    ContributesToProgress = Convert.ToBoolean(rawDBRow["ContributesToProgress"]),
                    ChatURL = rawDBRow["ChatURL"]?.ToString(),
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

            foreach (var log in logEntries)
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

            List<NpgsqlParameter> parameters = new List<NpgsqlParameter>
            {
                new NpgsqlParameter("@LogId", NpgsqlDbType.Integer) { Value = logId }
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
                Title = rawDBRow["Title"]?.ToString(),
                Content = rawDBRow["Content"].ToString(),
                ContributesToProgress = Convert.ToBoolean(rawDBRow["ContributesToProgress"]),
                ChatURL = rawDBRow["ChatURL"]?.ToString(),
                Tags = new List<Tags>()
            };

            // Get it's tags
            var tagParams = new List<NpgsqlParameter>
            {
                new NpgsqlParameter("@LogId", NpgsqlDbType.Integer) { Value = logId }
            };

            var tagRows = _database.ExecuteQuery(LogEntryQueries.GetLogTagRelationsByLogId, tagParams);

            // Add the tags to the log
            foreach (var tag in tagRows)
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
            List<NpgsqlParameter> parameters = new List<NpgsqlParameter>
            {
                new NpgsqlParameter("@NodeId", nodeId)
            };

            var rawResults = _database.ExecuteQuery(LogEntryQueries.GetLogsByNodeId, parameters);

            return rawResults.Select(row => new LogEntry
            {
                LogId = Convert.ToInt32(row["LogId"]),
                NodeId = Convert.ToInt32(row["NodeId"]),
                EntryDate = Convert.ToDateTime(row["EntryDate"]),
                Title = row["Title"]?.ToString(),
                Content = row["Content"].ToString(),
                ContributesToProgress = Convert.ToBoolean(row["ContributesToProgress"]),
                ChatURL = row["ChatURL"]?.ToString()
            })
            .ToList();
        }

        // === DELETE ===
        public bool DeleteLogEntry(int logId)
        {
            FieldValidator.ValidateId(logId, "Log ID");

            var parameters = new List<NpgsqlParameter>
            {
                new NpgsqlParameter("@LogId", NpgsqlDbType.Integer) { Value = logId }
            };

            // LogEntryTags rows cascade-delete via the FK, no separate cleanup needed.
            int result = _database.ExecuteNonQuery(LogEntryQueries.DeleteLogEntryById, parameters);
            return result > 0;
        }

        public bool DeleteAllLogEntriesByNodeId(int nodeId)
        {
            // Validate Input Fields 
            FieldValidator.ValidateId(nodeId, "KnowledgeNode Id");

            List<NpgsqlParameter> parameters = new List<NpgsqlParameter>
            {
                new NpgsqlParameter("@NodeId", NpgsqlDbType.Integer) { Value = nodeId }
            };

            // DELETE Query + Parameters to delete all LogEntries from a specific Knowledge Node
            int result = _database.ExecuteNonQuery(LogEntryQueries.DeleteAllLogsByNodeId, parameters);

            // Return true to see if DELETE was successful
            return result > 0;
        }

        public bool RemoveAllTagsFromLog(int logId)
        {
            FieldValidator.ValidateId(logId, "Log ID");

            // We still need to fetch all tags that are on this log
            var parameters = new List<NpgsqlParameter> { new NpgsqlParameter("@LogId", NpgsqlDbType.Integer) { Value = logId } };
            var existingRows = _database.ExecuteQuery(LogEntryQueries.GetLogTagRelationsByLogId, parameters);

            if (existingRows.Count == 0) return false;

            // Validate the tagId if so
            // Prepare and execute deletion for each Tag relation
            foreach (var row in existingRows)
            {
                int tagId = Convert.ToInt32(row["TagId"]);
                
                var deleteParams = new List<NpgsqlParameter>
                {
                    new NpgsqlParameter("@LogId", NpgsqlDbType.Integer) { Value = logId },
                    new NpgsqlParameter("@TagId", NpgsqlDbType.Integer) { Value = tagId }
                };

                _database.ExecuteNonQuery(LogEntryQueries.DeleteLogTagRelations, deleteParams);
            }

            return true;
        }

        public bool RemoveSpecificTagsFromLog(int logId, List<int> tagIdsToRemove)
        {
            FieldValidator.ValidateId(logId, "Log ID");

            if (tagIdsToRemove == null || tagIdsToRemove.Count == 0)
                throw new ArgumentException("At least one tag ID must be provided.");

            foreach (int tagId in tagIdsToRemove)
            {
                FieldValidator.ValidateId(tagId, "Tag ID");

                var parameters = new List<NpgsqlParameter>
                {
                    new NpgsqlParameter("@LogId", NpgsqlDbType.Integer) { Value = logId },
                    new NpgsqlParameter("@TagId", NpgsqlDbType.Integer) { Value = tagId }
                };

                _database.ExecuteNonQuery(LogEntryQueries.DeleteLogTagRelations, parameters);
            }

            return true;
        }


        /* ===================== HELPERS ===================== */

        private void BulkInsertLogEntryTagRelation(int logId, List<int> tagIds)
        {
            foreach (int tagId in tagIds)
            {
                FieldValidator.ValidateId(tagId, "Tag ID");

                var parameters = new List<NpgsqlParameter>
                {
                    new NpgsqlParameter("@LogId", NpgsqlDbType.Integer) {Value = logId},
                    new NpgsqlParameter("@TagId", NpgsqlDbType.Integer) {Value = tagId},
                };

                _database.ExecuteNonQuery(LogEntryQueries.InsertLogTagRelation, parameters);
            }
        }
    }
}
