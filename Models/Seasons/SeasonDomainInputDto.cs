namespace Knowledge_Center_API.Models.Seasons
{
    /*
     * One domain in a PUT /api/seasons/{id}/domains lineup.
     */
    public class SeasonDomainInputDto
    {
        public int DomainId { get; set; }
        // "Focus" or "Passive"
        public string Role { get; set; }
        public string? Note { get; set; }
    }
}
