using Knowledge_Center_API.DataAccess;
using Knowledge_Center_API.Models.Learn;
using Knowledge_Center_API.Services.Validation;
using Npgsql;
using NpgsqlTypes;

namespace Knowledge_Center_API.Services.Core
{
    public class LearnService
    {
        // New entries land this far apart. An insert takes the midpoint of its
        // neighbours, so one gap absorbs ~16 inserts at the same spot before the
        // session is renumbered back to even gaps.
        public const long PositionGap = 65536;

        private const int MaxSourceLength = 200_000;

        private readonly Database _database;
        private readonly KnowledgeNodeService _knService;

        public LearnService(Database database, KnowledgeNodeService knService)
        {
            _database = database;
            _knService = knService;
        }

        /* ===================== SESSIONS ===================== */

        // === CREATE ===
        public int CreateSession(LearnSessionCreateDto dto)
        {
            FieldValidator.ValidateRequiredString(dto.Title, "Title", 200);
            FieldValidator.ValidateOptionalString(dto.Topic, "Topic", 200);
            ValidateOptionalNode(dto.NodeId);

            var parameters = new List<NpgsqlParameter>
            {
                new NpgsqlParameter("@Title", NpgsqlDbType.Varchar, 200) { Value = dto.Title.Trim() },
                new NpgsqlParameter("@Topic", NpgsqlDbType.Varchar, 200) { Value = string.IsNullOrWhiteSpace(dto.Topic) ? DBNull.Value : dto.Topic.Trim() },
                new NpgsqlParameter("@NodeId", NpgsqlDbType.Integer) { Value = dto.NodeId.HasValue ? dto.NodeId.Value : DBNull.Value },
                new NpgsqlParameter("@Now", NpgsqlDbType.Timestamp) { Value = AppClock.Now }
            };

            return _database.ExecuteScalar<int>(LearnQueries.InsertSession, parameters);
        }

        // === READ ===
        public List<LearnSession> GetAllSessions()
        {
            var rows = _database.ExecuteQuery(LearnQueries.GetAllSessions, null);
            var sessions = rows.Select(ConvertDBRowToSession).ToList();

            var openCounts = GetOpenQuestions(null)
                .GroupBy(q => q.SessionId)
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var session in sessions)
                session.OpenQuestionCount = openCounts.GetValueOrDefault(session.SessionId);

            return sessions;
        }

        public LearnSessionDetailsDto? GetSessionWithEntries(int sessionId)
        {
            FieldValidator.ValidateId(sessionId, "Session ID");

            var rows = _database.ExecuteQuery(LearnQueries.GetSessionById, SessionIdParam(sessionId));
            if (rows.Count == 0) return null;

            var summary = ConvertDBRowToSession(rows[0]);
            var details = new LearnSessionDetailsDto
            {
                SessionId = summary.SessionId,
                Title = summary.Title,
                Topic = summary.Topic,
                NodeId = summary.NodeId,
                NodeTitle = summary.NodeTitle,
                CreatedAt = summary.CreatedAt,
                UpdatedAt = summary.UpdatedAt,
                EntryCount = summary.EntryCount,
                Entries = GetEntriesForSession(sessionId)
            };

            details.OpenQuestionCount = details.Entries.Count(e => e.QuestionStatus == "open");
            return details;
        }

        public bool SessionExists(int sessionId)
        {
            FieldValidator.ValidateId(sessionId, "Session ID");
            return _database.ExecuteQuery(LearnQueries.GetSessionById, SessionIdParam(sessionId)).Count > 0;
        }

        // === UPDATE ===
        public bool UpdateSession(int sessionId, LearnSessionUpdateDto dto)
        {
            var existing = GetSessionWithEntries(sessionId);
            if (existing == null) return false;

            // Merge-style: only supplied fields replace what's stored. Empty Topic
            // clears it; NodeId 0 unlinks the node.
            string newTitle = dto.Title != null ? dto.Title.Trim() : existing.Title;
            string? newTopic = dto.Topic != null ? dto.Topic.Trim() : existing.Topic;
            int? newNodeId = dto.NodeId.HasValue
                ? (dto.NodeId.Value == 0 ? null : dto.NodeId.Value)
                : existing.NodeId;

            FieldValidator.ValidateRequiredString(newTitle, "Title", 200);
            FieldValidator.ValidateOptionalString(newTopic, "Topic", 200);
            ValidateOptionalNode(newNodeId);

            var parameters = new List<NpgsqlParameter>
            {
                new NpgsqlParameter("@SessionId", NpgsqlDbType.Integer) { Value = sessionId },
                new NpgsqlParameter("@Title", NpgsqlDbType.Varchar, 200) { Value = newTitle },
                new NpgsqlParameter("@Topic", NpgsqlDbType.Varchar, 200) { Value = string.IsNullOrWhiteSpace(newTopic) ? DBNull.Value : newTopic },
                new NpgsqlParameter("@NodeId", NpgsqlDbType.Integer) { Value = newNodeId.HasValue ? newNodeId.Value : DBNull.Value },
                new NpgsqlParameter("@Now", NpgsqlDbType.Timestamp) { Value = AppClock.Now }
            };

            return _database.ExecuteNonQuery(LearnQueries.UpdateSession, parameters) > 0;
        }

        // === DELETE ===
        public bool DeleteSession(int sessionId)
        {
            FieldValidator.ValidateId(sessionId, "Session ID");
            return _database.ExecuteNonQuery(LearnQueries.DeleteSession, SessionIdParam(sessionId)) > 0;
        }

        /* ===================== ENTRIES ===================== */

        // === CREATE ===

        /*
         * Inserts an entry directly above BeforeEntryId, or at the end when that's
         * null. Only the new row is written unless the gap above BeforeEntryId has
         * run out, in which case this one session is renumbered first.
         */
        public int CreateEntry(int sessionId, LearnEntryCreateDto dto)
        {
            FieldValidator.ValidateId(sessionId, "Session ID");
            FieldValidator.ValidateRequiredString(dto.Source, "Source", MaxSourceLength);
            ValidateReferences(sessionId, null, dto.Source, previousSource: null);

            if (dto.BeforeEntryId.HasValue)
            {
                var before = GetEntryById(dto.BeforeEntryId.Value);
                if (before == null || before.SessionId != sessionId)
                    throw new ArgumentException($"Entry {dto.BeforeEntryId} is not in this session.");
            }

            // Two inserts racing for the same gap can pick the same midpoint; the
            // unique (SessionId, Position) constraint rejects the loser, so retry once.
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    long position = NextPosition(sessionId, dto.BeforeEntryId);

                    var parameters = new List<NpgsqlParameter>
                    {
                        new NpgsqlParameter("@SessionId", NpgsqlDbType.Integer) { Value = sessionId },
                        new NpgsqlParameter("@Position", NpgsqlDbType.Bigint) { Value = position },
                        new NpgsqlParameter("@Source", NpgsqlDbType.Text) { Value = dto.Source },
                        new NpgsqlParameter("@Now", NpgsqlDbType.Timestamp) { Value = AppClock.Now }
                    };

                    int newId = _database.ExecuteScalar<int>(LearnQueries.InsertEntry, parameters);
                    TouchSession(sessionId);
                    return newId;
                }
                catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation && attempt == 0)
                {
                    // fall through and recompute against the winner's position
                }
            }
        }

        // === READ ===
        public LearnEntry? GetEntryById(int entryId)
        {
            FieldValidator.ValidateId(entryId, "Entry ID");

            var parameters = new List<NpgsqlParameter>
            {
                new NpgsqlParameter("@EntryId", NpgsqlDbType.Integer) { Value = entryId }
            };

            var rows = _database.ExecuteQuery(LearnQueries.GetEntryById, parameters);
            if (rows.Count == 0) return null;

            // Derived fields depend on the rest of the session (answered status),
            // so annotate within the full session and pick this entry back out.
            int sessionId = Convert.ToInt32(rows[0]["SessionId"]);
            return GetEntriesForSession(sessionId).FirstOrDefault(e => e.EntryId == entryId);
        }

        public List<LearnEntry> GetEntriesForSession(int sessionId)
        {
            var rows = _database.ExecuteQuery(LearnQueries.GetEntriesBySessionId, SessionIdParam(sessionId));
            var entries = rows.Select(ConvertDBRowToEntry).ToList();
            LearnSourceParser.Annotate(entries);
            return entries;
        }

        /*
         * Questions with no follow-up pointing at them, across every session or
         * just one.
         */
        public List<LearnOpenQuestionDto> GetOpenQuestions(int? sessionId)
        {
            var parameters = new List<NpgsqlParameter>
            {
                new NpgsqlParameter("@SessionId", NpgsqlDbType.Integer) { Value = sessionId.HasValue ? sessionId.Value : DBNull.Value }
            };

            var rows = _database.ExecuteQuery(LearnQueries.GetQuestionAndFollowUpEntries, parameters);

            var result = new List<LearnOpenQuestionDto>();
            foreach (var group in rows.GroupBy(r => Convert.ToInt32(r["SessionId"])))
            {
                var entries = group.Select(ConvertDBRowToEntry).ToList();
                LearnSourceParser.Annotate(entries);

                string sessionTitle = group.First()["SessionTitle"].ToString();
                result.AddRange(entries
                    .Where(e => e.QuestionStatus == "open")
                    .Select(e => new LearnOpenQuestionDto
                    {
                        EntryId = e.EntryId,
                        SessionId = e.SessionId,
                        SessionTitle = sessionTitle,
                        Source = e.Source,
                        CreatedAt = e.CreatedAt
                    }));
            }

            return result;
        }

        public string? ExportSessionMarkdown(int sessionId)
        {
            var session = GetSessionWithEntries(sessionId);
            if (session == null) return null;

            return LearnMarkdownExporter.Build(session);
        }

        // === UPDATE ===
        public bool UpdateEntrySource(int entryId, LearnEntryUpdateDto dto)
        {
            var existing = GetEntryById(entryId);
            if (existing == null) return false;

            FieldValidator.ValidateRequiredString(dto.Source, "Source", MaxSourceLength);
            ValidateReferences(existing.SessionId, entryId, dto.Source, existing.Source);

            var parameters = new List<NpgsqlParameter>
            {
                new NpgsqlParameter("@EntryId", NpgsqlDbType.Integer) { Value = entryId },
                new NpgsqlParameter("@Source", NpgsqlDbType.Text) { Value = dto.Source },
                new NpgsqlParameter("@Now", NpgsqlDbType.Timestamp) { Value = AppClock.Now }
            };

            bool updated = _database.ExecuteNonQuery(LearnQueries.UpdateEntrySource, parameters) > 0;
            if (updated) TouchSession(existing.SessionId);
            return updated;
        }

        // === DELETE ===

        /*
         * Follow-ups that answered this entry are left alone: their source still
         * says @answers(id), and the client shows them as answering a deleted entry.
         */
        public bool DeleteEntry(int entryId)
        {
            var existing = GetEntryById(entryId);
            if (existing == null) return false;

            var parameters = new List<NpgsqlParameter>
            {
                new NpgsqlParameter("@EntryId", NpgsqlDbType.Integer) { Value = entryId }
            };

            bool deleted = _database.ExecuteNonQuery(LearnQueries.DeleteEntry, parameters) > 0;
            if (deleted) TouchSession(existing.SessionId);
            return deleted;
        }

        /* ===================== HELPERS ===================== */

        private long NextPosition(int sessionId, int? beforeEntryId)
        {
            if (!beforeEntryId.HasValue)
            {
                long max = _database.ExecuteScalar<long>(LearnQueries.GetMaxPosition, SessionIdParam(sessionId));
                return max + PositionGap;
            }

            for (int pass = 0; pass < 2; pass++)
            {
                var before = GetEntryById(beforeEntryId.Value)!;

                var parameters = SessionIdParam(sessionId);
                parameters.Add(new NpgsqlParameter("@Position", NpgsqlDbType.Bigint) { Value = before.Position });
                long previous = _database.ExecuteScalar<long>(LearnQueries.GetPreviousPosition, parameters);

                if (before.Position - previous >= 2)
                    return previous + (before.Position - previous) / 2;

                // Gap exhausted: spread the session out again and recompute.
                var renumberParams = SessionIdParam(sessionId);
                renumberParams.Add(new NpgsqlParameter("@Gap", NpgsqlDbType.Bigint) { Value = PositionGap });
                _database.ExecuteNonQuery(LearnQueries.RenumberSession, renumberParams);
            }

            throw new InvalidOperationException("Could not find a free position after renumbering.");
        }

        /*
         * @answers(id) must point at another entry in the same session. A reference
         * that hasn't changed since the entry was last saved is not rechecked, so a
         * follow-up whose question was deleted can still be edited.
         */
        private void ValidateReferences(int sessionId, int? entryId, string source, string? previousSource)
        {
            var parsed = LearnSourceParser.Parse(source);
            var previous = previousSource != null ? LearnSourceParser.Parse(previousSource) : null;

            if (parsed.AnswersEntryId.HasValue && parsed.AnswersEntryId != previous?.AnswersEntryId)
            {
                int targetId = parsed.AnswersEntryId.Value;

                if (targetId == entryId)
                    throw new ArgumentException("An entry cannot answer itself.");

                var target = targetId > 0 ? GetEntryById(targetId) : null;
                if (target == null)
                    throw new ArgumentException($"@answers({targetId}): entry {targetId} does not exist.");

                if (target.SessionId != sessionId)
                    throw new ArgumentException($"@answers({targetId}): entry {targetId} belongs to a different session.");
            }
        }

        private void ValidateOptionalNode(int? nodeId)
        {
            if (!nodeId.HasValue) return;

            FieldValidator.ValidateId(nodeId.Value, "KnowledgeNode ID");
            if (!_knService.KnowledgeNodeExists(nodeId.Value))
                throw new ArgumentException($"KnowledgeNode with ID {nodeId} not found.");
        }

        private void TouchSession(int sessionId)
        {
            var parameters = SessionIdParam(sessionId);
            parameters.Add(new NpgsqlParameter("@Now", NpgsqlDbType.Timestamp) { Value = AppClock.Now });
            _database.ExecuteNonQuery(LearnQueries.TouchSession, parameters);
        }

        private static List<NpgsqlParameter> SessionIdParam(int sessionId) => new()
        {
            new NpgsqlParameter("@SessionId", NpgsqlDbType.Integer) { Value = sessionId }
        };

        private static LearnSession ConvertDBRowToSession(Dictionary<string, object> row) => new()
        {
            SessionId = Convert.ToInt32(row["SessionId"]),
            Title = row["Title"].ToString(),
            Topic = row["Topic"] is DBNull ? null : row["Topic"].ToString(),
            NodeId = row["NodeId"] is DBNull ? null : Convert.ToInt32(row["NodeId"]),
            NodeTitle = row["NodeTitle"] is DBNull ? null : row["NodeTitle"].ToString(),
            CreatedAt = Convert.ToDateTime(row["CreatedAt"]),
            UpdatedAt = Convert.ToDateTime(row["UpdatedAt"]),
            EntryCount = Convert.ToInt32(row["EntryCount"])
        };

        private static LearnEntry ConvertDBRowToEntry(Dictionary<string, object> row) => new()
        {
            EntryId = Convert.ToInt32(row["EntryId"]),
            SessionId = Convert.ToInt32(row["SessionId"]),
            Position = Convert.ToInt64(row["Position"]),
            Source = row["Source"].ToString(),
            CreatedAt = Convert.ToDateTime(row["CreatedAt"]),
            UpdatedAt = Convert.ToDateTime(row["UpdatedAt"])
        };
    }
}
