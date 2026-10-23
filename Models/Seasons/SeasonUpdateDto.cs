namespace Knowledge_Center_API.Models.Seasons
{
    /*
     * PUT /api/seasons/{id}
     *
     * Merge-style: only supplied fields change. An empty Intention or
     * Reflection clears it. Planned dates can't change once a season has ended.
     */
    public class SeasonUpdateDto
    {
        public string? Name { get; set; }
        public string? Intention { get; set; }
        public DateOnly? PlannedStart { get; set; }
        public DateOnly? PlannedEnd { get; set; }
        public string? Reflection { get; set; }
    }
}
