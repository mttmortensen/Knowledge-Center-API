namespace Knowledge_Center_API.Models.Actions
{
    /*
     * PUT /api/actions/{id}
     *
     * Only ActionText can be edited this way — Status changes go through
     * the dedicated complete/reopen endpoints instead.
     */
    public class ActionItemUpdateDto
    {
        public int Id { get; set; }
        public string? ActionText { get; set; }
    }
}
