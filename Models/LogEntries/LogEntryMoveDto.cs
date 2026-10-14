namespace Knowledge_Center_API.Models.LogEntries
{
    /*
     * PUT /api/logs/{id}/move
     *
     * Moves a log entry to another Knowledge Node. Its tags and any Learn
     * entry that links to it come along, since the log keeps its id.
     */
    public class LogEntryMoveDto
    {
        public int NodeId { get; set; }
    }
}
