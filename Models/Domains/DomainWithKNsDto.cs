using Knowledge_Center_API.Models.KnowledgeNodes;

namespace Knowledge_Center_API.Models.Domains
{
    public class DomainWithKNsDto
    {
        public int DomainId { get; set; }
        public string DomainName { get; set; }
        public string DomainDescription { get; set; }
        public string DomainStatus { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastUsed { get; set; }
        public DateTime LastUpdated { get; set; }
        public List<KnowledgeNodeInlineDto> KnowledgeNodes { get; set; }
    }
}
