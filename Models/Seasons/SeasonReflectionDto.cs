namespace Knowledge_Center_API.Models.Seasons
{
    /*
     * POST /api/seasons/{id}/end, POST /api/seasons/{id}/rotate
     *
     * The reflection for the season being ended. Optional; it can be written
     * later with PUT /api/seasons/{id}.
     */
    public class SeasonReflectionDto
    {
        public string? Reflection { get; set; }
    }
}
