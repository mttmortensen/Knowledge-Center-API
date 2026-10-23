namespace Knowledge_Center_API.Models.Seasons
{
    public class SeasonNodeStatsDto
    {
        public int NodeId { get; set; }
        public string Title { get; set; }
        public int DomainId { get; set; }
        public string DomainName { get; set; }
        // "Focus", "Passive" or "Outside" (not in the season)
        public string Role { get; set; }
        public int Logs { get; set; }
        public int ActionsCreated { get; set; }
        public int ActionsCompleted { get; set; }
        public int LearnEntries { get; set; }
        // Logs + completed actions + Learn entries.
        public int Score { get; set; }
    }
}
