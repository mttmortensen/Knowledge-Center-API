namespace Knowledge_Center_API.Models.Seasons
{
    /*
     * The confirmation for a SeasonOverloadWarningDto. Phrase and Reason are
     * only checked when the warning's Requires asks for them.
     */
    public class SeasonOverloadAckDto
    {
        public string ChallengeToken { get; set; }
        public string? Phrase { get; set; }
        public string? Reason { get; set; }
    }
}
