namespace Knowledge_Center_API.Models.DTOs
{
    /*
     * GET /api/knowledge-nodes/{id}
     * 
     * As explaining in KNLogs Dto, this is the inline LogEntry 
     * that will only be attached and appear in a GET by Id logic for 
     * KNs. 
     */
    public class LogEntryInlineDto
    {
        public int LogId { get; set; }
        public string Content { get; set; }
        public DateTime EntryDate { get; set; }
        public bool ContributesToProgress { get; set; }
    }
}
