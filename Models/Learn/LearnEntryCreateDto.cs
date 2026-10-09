namespace Knowledge_Center_API.Models.Learn
{
    /*
     * POST /api/learn/sessions/{sessionId}/entries
     *
     * BeforeEntryId inserts the entry directly above that entry; leave it null to
     * append at the end of the timeline.
     */
    public class LearnEntryCreateDto
    {
        public string Source { get; set; }
        public int? BeforeEntryId { get; set; }
    }
}
