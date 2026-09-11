namespace Knowledge_Center_API.Models.Stats
{
    public class RecentActionDto
    {
        public int Id { get; set; }
        public int KnowledgeNodeId { get; set; }
        public string KnowledgeNodeTitle { get; set; }
        public string ActionText { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}
