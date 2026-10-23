using System.Text.RegularExpressions;
using Knowledge_Center_API.Models.Seasons;

namespace Knowledge_Center_API.Services.Core
{
    /*
     * The warning shown when a season gets more than two Focus domains. It's a
     * warning, not a hard limit, but each extra Focus domain makes confirming it
     * harder: a click, then a typed phrase, then a written reason, then a longer
     * phrase naming every focus and a wait before it can be confirmed.
     *
     * All of the wording lives here so it can be tuned in one place. Pure: no
     * database, no clock; the challenge token is handled by SeasonOverloadToken.
     */
    public static class SeasonOverloadPolicy
    {
        // A season's normal capacity. Passive domains never count toward it.
        public const int MaxFocusDomains = 2;

        public const int HighestLevel = 4;

        private const string Suggestion =
            "Move some of these to Passive. They stay in the season and you can still work on them; " +
            "they just stop competing for your main attention. Or put them in a planned next season " +
            "so they're the first thing you rotate to.";

        // 0 when the lineup is within capacity, otherwise 1-4.
        public static int LevelFor(int focusCount) =>
            focusCount <= MaxFocusDomains ? 0 : Math.Min(focusCount - MaxFocusDomains, HighestLevel);

        /*
         * The warning for this Focus lineup, or null when it's within capacity.
         * ChallengeToken and ChallengeExpiresAt are left for the caller to fill.
         */
        public static SeasonOverloadWarningDto? BuildWarning(IReadOnlyCollection<string> focusDomainNames)
        {
            int count = focusDomainNames.Count;
            int level = LevelFor(count);
            if (level == 0)
                return null;

            var names = focusDomainNames.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();

            var warning = new SeasonOverloadWarningDto
            {
                Level = level,
                FocusCount = count,
                FocusDomains = names,
                Suggestion = Suggestion
            };

            switch (level)
            {
                case 1:
                    warning.Title = "Three focus interests is already a stretch.";
                    warning.Message =
                        "A season works because one or two interests get enough attention to actually move. " +
                        "A third splits the same evenings and weekends three ways, so each one gets about a third " +
                        "of you, and the season is much more likely to end with nothing that feels finished.";
                    warning.Consequences = new()
                    {
                        "Each focus gets about a third of your attention instead of half",
                        "Progress on all of them slows down",
                        "Higher odds of ending the season feeling behind on everything"
                    };
                    warning.Requires = new SeasonOverloadRequirementsDto
                    {
                        ConfirmLabel = "I understand. Add it anyway."
                    };
                    break;

                case 2:
                    warning.Title = "Four focus interests isn't a season. It's a to-do list.";
                    warning.Message =
                        "Every switch between interests costs warm-up time before you're doing real work again. " +
                        "With four, a large share of your time goes to remembering where you left off. This is the " +
                        "'busy, but nothing is moving' pattern Seasons exists to break.";
                    warning.Consequences = new()
                    {
                        "Context switching eats the time you meant to spend learning",
                        "Shallow progress everywhere, depth nowhere",
                        "Interests start to feel like chores you're failing at",
                        "More stress, less enjoyment from the things you picked for fun"
                    };
                    warning.Requires = new SeasonOverloadRequirementsDto
                    {
                        ConfirmLabel = "Add a fourth focus",
                        Phrase = "I accept slower progress on everything"
                    };
                    break;

                case 3:
                    warning.Title = "Five focus interests is how burnout starts.";
                    warning.Message =
                        "There isn't enough time in a normal week to make noticeable progress on five things " +
                        "alongside work and the rest of life. The usual result is stress, guilt over whichever ones " +
                        "got skipped this week, and eventually dropping all of them. If they all genuinely matter, " +
                        "they'll still matter next season. Rotate them in instead.";
                    warning.Consequences = new()
                    {
                        "Constant guilt over whatever got skipped this week",
                        "Stress that bleeds into work, sleep and downtime",
                        "A real risk of burning out and abandoning all of them",
                        "Season stats spread too thin to tell you anything useful"
                    };
                    warning.Requires = new SeasonOverloadRequirementsDto
                    {
                        ConfirmLabel = "Spread myself across five",
                        Phrase = "I am choosing to spread myself thin and I accept the burnout risk",
                        ReasonMinLength = 80,
                        ReasonPrompt = "Why can't one of these wait for a future season?"
                    };
                    break;

                default:
                    warning.Title = $"{count} focus interests. You've rebuilt the problem Seasons was meant to fix.";
                    warning.Message =
                        "This is no longer a season. It's everything at once with a start and end date. Take a minute " +
                        "before going through with it: pick the one or two you'd be most disappointed to have made no " +
                        "progress on by the end of this season, and move the rest to Passive or a planned next season. " +
                        "That's not giving them up. It's giving them a turn.";
                    warning.Consequences = new()
                    {
                        "None of these will get the attention you're hoping to give them",
                        "Near-certain overwhelm, and a good chance of burnout",
                        "Stress and guilt that make the interests feel like obligations",
                        "Likely to end the season with less done than with two focuses",
                        "Season stats will mostly measure how thinly you were spread"
                    };
                    warning.Requires = new SeasonOverloadRequirementsDto
                    {
                        ConfirmLabel = $"Focus on all {count} anyway",
                        Phrase = $"I am choosing to focus on {JoinNames(names)} all at once, knowing it will likely burn me out",
                        ReasonMinLength = 150,
                        ReasonPrompt = "What makes this season different enough to justify all of these at once?",
                        WaitSeconds = 60
                    };
                    break;
            }

            warning.Requires.WaitRemainingSeconds = warning.Requires.WaitSeconds;
            return warning;
        }

        /*
         * Checks the phrase and reason of an ack against the warning's
         * requirements. Returns null when they're satisfied, otherwise what's
         * wrong. The challenge token and wait are checked separately.
         */
        public static string? CheckAck(SeasonOverloadWarningDto warning, SeasonOverloadAckDto ack)
        {
            var requires = warning.Requires;

            if (requires.Phrase != null && Normalize(ack.Phrase) != Normalize(requires.Phrase))
                return "Type the phrase exactly as shown to confirm.";

            if (requires.ReasonMinLength > 0)
            {
                int length = (ack.Reason ?? string.Empty).Trim().Length;
                if (length < requires.ReasonMinLength)
                    return $"Write at least {requires.ReasonMinLength} characters on why ({length} so far).";
            }

            return null;
        }

        // "A", "A and B", "A, B and C"
        private static string JoinNames(IReadOnlyList<string> names) =>
            names.Count == 1
                ? names[0]
                : string.Join(", ", names.Take(names.Count - 1)) + " and " + names[^1];

        // Case and spacing don't matter, and a trailing full stop is forgiven.
        private static string Normalize(string? text) =>
            Regex.Replace((text ?? string.Empty).Trim().TrimEnd('.'), @"\s+", " ").ToLowerInvariant();
    }
}
