namespace Knowledge_Center_API.Models.Seasons
{
    /*
     * GET /api/seasons/{id}, GET /api/seasons/current
     *
     * Stats is null until the season has started.
     */
    public class SeasonDetailsDto : Season
    {
        public List<SeasonDomain> Domains { get; set; } = new();
        public SeasonStatsDto? Stats { get; set; }
    }
}
