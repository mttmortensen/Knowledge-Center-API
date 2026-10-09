namespace Knowledge_Center_API.Models.Learn
{
    /*
     * GET /api/learn/sessions/{id}
     *
     * A session plus all of its entries in timeline (Position) order.
     */
    public class LearnSessionDetailsDto : LearnSession
    {
        public List<LearnEntry> Entries { get; set; } = new();
    }
}
