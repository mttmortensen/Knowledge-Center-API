namespace Knowledge_Center_API.Models.Seasons
{
    /*
     * POST /api/seasons
     *
     * A new season starts out Planned with no domains; the lineup is set with
     * PUT /api/seasons/{id}/domains.
     */
    public class SeasonCreateDto
    {
        public string Name { get; set; }
        public string? Intention { get; set; }
        public DateOnly PlannedStart { get; set; }
        public DateOnly PlannedEnd { get; set; }
    }
}
