namespace Knowledge_Center_API.Models.Seasons
{
    /*
     * What a SeasonOverloadAckDto has to contain for the warning at this level.
     */
    public class SeasonOverloadRequirementsDto
    {
        // Text for the confirm button.
        public string ConfirmLabel { get; set; }
        // When set, Phrase must match it (case and spacing don't matter).
        public string? Phrase { get; set; }
        // When above zero, Reason must be at least this many characters.
        public int ReasonMinLength { get; set; }
        public string? ReasonPrompt { get; set; }
        // How long after the warning was issued the ack is accepted.
        public int WaitSeconds { get; set; }
        // Seconds still left on that wait as of this response.
        public int WaitRemainingSeconds { get; set; }
    }
}
