using System.Text.RegularExpressions;
using Knowledge_Center_API.Models.Learn;

namespace Knowledge_Center_API.Services.Core
{
    /*
     * Derives a Learn entry's type and links from its raw markdown source.
     *
     * The web client has a matching parser (src/lib/learn/parse.ts) for live
     * rendering while typing; this one is authoritative for validation, open
     * questions and export. Keep the two rule sets in step.
     *
     *   @answers(12) ...   follow-up to entry 12
     *   ?? ...             question
     *   ```...             code block
     *   ![caption](url)    image (when that is the whole entry)
     *   anything else      note
     *
     * Learn is deliberately separate from Logs: nothing here reads or writes them.
     */
    public static class LearnSourceParser
    {
        public const string Note = "note";
        public const string Image = "image";
        public const string Code = "code";
        public const string Question = "question";
        public const string FollowUp = "followup";

        private static readonly Regex AnswersPattern = new(@"^\s*@answers\((\d+)\)", RegexOptions.Compiled);
        private static readonly Regex ImagePattern = new(@"^\s*!\[[^\]]*\]\([^)\s]+\)\s*$", RegexOptions.Compiled);

        public static ParsedLearnSource Parse(string? source)
        {
            string text = source ?? string.Empty;

            Match answers = AnswersPattern.Match(text);
            if (answers.Success && int.TryParse(answers.Groups[1].Value, out int targetId))
                return new ParsedLearnSource(FollowUp, targetId);

            string trimmed = text.TrimStart();

            if (trimmed.StartsWith("??"))
                return new ParsedLearnSource(Question, null);

            if (trimmed.StartsWith("```"))
                return new ParsedLearnSource(Code, null);

            if (ImagePattern.IsMatch(text))
                return new ParsedLearnSource(Image, null);

            return new ParsedLearnSource(Note, null);
        }

        /*
         * Fills in the derived fields on one session's entries. A question is
         * answered when any follow-up in the same session points at it.
         */
        public static void Annotate(List<LearnEntry> entries)
        {
            var parsed = entries.ToDictionary(e => e.EntryId, e => Parse(e.Source));

            var answeredIds = parsed.Values
                .Where(p => p.AnswersEntryId.HasValue)
                .Select(p => p.AnswersEntryId!.Value)
                .ToHashSet();

            foreach (var entry in entries)
            {
                var p = parsed[entry.EntryId];
                entry.EntryType = p.EntryType;
                entry.AnswersEntryId = p.AnswersEntryId;
                entry.QuestionStatus = p.EntryType == Question
                    ? (answeredIds.Contains(entry.EntryId) ? "answered" : "open")
                    : null;
            }
        }
    }

    public record ParsedLearnSource(string EntryType, int? AnswersEntryId);
}
