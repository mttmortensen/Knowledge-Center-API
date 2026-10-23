namespace Knowledge_Center_API.Models.Seasons
{
    /*
     * 409 body from PUT /api/seasons/{id}/domains when the lineup adds a third
     * (or later) Focus domain. Nothing was saved. Each extra Focus domain raises
     * Level, and with it what Requires asks for before the lineup is accepted.
     */
    public class SeasonOverloadWarningDto
    {
        public string Warning { get; set; } = "FocusOverload";
        public int Level { get; set; }
        public int FocusCount { get; set; }
        public List<string> FocusDomains { get; set; } = new();
        public string Title { get; set; }
        public string Message { get; set; }
        public List<string> Consequences { get; set; } = new();
        public string Suggestion { get; set; }
        public SeasonOverloadRequirementsDto Requires { get; set; }

        public string ChallengeToken { get; set; }
        public DateTime ChallengeExpiresAt { get; set; }

        // Set when an OverloadAck was sent but didn't satisfy Requires.
        public string? AckError { get; set; }
    }
}
