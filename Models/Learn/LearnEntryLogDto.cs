namespace Knowledge_Center_API.Models.Learn
{
    /*
     * PUT /api/learn/entries/{id}/log
     *
     * The log made from this entry. Null or 0 clears the link.
     */
    public class LearnEntryLogDto
    {
        public int? LogId { get; set; }
    }
}
