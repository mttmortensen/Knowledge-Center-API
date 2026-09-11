namespace Knowledge_Center_API.Models.LogEntries
{
    /*
     * PUT /api/logs/{id}
     *
     * Updates the editable body of an existing entry (title, content).
     * Tags and ChatURL stay on their own dedicated routes.
     */
    public class LogEntryContentUpdateDto
    {
        public string? Title { get; set; }
        public string? Content { get; set; }
    }
}
