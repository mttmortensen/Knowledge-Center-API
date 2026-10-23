namespace Knowledge_Center_API.Models.Seasons
{
    /*
     * GET /api/seasons
     *
     * A season without its domains or stats. Status is stored (Planned, Active,
     * Ended); the day counts and IsOverdue are computed on read.
     */
    public class Season
    {
        public int SeasonId { get; set; }
        public string Name { get; set; }
        public string? Intention { get; set; }
        public string Status { get; set; }
        public DateOnly PlannedStart { get; set; }
        public DateOnly PlannedEnd { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public string? Reflection { get; set; }
        public string? OverloadReason { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public int FocusCount { get; set; }
        public int PassiveCount { get; set; }

        public int DaysPlanned { get; set; }
        // Null until the season has started.
        public int? DaysElapsed { get; set; }
        // Only set while the season is Active; 0 once PlannedEnd has passed.
        public int? DaysRemaining { get; set; }
        // Active and past PlannedEnd. Seasons never end on their own.
        public bool IsOverdue { get; set; }
    }
}
