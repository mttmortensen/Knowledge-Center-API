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

        // Distinct calendar days that had at least one log entry, most recent first.
        public static readonly string GetDistinctLogEntryDays = @"
            SELECT DISTINCT ""EntryDate""::date AS ""Day""
            FROM ""LogEntries""
            ORDER BY ""Day"" DESC;
        ";
    }
}
