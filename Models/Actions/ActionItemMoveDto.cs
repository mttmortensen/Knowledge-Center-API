namespace Knowledge_Center_API.Models.Actions
{
    /*
     * PUT /api/actions/{id}/move
     *
     * Moves an action to another Knowledge Node. Status, dates and the
     * follow-up note are kept as they are.
     */
    public class ActionItemMoveDto
    {
        public int KnowledgeNodeId { get; set; }
    }
}
