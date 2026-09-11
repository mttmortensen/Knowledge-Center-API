using Knowledge_Center_API.DataAccess.Demo;
using Knowledge_Center_API.Models.Stats;
using Knowledge_Center_API.Services.Core;
using Knowledge_Center_API.Services.Security;
using Microsoft.AspNetCore.Mvc;

namespace Knowledge_Center_API.Controllers
{
    /// <summary>
    /// Aggregate dashboard/reporting stats across Domains, KnowledgeNodes, LogEntries, Actions, and Tags.
    /// Requires JWT authentication.
    /// </summary>
    [RequireToken]
    [ApiController]
    [Route("api/stats")]
    public class StatsController : ControllerBase
    {
        private readonly StatsService _statsService;

        public StatsController(StatsService statsService)
        {
            _statsService = statsService;
        }

        /// <summary>
        /// Retrieves aggregate counts and log streak stats for use in a dashboard or report.
        /// </summary>
        [HttpGet]
        public IActionResult GetSummary()
        {
            if (User.HasClaim("demo", "true"))
            {
                return Ok(BuildDemoSummary());
            }

            var summary = _statsService.GetSummary();
            return Ok(summary);
        }

        private static StatsSummaryDto BuildDemoSummary()
        {
            int domainTotal = DemoData.Domains.Count;
            int domainArchived = DemoData.Domains.Count(d => d.IsArchived);

            int knTotal = DemoData.KnowledgeNodes.Count;
            int knArchived = DemoData.KnowledgeNodes.Count(kn => kn.IsArchived);

            int logTotal = DemoData.LogEntries.Count;
            int logWithTitle = DemoData.LogEntries.Count(l => !string.IsNullOrWhiteSpace(l.Title));

            int actionTotal = DemoData.Actions.Count;
            int actionOpen = DemoData.Actions.Count(a => a.Status == "Open");
            int actionCompleted = DemoData.Actions.Count(a => a.Status == "Completed");

            return new StatsSummaryDto
            {
                Domains = new DomainStatsDto
                {
                    Total = domainTotal,
                    Active = domainTotal - domainArchived,
                    Archived = domainArchived
                },
                KnowledgeNodes = new KnowledgeNodeStatsDto
                {
                    Total = knTotal,
                    Active = knTotal - knArchived,
                    Archived = knArchived
                },
                LogEntries = new LogEntryStatsDto
                {
                    Total = logTotal,
                    WithTitle = logWithTitle,
                    WithoutTitle = logTotal - logWithTitle
                },
                Actions = new ActionStatsDto
                {
                    Total = actionTotal,
                    Open = actionOpen,
                    Completed = actionCompleted
                },
                Tags = new TagStatsDto { Total = DemoData.Tags.Count },
                LogStreak = new LogStreakDto { CurrentStreak = 0, LongestStreak = 0, LastEntryDate = null },
                CtpByDay = DemoData.LogEntries
                    .GroupBy(log => log.EntryDate.Date)
                    .Select(group => new CtpDayCountDto { Date = group.Key, Count = group.Count() })
                    .ToList(),
                TopTags = DemoData.Tags
                    .Select(tag => new TagCountDto { TagId = tag.TagId, Name = tag.Name, Count = 1 })
                    .ToList()
            };
        }
    }
}
