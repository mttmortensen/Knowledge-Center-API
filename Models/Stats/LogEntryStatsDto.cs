namespace Knowledge_Center_API.Models.Stats
{
    public class LogEntryStatsDto
    {
        public int Total { get; set; }

        // "Comic book" stats: how many log entries have a Title set vs left blank.
        public int WithTitle { get; set; }
        public int WithoutTitle { get; set; }
    }
}
