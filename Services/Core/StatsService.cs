using Knowledge_Center_API.DataAccess;
using Knowledge_Center_API.Models.Actions;
using Knowledge_Center_API.Models.Stats;
using Npgsql;
using NpgsqlTypes;

namespace Knowledge_Center_API.Services.Core
{
    public class StatsService
    {
        private const int HeatmapDays = 371; // ~53 weeks, matches the frontend's contribution calendar window
        private const int TopTagsLimit = 8;
        private const int RecentActionsLimit = 8;

        private readonly Database _database;

        public StatsService(Database database)
        {
            _database = database;
        }

        public StatsSummaryDto GetSummary()
        {
            return new StatsSummaryDto
            {
                Domains = GetDomainStats(),
                KnowledgeNodes = GetKnowledgeNodeStats(),
                LogEntries = GetLogEntryStats(),
                Actions = GetActionStats(),
                Tags = GetTagStats(),
                LogStreak = GetLogStreak(),
                CtpByDay = GetCtpByDay(),
                TopTags = GetTopTags(TopTagsLimit),
                RecentActions = GetRecentActions(RecentActionsLimit)
            };
        }

        private DomainStatsDto GetDomainStats()
        {
            int total = _database.ExecuteScalar<int>(StatsQueries.CountDomainsTotal, null);
            int archived = _database.ExecuteScalar<int>(StatsQueries.CountDomainsArchived, null);

            return new DomainStatsDto
            {
                Total = total,
                Archived = archived,
                Active = total - archived
            };
        }

        private KnowledgeNodeStatsDto GetKnowledgeNodeStats()
        {
            int total = _database.ExecuteScalar<int>(StatsQueries.CountKnowledgeNodesTotal, null);
            int archived = _database.ExecuteScalar<int>(StatsQueries.CountKnowledgeNodesArchived, null);

            return new KnowledgeNodeStatsDto
            {
                Total = total,
                Archived = archived,
                Active = total - archived
            };
        }

        private LogEntryStatsDto GetLogEntryStats()
        {
            int total = _database.ExecuteScalar<int>(StatsQueries.CountLogEntriesTotal, null);
            int contributing = _database.ExecuteScalar<int>(StatsQueries.CountLogEntriesContributingToProgress, null);
            int withTitle = _database.ExecuteScalar<int>(StatsQueries.CountLogEntriesWithTitle, null);

            return new LogEntryStatsDto
            {
                Total = total,
                ContributingToProgress = contributing,
                WithTitle = withTitle,
                WithoutTitle = total - withTitle
            };
        }

        private ActionStatsDto GetActionStats()
        {
            return new ActionStatsDto
            {
                Total = _database.ExecuteScalar<int>(StatsQueries.CountActionsTotal, null),
                Open = _database.ExecuteScalar<int>(StatsQueries.CountActionsOpen, null),
                Completed = _database.ExecuteScalar<int>(StatsQueries.CountActionsCompleted, null)
            };
        }

        private TagStatsDto GetTagStats()
        {
            return new TagStatsDto
            {
                Total = _database.ExecuteScalar<int>(StatsQueries.CountTagsTotal, null)
            };
        }

        private LogStreakDto GetLogStreak()
        {
            var rawDBResults = _database.ExecuteQuery(StatsQueries.GetDistinctLogEntryDays, null);

            var days = rawDBResults
                .Select(row => ToDateOnly(row["Day"]))
                .OrderByDescending(day => day)
                .ToList();

            if (days.Count == 0)
            {
                return new LogStreakDto { CurrentStreak = 0, LongestStreak = 0, LastEntryDate = null };
            }

            int longestStreak = 1;
            int runLength = 1;
            for (int i = 1; i < days.Count; i++)
            {
                if (days[i - 1].DayNumber - days[i].DayNumber == 1)
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

            // Current streak only holds if the most recent logged day was today or
            // yesterday — anything older means the streak already broke, even though
            // there's still a run of consecutive days sitting further back in history.
            var today = DateOnly.FromDateTime(DateTime.Now);
            int currentStreak = 0;
            if (days[0] == today || days[0] == today.AddDays(-1))
            {
                currentStreak = 1;
                for (int i = 1; i < days.Count; i++)
                {
                    if (days[i - 1].DayNumber - days[i].DayNumber == 1)
                        currentStreak++;
                    else
                        break;
                }
            }

            return new LogStreakDto
            {
                CurrentStreak = currentStreak,
                LongestStreak = longestStreak,
                LastEntryDate = days[0].ToDateTime(TimeOnly.MinValue)
            };
        }

        // Npgsql maps a Postgres "date" column to System.DateOnly by default, but
        // Database.ExecuteQuery hands back plain object values — guard the DateTime
        // conversion path too in case that mapping ever changes.
        private static DateOnly ToDateOnly(object value)
        {
            if (value is DateOnly dateOnly)
                return dateOnly;

            return DateOnly.FromDateTime(Convert.ToDateTime(value));
        }

        private List<CtpDayCountDto> GetCtpByDay()
        {
            var parameters = new List<NpgsqlParameter>
            {
                new NpgsqlParameter("@Since", NpgsqlDbType.Timestamp) { Value = DateTime.Now.Date.AddDays(-HeatmapDays) }
            };

            var rawDBResults = _database.ExecuteQuery(StatsQueries.GetCtpCountsByDay, parameters);

            return rawDBResults.Select(row => new CtpDayCountDto
            {
                Date = ToDateOnly(row["Date"]).ToDateTime(TimeOnly.MinValue),
                Count = Convert.ToInt32(row["Count"])
            }).ToList();
        }

        private List<TagCountDto> GetTopTags(int limit)
        {
            var parameters = new List<NpgsqlParameter>
            {
                new NpgsqlParameter("@Limit", NpgsqlDbType.Integer) { Value = limit }
            };

            var rawDBResults = _database.ExecuteQuery(StatsQueries.GetTopTags, parameters);

            return rawDBResults.Select(row => new TagCountDto
            {
                TagId = Convert.ToInt32(row["TagId"]),
                Name = row["Name"].ToString(),
                Count = Convert.ToInt32(row["Count"])
            }).ToList();
        }

        private List<ActionItem> GetRecentActions(int limit)
        {
            var parameters = new List<NpgsqlParameter>
            {
                new NpgsqlParameter("@Limit", NpgsqlDbType.Integer) { Value = limit }
            };

            var rawDBResults = _database.ExecuteQuery(StatsQueries.GetRecentActions, parameters);
            return rawDBResults.Select(ConvertDBRowToActionItem).ToList();
        }

        private ActionItem ConvertDBRowToActionItem(Dictionary<string, object> rawDBRow)
        {
            return new ActionItem
            {
                Id = Convert.ToInt32(rawDBRow["Id"]),
                KnowledgeNodeId = Convert.ToInt32(rawDBRow["KnowledgeNodeId"]),
                ActionText = rawDBRow["ActionText"].ToString(),
                Status = rawDBRow["Status"].ToString(),
                CreatedAt = Convert.ToDateTime(rawDBRow["CreatedAt"]),
                CompletedAt = rawDBRow["CompletedAt"] == null || rawDBRow["CompletedAt"] == DBNull.Value
                    ? (DateTime?)null
                    : Convert.ToDateTime(rawDBRow["CompletedAt"])
            };
        }
    }
}
