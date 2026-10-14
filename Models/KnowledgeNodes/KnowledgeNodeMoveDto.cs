namespace Knowledge_Center_API.Models.KnowledgeNodes
{
    /*
     * PUT /api/knowledge-nodes/{id}/move
     *
     * Moves a Knowledge Node to another Domain. Its logs and actions
     * come along, since the node keeps its id.
     */
    public class KnowledgeNodeMoveDto
    {
        public int DomainId { get; set; }
    }
}
