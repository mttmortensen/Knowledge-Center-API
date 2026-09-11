using Knowledge_Center_API.Models.Stats;

namespace Knowledge_Center_API.Services.Core
{
    // Shared run-length logic for "current/longest streak of consecutive days"
    // stats, used for both LogEntries and Actions.
    public static class StreakCalculator
    {
        // ~53 weeks, matches the frontend's contribution calendar window. Shared
        // default so LogEntries and Actions heatmaps stay in sync without each
        // resource redefining its own magic number.
        public const int DefaultHeatmapDays = 371;

        public static LogStreakDto Compute(List<DateOnly> daysDescending)
        {
            if (daysDescending.Count == 0)
                return new LogStreakDto { CurrentStreak = 0, LongestStreak = 0, LastEntryDate = null };

            int longestStreak = 1;
            int runLength = 1;
            for (int i = 1; i < daysDescending.Count; i++)
            {
                if (daysDescending[i - 1].DayNumber - daysDescending[i].DayNumber == 1)
                {
                    runLength++;
                }
                else
                {
                    longestStreak = Math.Max(longestStreak, runLength);
                    runLength = 1;
                }
            }
            longestStreak = Math.Max(longestStreak, runLength);

            // Current streak only holds if the most recent day was today or
            // yesterday — anything older means the streak already broke, even though
            // there's still a run of consecutive days sitting further back in history.
            var today = DateOnly.FromDateTime(DateTime.Now);
            int currentStreak = 0;
            if (daysDescending[0] == today || daysDescending[0] == today.AddDays(-1))
            {
                currentStreak = 1;
                for (int i = 1; i < daysDescending.Count; i++)
                {
                    if (daysDescending[i - 1].DayNumber - daysDescending[i].DayNumber == 1)
                        currentStreak++;
                    else
                        break;
                }
            }

            return new LogStreakDto
            {
                CurrentStreak = currentStreak,
                LongestStreak = longestStreak,
                LastEntryDate = daysDescending[0].ToDateTime(TimeOnly.MinValue)
            };
        }

        public static DateOnly ToDateOnly(object value)
        {
            if (value is DateOnly dateOnly)
                return dateOnly;

            return DateOnly.FromDateTime(Convert.ToDateTime(value));
        }
    }
}
