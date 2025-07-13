namespace Knowledge_Center_API.Models
{
    public class LogEntry
    {
        public int LogId { get; set; }
        public int NodeId { get; set; }
        public DateTime EntryDate { get; set; }
        public string Content { get; set; }
        public int TagId { get; set; }
        public string Tag { get; set; }
        public bool ContributesToProgress { get; set; }

    }
}
