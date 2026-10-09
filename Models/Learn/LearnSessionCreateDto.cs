namespace Knowledge_Center_API.Models.Learn
{
    /*
     * POST /api/learn/sessions
     */
    public class LearnSessionCreateDto
    {
        public string Title { get; set; }
        public string? Topic { get; set; }
        public int? NodeId { get; set; }
    }
}
