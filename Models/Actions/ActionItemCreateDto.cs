namespace Knowledge_Center_API.Models.Actions
{
    /*
     * POST /api/actions
     *
     * Status, CreatedAt, and CompletedAt are server-controlled on create —
     * this DTO only carries what the client actually supplies.
     */
    public class ActionItemCreateDto
    {
        public int KnowledgeNodeId { get; set; }
        public string ActionText { get; set; }
    }
}
