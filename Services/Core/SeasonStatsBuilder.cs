using Knowledge_Center_API.Models.Seasons;

namespace Knowledge_Center_API.Services.Core
{
    /*
     * Turns per-Knowledge-Node activity counts for a season's window into the
     * season's stats. Pure, so the database path and demo mode share it.
     */
    public static class SeasonStatsBuilder
    {
        public const int TopNodeCount = 5;

        public const string Outside = "Outside";

        public sealed record NodeActivity(
            int NodeId, string Title, int DomainId, string DomainName,
            int Logs, int ActionsCreated, int ActionsCompleted, int LearnEntries);

        // Created actions are shown but not scored: writing a to-do isn't the work.
        public static int Score(int logs, int actionsCompleted, int learnEntries) =>
            logs + actionsCompleted + learnEntries;

        public static SeasonStatsDto Build(
            DateTime from, DateTime to,
            IEnumerable<NodeActivity> nodeActivity,
            IReadOnlyCollection<SeasonDomain> members,
            int activeDays, int learnSessions, int unfiledLearnEntries)
        {
            var roles = members.ToDictionary(m => m.DomainId, m => m.Role);
            string RoleOf(int domainId) => roles.GetValueOrDefault(domainId, Outside);

            var nodes = nodeActivity.Select(n => new SeasonNodeStatsDto
            {
                NodeId = n.NodeId,
                Title = n.Title,
                DomainId = n.DomainId,
                DomainName = n.DomainName,
                Role = RoleOf(n.DomainId),
                Logs = n.Logs,
                ActionsCreated = n.ActionsCreated,
                ActionsCompleted = n.ActionsCompleted,
                LearnEntries = n.LearnEntries,
                Score = Score(n.Logs, n.ActionsCompleted, n.LearnEntries)
            }).ToList();

            // Every season domain is listed, even with no activity: a Focus
            // domain sitting at zero is the most useful thing this can show.
            var byDomain = members
                .Select(m => new SeasonDomainStatsDto { DomainId = m.DomainId, DomainName = m.DomainName, Role = m.Role })
                .ToDictionary(d => d.DomainId);

            foreach (var node in nodes)
            {
                if (!byDomain.TryGetValue(node.DomainId, out var domain))
                {
                    domain = new SeasonDomainStatsDto { DomainId = node.DomainId, DomainName = node.DomainName, Role = Outside };
                    byDomain[node.DomainId] = domain;
                }

                domain.Logs += node.Logs;
                domain.ActionsCreated += node.ActionsCreated;
                domain.ActionsCompleted += node.ActionsCompleted;
                domain.LearnEntries += node.LearnEntries;
                domain.Score += node.Score;
            }

            int totalScore = nodes.Sum(n => n.Score);
            int focusScore = nodes.Where(n => n.Role == "Focus").Sum(n => n.Score);

            return new SeasonStatsDto
            {
                From = from,
                To = to,
                ActiveDays = activeDays,
                FocusShare = totalScore == 0 ? 0 : Math.Round(100.0 * focusScore / totalScore, 1),
                Totals = new SeasonActivityTotalsDto
                {
                    Logs = nodes.Sum(n => n.Logs),
                    ActionsCreated = nodes.Sum(n => n.ActionsCreated),
                    ActionsCompleted = nodes.Sum(n => n.ActionsCompleted),
                    LearnSessions = learnSessions,
                    LearnEntries = nodes.Sum(n => n.LearnEntries) + unfiledLearnEntries
                },
                ByDomain = byDomain.Values
                    .OrderBy(d => RoleRank(d.Role))
                    .ThenByDescending(d => d.Score)
                    .ThenBy(d => d.DomainName, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                TopKnowledgeNodes = nodes
                    .Where(n => n.Score > 0)
                    .OrderByDescending(n => n.Score)
                    .ThenByDescending(n => n.Logs)
                    .ThenBy(n => n.Title, StringComparer.OrdinalIgnoreCase)
                    .Take(TopNodeCount)
                    .ToList(),
                UnfiledLearnEntries = unfiledLearnEntries
            };
        }

        private static int RoleRank(string role) => role switch
        {
            "Focus" => 0,
            "Passive" => 1,
            _ => 2
        };
    }
}
