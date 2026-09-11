using Knowledge_Center_API.DataAccess;
using Knowledge_Center_API.Models.Stats;
using Npgsql;
using NpgsqlTypes;

namespace Knowledge_Center_API.Services.Core
{
    public class StatsService
    {
        private const int HeatmapDays = 371; // ~53 weeks, matches the frontend's contribution calendar window
        private const int TopTagsLimit = 8;

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
                TopTags = GetTopTags(TopTagsLimit)
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
            int withTitle = _database.ExecuteScalar<int>(StatsQueries.CountLogEntriesWithTitle, null);

            return new LogEntryStatsDto
            {
                Total = total,
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
                .Select(row => StreakCalculator.ToDateOnly(row["Day"]))
                .OrderByDescending(day => day)
                .ToList();

            return StreakCalculator.Compute(days);
        }

        // Npgsql maps a Postgres "date" column to System.DateOnly by default, but
        // Database.ExecuteQuery hands back plain object values — guard the DateTime
        // conversion path too in case that mapping ever changes.
        private static DateOnly ToDateOnly(object value) => StreakCalculator.ToDateOnly(value);

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
    }
}
