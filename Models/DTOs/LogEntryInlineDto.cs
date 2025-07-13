namespace Knowledge_Center_API.Models.DTOs
{
    public class LogEntryInlineDto
    {
        public int LogId { get; set; }
        public string Content { get; set; }
        public DateTime EntryDate { get; set; }
        public bool ContributesToProgress { get; set; }
    }
}
