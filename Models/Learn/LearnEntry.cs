namespace Knowledge_Center_API.Models.Learn
{
    /*
     * One entry in a Learn session's timeline.
     *
     * Source (raw markdown) is the only stored content. EntryType, QuestionStatus,
     * and AnswersEntryId are derived from it on every read by
     * LearnSourceParser, so they can never drift from what was written.
     * LogId is stored separately and only records which log was made from it.
     */
    public class LearnEntry
    {
        public int EntryId { get; set; }
        public int SessionId { get; set; }
        public long Position { get; set; }
        public string Source { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        /// <summary>The log made from this entry, if any. Cleared when that log is deleted.</summary>
        public int? LogId { get; set; }

        // === Derived (read-only) ===

        /// <summary>note, image, code, question or followup.</summary>
        public string EntryType { get; set; }

        /// <summary>open or answered for questions; null otherwise.</summary>
        public string? QuestionStatus { get; set; }

        /// <summary>Target of an @answers(id) follow-up.</summary>
        public int? AnswersEntryId { get; set; }
    }
}
