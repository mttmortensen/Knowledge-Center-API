using Knowledge_Center_API.Models.LogEntries;

namespace Knowledge_Center_API.Models.KnowledgeNodes
{
    /* 
     * GET /api/knowledge-nodes/{id}
     * 
     * A KN has many logs, that is my design of this entity. 
     * So to translate that, I added the LogEntryInline Dto 
     * To this new KN Dto. 
     */
    public class KnowledgeNodeDetailsWithLogsDto
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public int DomainId { get; set; }
        public string NodeType { get; set; } 
        public string Description { get; set; }
        public int ConfidenceLevel { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdated { get; set; }

        public List<LogEntryDetailsInlineDto> Logs { get; set; }

    }
}
