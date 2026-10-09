namespace Knowledge_Center_API.Models.Learn
{
    /*
     * GET /api/learn/questions/open
     *
     * A question entry with no @answers follow-up yet, plus the session it lives in.
     */
    public class LearnOpenQuestionDto
    {
        public int EntryId { get; set; }
        public int SessionId { get; set; }
        public string SessionTitle { get; set; }
        public string Source { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
