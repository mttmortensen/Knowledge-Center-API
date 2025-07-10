namespace Knowledge_Center_API.Models.DTOs
{
    /*
     * POST /api/logs
     * 
     * This DTO is going to be used in only creating 
     * of a Log Entry as we don't need to pull in the 
     * Tag name/value as the front end will handle this
     */
    public class LogEntryCreateDto
    {
        public int NodeId { get; set; }
        public int TagId { get; set; }
        public string Content { get; set; }
        public bool ContributesToProgress { get; set; }
    }
}
