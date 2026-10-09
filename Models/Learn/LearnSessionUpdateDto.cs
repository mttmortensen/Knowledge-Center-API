namespace Knowledge_Center_API.Models.Learn
{
    /*
     * PUT /api/learn/sessions/{id}
     *
     * Merge-style: only supplied fields change. An empty Topic clears it, and
     * NodeId = 0 unlinks the Knowledge Node.
     */
    public class LearnSessionUpdateDto
    {
        public string? Title { get; set; }
        public string? Topic { get; set; }
        public int? NodeId { get; set; }
    }
}
