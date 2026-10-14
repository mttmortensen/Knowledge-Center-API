namespace Knowledge_Center_API.Models.Actions
{
    /*
     * PUT /api/actions/{id}/complete      (body optional)
     * PUT /api/actions/{id}/follow-up
     *
     * A note on what happened after the action was closed. On complete, a
     * blank FollowUp leaves any existing note alone; on the follow-up
     * endpoint, a blank FollowUp clears it.
     */
    public class ActionItemFollowUpDto
    {
        public string? FollowUp { get; set; }
    }
}
