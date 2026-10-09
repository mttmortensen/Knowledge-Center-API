namespace Knowledge_Center_API.Models.Learn
{
    /*
     * GET /api/learn/sessions
     *
     * A Learn session (notebook) without its entries. EntryCount and
     * OpenQuestionCount are computed on read, never stored.
     */
    public class LearnSession
    {
        public int SessionId { get; set; }
        public string Title { get; set; }
        public string? Topic { get; set; }
        public int? NodeId { get; set; }
        public string? NodeTitle { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int EntryCount { get; set; }
        public int OpenQuestionCount { get; set; }
    }
}
