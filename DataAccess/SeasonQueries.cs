namespace Knowledge_Center_API.DataAccess
{
    public class SeasonQueries
    {
        /* ===================== SEASONS ===================== */

        public static readonly string InsertSeason = @"
            INSERT INTO ""Seasons""
                (""Name"", ""Intention"", ""Status"", ""PlannedStart"", ""PlannedEnd"", ""CreatedAt"", ""UpdatedAt"")
            VALUES
                (@Name, @Intention, 'Planned', @PlannedStart, @PlannedEnd, @Now, @Now)
            RETURNING ""SeasonId"";
        ";

        private const string SeasonColumns = @"
            s.""SeasonId"", s.""Name"", s.""Intention"", s.""Status"", s.""PlannedStart"", s.""PlannedEnd"",
            s.""StartedAt"", s.""EndedAt"", s.""Reflection"", s.""OverloadReason"", s.""CreatedAt"", s.""UpdatedAt"",
            (SELECT COUNT(*) FROM ""SeasonDomains"" sd WHERE sd.""SeasonId"" = s.""SeasonId"" AND sd.""Role"" = 'Focus') AS ""FocusCount"",
            (SELECT COUNT(*) FROM ""SeasonDomains"" sd WHERE sd.""SeasonId"" = s.""SeasonId"" AND sd.""Role"" = 'Passive') AS ""PassiveCount""
        ";

        // Active first, then upcoming, then past; newest first within each.
        public static readonly string GetSeasons = $@"
            SELECT {SeasonColumns}
            FROM ""Seasons"" s
            WHERE @Status IS NULL OR s.""Status"" = @Status
            ORDER BY
                CASE s.""Status"" WHEN 'Active' THEN 0 WHEN 'Planned' THEN 1 ELSE 2 END,
                s.""PlannedStart"" DESC;
        ";

        public static readonly string GetSeasonById = $@"
            SELECT {SeasonColumns}
            FROM ""Seasons"" s
            WHERE s.""SeasonId"" = @SeasonId;
        ";

        // Null until a season ends, and for seasons ended before snapshots existed.
        public static readonly string GetStatsSnapshot = @"
            SELECT ""StatsSnapshot""::text AS ""StatsSnapshot"" FROM ""Seasons"" WHERE ""SeasonId"" = @SeasonId;
        ";

        public static readonly string GetActiveSeasonId = @"
            SELECT ""SeasonId"" FROM ""Seasons"" WHERE ""Status"" = 'Active';
        ";

        public static readonly string UpdateSeason = @"
            UPDATE ""Seasons""
            SET ""Name"" = @Name, ""Intention"" = @Intention, ""PlannedStart"" = @PlannedStart,
                ""PlannedEnd"" = @PlannedEnd, ""Reflection"" = @Reflection, ""UpdatedAt"" = @Now
            WHERE ""SeasonId"" = @SeasonId;
        ";

        // SeasonDomains rows cascade-delete via the FK.
        public static readonly string DeleteSeason = @"
            DELETE FROM ""Seasons""
            WHERE ""SeasonId"" = @SeasonId;
        ";

        // Seasons that haven't ended yet whose planned dates overlap the given
        // range (end-exclusive, so one season can start the day another ends).
        public static readonly string CountOverlappingSeasons = @"
            SELECT COUNT(*) FROM ""Seasons""
            WHERE ""Status"" <> 'Ended'
              AND ""SeasonId"" <> @SeasonId
              AND ""PlannedStart"" < @PlannedEnd
              AND @PlannedStart < ""PlannedEnd"";
        ";

        /* ===================== LIFECYCLE ===================== */

        public static readonly string StartSeason = @"
            UPDATE ""Seasons""
            SET ""Status"" = 'Active', ""StartedAt"" = @Now, ""UpdatedAt"" = @Now
            WHERE ""SeasonId"" = @SeasonId AND ""Status"" = 'Planned';
        ";

        // A null reflection keeps whatever was already written. The stats as of
        // @Now are stored with it, so later moves can't change a finished season.
        public static readonly string EndSeason = @"
            UPDATE ""Seasons""
            SET ""Status"" = 'Ended', ""EndedAt"" = @Now, ""UpdatedAt"" = @Now,
                ""Reflection"" = COALESCE(@Reflection, ""Reflection""),
                ""StatsSnapshot"" = @StatsSnapshot
            WHERE ""SeasonId"" = @SeasonId AND ""Status"" = 'Active';
        ";

        // Ends the active season and starts the planned one in a single UPDATE,
        // so there's never a moment with none or two active. The one-active
        // constraint is deferred, so it's checked once both rows have changed.
        // Touches nothing unless both seasons are in the right state; the caller
        // checks for exactly two rows.
        public static readonly string RotateSeason = @"
            UPDATE ""Seasons""
            SET ""Status"" = CASE WHEN ""SeasonId"" = @SeasonId THEN 'Active' ELSE 'Ended' END,
                ""StartedAt"" = CASE WHEN ""SeasonId"" = @SeasonId THEN @Now ELSE ""StartedAt"" END,
                ""EndedAt"" = CASE WHEN ""SeasonId"" = @SeasonId THEN ""EndedAt"" ELSE @Now END,
                ""Reflection"" = CASE WHEN ""SeasonId"" = @SeasonId THEN ""Reflection""
                                      ELSE COALESCE(@Reflection, ""Reflection"") END,
                ""StatsSnapshot"" = CASE WHEN ""SeasonId"" = @SeasonId THEN ""StatsSnapshot"" ELSE @StatsSnapshot END,
                ""UpdatedAt"" = @Now
            WHERE ((""SeasonId"" = @SeasonId AND ""Status"" = 'Planned') OR (""SeasonId"" <> @SeasonId AND ""Status"" = 'Active'))
              AND EXISTS (SELECT 1 FROM ""Seasons"" WHERE ""SeasonId"" = @SeasonId AND ""Status"" = 'Planned')
              AND EXISTS (SELECT 1 FROM ""Seasons"" WHERE ""SeasonId"" <> @SeasonId AND ""Status"" = 'Active');
        ";

        /* ===================== DOMAINS ===================== */

        public static readonly string GetSeasonDomains = @"
            SELECT sd.""DomainId"", d.""DomainName"", sd.""Role"", sd.""Note"", d.""IsArchived""
            FROM ""SeasonDomains"" sd
            JOIN ""Domains"" d ON d.""DomainId"" = sd.""DomainId""
            WHERE sd.""SeasonId"" = @SeasonId
            ORDER BY CASE sd.""Role"" WHEN 'Focus' THEN 0 ELSE 1 END, d.""DomainName"";
        ";

        public static readonly string GetDomainsByIds = @"
            SELECT ""DomainId"", ""DomainName"", ""IsArchived""
            FROM ""Domains""
            WHERE ""DomainId"" = ANY(@DomainIds);
        ";

        // Replaces the whole lineup in one statement. The primary key is
        // deferred, so re-inserting a domain that's being deleted is fine; the
        // delete's snapshot doesn't see the new rows, so only the old ones go.
        public static readonly string ReplaceSeasonDomains = @"
            WITH ""Removed"" AS (
                DELETE FROM ""SeasonDomains"" WHERE ""SeasonId"" = @SeasonId
            ),
            ""Touched"" AS (
                UPDATE ""Seasons""
                SET ""OverloadReason"" = @OverloadReason, ""UpdatedAt"" = @Now
                WHERE ""SeasonId"" = @SeasonId
            )
            INSERT INTO ""SeasonDomains"" (""SeasonId"", ""DomainId"", ""Role"", ""Note"")
            SELECT @SeasonId, t.""DomainId"", t.""Role"", t.""Note""
            FROM unnest(@DomainIds, @Roles, @Notes) AS t(""DomainId"", ""Role"", ""Note"");
        ";

        /* ===================== STATS ===================== */

        // Activity per Knowledge Node in [@From, @To], with the node's current
        // domain. Learn entries count toward the node their session is filed under.
        public static readonly string GetNodeActivity = @"
            WITH ""Activity"" AS (
                SELECT ""NodeId"", 'Log' AS ""Kind"" FROM ""LogEntries""
                WHERE ""EntryDate"" BETWEEN @From AND @To
                UNION ALL
                SELECT ""KnowledgeNodeId"", 'ActionCreated' FROM ""Actions""
                WHERE ""CreatedAt"" BETWEEN @From AND @To
                UNION ALL
                SELECT ""KnowledgeNodeId"", 'ActionCompleted' FROM ""Actions""
                WHERE ""CompletedAt"" BETWEEN @From AND @To
                UNION ALL
                SELECT s.""NodeId"", 'Learn' FROM ""LearnEntries"" e
                JOIN ""LearnSessions"" s ON s.""SessionId"" = e.""SessionId""
                WHERE s.""NodeId"" IS NOT NULL AND e.""CreatedAt"" BETWEEN @From AND @To
            )
            SELECT kn.""Id"" AS ""NodeId"", kn.""Title"", d.""DomainId"", d.""DomainName"",
                   COUNT(*) FILTER (WHERE a.""Kind"" = 'Log') AS ""Logs"",
                   COUNT(*) FILTER (WHERE a.""Kind"" = 'ActionCreated') AS ""ActionsCreated"",
                   COUNT(*) FILTER (WHERE a.""Kind"" = 'ActionCompleted') AS ""ActionsCompleted"",
                   COUNT(*) FILTER (WHERE a.""Kind"" = 'Learn') AS ""LearnEntries""
            FROM ""Activity"" a
            JOIN ""KnowledgeNodes"" kn ON kn.""Id"" = a.""NodeId""
            JOIN ""Domains"" d ON d.""DomainId"" = kn.""DomainId""
            GROUP BY kn.""Id"", kn.""Title"", d.""DomainId"", d.""DomainName"";
        ";

        public static readonly string CountActiveDays = @"
            SELECT COUNT(*) FROM (
                SELECT ""EntryDate""::date FROM ""LogEntries"" WHERE ""EntryDate"" BETWEEN @From AND @To
                UNION
                SELECT ""CreatedAt""::date FROM ""Actions"" WHERE ""CreatedAt"" BETWEEN @From AND @To
                UNION
                SELECT ""CompletedAt""::date FROM ""Actions"" WHERE ""CompletedAt"" BETWEEN @From AND @To
                UNION
                SELECT ""CreatedAt""::date FROM ""LearnEntries"" WHERE ""CreatedAt"" BETWEEN @From AND @To
            ) AS ""Days"";
        ";

        public static readonly string GetLearnSummary = @"
            SELECT COUNT(DISTINCT e.""SessionId"") AS ""LearnSessions"",
                   COUNT(*) FILTER (WHERE s.""NodeId"" IS NULL) AS ""UnfiledLearnEntries""
            FROM ""LearnEntries"" e
            JOIN ""LearnSessions"" s ON s.""SessionId"" = e.""SessionId""
            WHERE e.""CreatedAt"" BETWEEN @From AND @To;
        ";
    }
}
