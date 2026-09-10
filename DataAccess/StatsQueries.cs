namespace Knowledge_Center_API.DataAccess
{
    public static class StatsQueries
    {
        public static readonly string CountDomainsTotal = @"
            SELECT COUNT(*) FROM ""Domains"";
        ";

        public static readonly string CountDomainsArchived = @"
            SELECT COUNT(*) FROM ""Domains""
            WHERE ""IsArchived"" = TRUE;
        ";

        public static readonly string CountKnowledgeNodesTotal = @"
            SELECT COUNT(*) FROM ""KnowledgeNodes"";
        ";

        public static readonly string CountKnowledgeNodesArchived = @"
            SELECT COUNT(*) FROM ""KnowledgeNodes""
            WHERE ""IsArchived"" = TRUE;
        ";

        public static readonly string CountLogEntriesTotal = @"
            SELECT COUNT(*) FROM ""LogEntries"";
        ";

        public static readonly string CountLogEntriesContributingToProgress = @"
            SELECT COUNT(*) FROM ""LogEntries""
            WHERE ""ContributesToProgress"" = TRUE;
        ";

        public static readonly string CountLogEntriesWithTitle = @"
            SELECT COUNT(*) FROM ""LogEntries""
            WHERE ""Title"" IS NOT NULL AND TRIM(""Title"") <> '';
        ";

        public static readonly string CountActionsTotal = @"
            SELECT COUNT(*) FROM ""Actions"";
        ";

        public static readonly string CountActionsOpen = @"
            SELECT COUNT(*) FROM ""Actions""
            WHERE ""Status"" = 'Open';
        ";

        public static readonly string CountActionsCompleted = @"
            SELECT COUNT(*) FROM ""Actions""
            WHERE ""Status"" = 'Completed';
        ";

        public static readonly string CountTagsTotal = @"
            SELECT COUNT(*) FROM ""Tags"";
        ";

        // Distinct calendar days that had at least one log entry (any entry, regardless
        // of ContributesToProgress — that flag marks per-node progress like a commit,
        // not daily-logging-habit activity), most recent first.
        public static readonly string GetDistinctLogEntryDays = @"
            SELECT DISTINCT ""EntryDate""::date AS ""Day""
            FROM ""LogEntries""
            ORDER BY ""Day"" DESC;
        ";

        // Powers the dashboard's GitHub-style contribution heatmap: one row per day
        // that had at least one CTP entry, bounded to the heatmap's display window.
        public static readonly string GetCtpCountsByDay = @"
            SELECT DATE(""EntryDate"") AS ""Date"", COUNT(*) AS ""Count""
            FROM ""LogEntries""
            WHERE ""ContributesToProgress"" = TRUE AND ""EntryDate"" >= @Since
            GROUP BY DATE(""EntryDate"")
            ORDER BY DATE(""EntryDate"");
        ";

        public static readonly string GetTopTags = @"
            SELECT t.""TagId"", t.""Name"", COUNT(lt.""LogId"") AS ""Count""
            FROM ""Tags"" t
            INNER JOIN ""LogEntryTags"" lt ON lt.""TagId"" = t.""TagId""
            GROUP BY t.""TagId"", t.""Name""
            ORDER BY COUNT(lt.""LogId"") DESC, t.""Name""
            LIMIT @Limit;
        ";

        public static readonly string GetRecentActions = @"
            SELECT * FROM ""Actions""
            ORDER BY ""CreatedAt"" DESC
            LIMIT @Limit;
        ";
    }
}
