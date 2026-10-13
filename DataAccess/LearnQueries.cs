namespace Knowledge_Center_API.DataAccess
{
    public class LearnQueries
    {
        /* ===================== SESSIONS ===================== */

        public static readonly string InsertSession = @"
            INSERT INTO ""LearnSessions""
                (""Title"", ""Topic"", ""NodeId"", ""CreatedAt"", ""UpdatedAt"")
            VALUES
                (@Title, @Topic, @NodeId, @Now, @Now)
            RETURNING ""SessionId"";
        ";

        // EntryCount comes from SQL; open-question counts need the parser, so the
        // service fills those in from GetQuestionAndFollowUpEntries.
        public static readonly string GetAllSessions = @"
            SELECT s.""SessionId"", s.""Title"", s.""Topic"", s.""NodeId"", kn.""Title"" AS ""NodeTitle"",
                   s.""CreatedAt"", s.""UpdatedAt"",
                   (SELECT COUNT(*) FROM ""LearnEntries"" e WHERE e.""SessionId"" = s.""SessionId"") AS ""EntryCount""
            FROM ""LearnSessions"" s
            LEFT JOIN ""KnowledgeNodes"" kn ON kn.""Id"" = s.""NodeId""
            ORDER BY s.""UpdatedAt"" DESC;
        ";

        public static readonly string GetSessionById = @"
            SELECT s.""SessionId"", s.""Title"", s.""Topic"", s.""NodeId"", kn.""Title"" AS ""NodeTitle"",
                   s.""CreatedAt"", s.""UpdatedAt"",
                   (SELECT COUNT(*) FROM ""LearnEntries"" e WHERE e.""SessionId"" = s.""SessionId"") AS ""EntryCount""
            FROM ""LearnSessions"" s
            LEFT JOIN ""KnowledgeNodes"" kn ON kn.""Id"" = s.""NodeId""
            WHERE s.""SessionId"" = @SessionId;
        ";

        public static readonly string UpdateSession = @"
            UPDATE ""LearnSessions""
            SET ""Title"" = @Title, ""Topic"" = @Topic, ""NodeId"" = @NodeId, ""UpdatedAt"" = @Now
            WHERE ""SessionId"" = @SessionId;
        ";

        public static readonly string TouchSession = @"
            UPDATE ""LearnSessions""
            SET ""UpdatedAt"" = @Now
            WHERE ""SessionId"" = @SessionId;
        ";

        // LearnEntries rows cascade-delete via the FK.
        public static readonly string DeleteSession = @"
            DELETE FROM ""LearnSessions""
            WHERE ""SessionId"" = @SessionId;
        ";

        /* ===================== ENTRIES ===================== */

        public static readonly string GetEntriesBySessionId = @"
            SELECT ""EntryId"", ""SessionId"", ""Position"", ""Source"", ""CreatedAt"", ""UpdatedAt"", ""LogId""
            FROM ""LearnEntries""
            WHERE ""SessionId"" = @SessionId
            ORDER BY ""Position"";
        ";

        public static readonly string GetEntryById = @"
            SELECT ""EntryId"", ""SessionId"", ""Position"", ""Source"", ""CreatedAt"", ""UpdatedAt"", ""LogId""
            FROM ""LearnEntries""
            WHERE ""EntryId"" = @EntryId;
        ";

        // Cheap pre-filter for open-question lookups: only rows that could be a
        // question or a follow-up. The parser makes the final call.
        public static readonly string GetQuestionAndFollowUpEntries = @"
            SELECT e.""EntryId"", e.""SessionId"", e.""Position"", e.""Source"", e.""CreatedAt"", e.""UpdatedAt"", e.""LogId"",
                   s.""Title"" AS ""SessionTitle""
            FROM ""LearnEntries"" e
            INNER JOIN ""LearnSessions"" s ON s.""SessionId"" = e.""SessionId""
            WHERE e.""Source"" ~ '^\s*(\?\?|@answers\()'
              AND (@SessionId::integer IS NULL OR e.""SessionId"" = @SessionId::integer)
            ORDER BY e.""SessionId"", e.""Position"";
        ";

        public static readonly string GetMaxPosition = @"
            SELECT COALESCE(MAX(""Position""), 0)
            FROM ""LearnEntries""
            WHERE ""SessionId"" = @SessionId;
        ";

        public static readonly string GetPreviousPosition = @"
            SELECT COALESCE(MAX(""Position""), 0)
            FROM ""LearnEntries""
            WHERE ""SessionId"" = @SessionId AND ""Position"" < @Position;
        ";

        // Spreads a session's entries back out to even gaps, keeping their order.
        // Runs as one statement, which the deferred unique constraint allows.
        public static readonly string RenumberSession = @"
            UPDATE ""LearnEntries"" e
            SET ""Position"" = r.rn * @Gap
            FROM (
                SELECT ""EntryId"", ROW_NUMBER() OVER (ORDER BY ""Position"") AS rn
                FROM ""LearnEntries""
                WHERE ""SessionId"" = @SessionId
            ) r
            WHERE e.""EntryId"" = r.""EntryId"";
        ";

        public static readonly string InsertEntry = @"
            INSERT INTO ""LearnEntries""
                (""SessionId"", ""Position"", ""Source"", ""CreatedAt"", ""UpdatedAt"")
            VALUES
                (@SessionId, @Position, @Source, @Now, @Now)
            RETURNING ""EntryId"";
        ";

        public static readonly string UpdateEntrySource = @"
            UPDATE ""LearnEntries""
            SET ""Source"" = @Source, ""UpdatedAt"" = @Now
            WHERE ""EntryId"" = @EntryId;
        ";

        // Not a content edit, so UpdatedAt is left alone.
        public static readonly string UpdateEntryLog = @"
            UPDATE ""LearnEntries""
            SET ""LogId"" = @LogId
            WHERE ""EntryId"" = @EntryId;
        ";

        public static readonly string DeleteEntry = @"
            DELETE FROM ""LearnEntries""
            WHERE ""EntryId"" = @EntryId;
        ";
    }
}
