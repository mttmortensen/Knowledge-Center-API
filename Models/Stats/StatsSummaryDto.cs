namespace Knowledge_Center_API.Models.Stats
{
    public class StatsSummaryDto
    {
        public DomainStatsDto Domains { get; set; }
        public KnowledgeNodeStatsDto KnowledgeNodes { get; set; }
        public LogEntryStatsDto LogEntries { get; set; }
        public ActionStatsDto Actions { get; set; }
        public TagStatsDto Tags { get; set; }
        public LogStreakDto LogStreak { get; set; }

        // Dashboard-specific additions: a per-day CTP breakdown for the contribution
        // heatmap and the most-used tags.
        public List<CtpDayCountDto> CtpByDay { get; set; } = new();
        public List<TagCountDto> TopTags { get; set; } = new();
    }
}
