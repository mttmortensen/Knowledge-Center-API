namespace Knowledge_Center_API.Models.DTOs
{

    /*
     * PUT /api/logs
     * 
     * The only prop I'll only to be updated aside from Tags
     * but that's its own route and servicing
     * 
     */
    public class LogEntryUpdateDto
    {
        public string? ChatURL { get; set; }
    }
}
