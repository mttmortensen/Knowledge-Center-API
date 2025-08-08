namespace Knowledge_Center_API.Models.KnowledgeNodes
{
    /*
     * PUT /api/knowledge-nodes
     * 
     * This DTO is going to be used in only updating
     * A KnowledgeNode as we don't need update all the 
     * name/values.
     */
    public class KnowledgeNodeUpdateDto
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Status { get; set; }
        public string? NodeType { get; set; }
        public int? ConfidenceLevel { get; set; }
        public int? DomainId { get; set; }

    }
}
