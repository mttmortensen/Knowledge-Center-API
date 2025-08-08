namespace Knowledge_Center_API.Models.LogEntries
{
    public class LogEntry
    {
        public int LogId { get; set; }
        public int NodeId { get; set; }
        public DateTime EntryDate { get; set; }
        public string Content { get; set; }
        public List<Tags> Tags { get; set; } = new();
        public bool ContributesToProgress { get; set; } 
        public string? ChatURL { get; set; }
    }
}
