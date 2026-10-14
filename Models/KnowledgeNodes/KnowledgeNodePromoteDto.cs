namespace Knowledge_Center_API.Models.KnowledgeNodes
{
    /*
     * POST /api/knowledge-nodes/{id}/promote      (body optional)
     *
     * Names for what the promotion produces. A blank DomainName names the new
     * domain after the node; a blank KnowledgeNodeTitle keeps the node's title.
     * The node (with its logs and actions) moves under the domain either way.
     */
    public class KnowledgeNodePromoteDto
    {
        public string? DomainName { get; set; }
        public string? KnowledgeNodeTitle { get; set; }
    }
}
