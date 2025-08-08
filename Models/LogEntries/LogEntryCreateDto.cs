namespace Knowledge_Center_API.Models.LogEntries
{
    /*
     * POST /api/logs
     * 
     * This DTO is used for creating a new LogEntry.
     * The front-end supplies a list of TagIds.
     */
    public class LogEntryCreateDto
    {
        public int Id { get; set; }
        public int NodeId { get; set; }
        public List<int> TagIds { get; set; } = new();
        public string Content { get; set; }
        public bool ContributesToProgress { get; set; }
        public DateTime EntryDate { get; set; }
        public string? ChatURL { get; set; }
    }
}
