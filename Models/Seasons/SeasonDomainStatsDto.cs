namespace Knowledge_Center_API.Models.Seasons
{
    public class SeasonDomainStatsDto
    {
        public int DomainId { get; set; }
        public string DomainName { get; set; }
        // "Focus", "Passive" or "Outside" (not in the season)
        public string Role { get; set; }
        public int Logs { get; set; }
        public int ActionsCreated { get; set; }
        public int ActionsCompleted { get; set; }
        public int LearnEntries { get; set; }
        public int Score { get; set; }
    }
}
