namespace Knowledge_Center_API.Models.Stats
{
    public class LogStreakDto
    {
        public int CurrentStreak { get; set; }
        public int LongestStreak { get; set; }
        public DateTime? LastEntryDate { get; set; }
    }
}
