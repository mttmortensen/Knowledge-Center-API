namespace Knowledge_Center_API.Models.DTOs
{
    public class KnowledgeNodeInlineDto
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string NodeType { get; set; }
        public int ConfidenceLevel { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}
