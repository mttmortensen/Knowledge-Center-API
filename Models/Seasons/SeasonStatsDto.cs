namespace Knowledge_Center_API.Models.Seasons
{
    /*
     * Activity from StartedAt to EndedAt (or now, while Active). While a
     * season is active, work counts under the domain its Knowledge Node is in
     * today. An ended season keeps the stats it ended with (stored as
     * StatsSnapshot), unless it ended before snapshots existed.
     */
    public class SeasonStatsDto
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }

        // Days with any log, action or Learn entry.
        public int ActiveDays { get; set; }

        // Share (0-100) of scored activity that landed in Focus domains. Learn
        // entries in sessions without a Knowledge Node aren't scored.
        public double FocusShare { get; set; }

        public SeasonActivityTotalsDto Totals { get; set; } = new();
        public List<SeasonDomainStatsDto> ByDomain { get; set; } = new();
        public List<SeasonNodeStatsDto> TopKnowledgeNodes { get; set; } = new();

        // Learn entries whose session isn't filed under a Knowledge Node.
        public int UnfiledLearnEntries { get; set; }
    }
}
