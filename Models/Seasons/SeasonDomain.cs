namespace Knowledge_Center_API.Models.Seasons
{
    /*
     * A domain's place in a season: Focus or Passive. Domains that aren't in
     * the season are resting.
     */
    public class SeasonDomain
    {
        public int DomainId { get; set; }
        public string DomainName { get; set; }
        public string Role { get; set; }
        public string? Note { get; set; }
        public bool IsArchived { get; set; }
    }
}
