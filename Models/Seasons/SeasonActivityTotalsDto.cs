namespace Knowledge_Center_API.Models.Seasons
{
    public class SeasonActivityTotalsDto
    {
        public int Logs { get; set; }
        public int ActionsCreated { get; set; }
        public int ActionsCompleted { get; set; }
        // Sessions with at least one entry written during the season.
        public int LearnSessions { get; set; }
        public int LearnEntries { get; set; }
    }
}
