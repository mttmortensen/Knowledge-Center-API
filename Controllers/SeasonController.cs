using Knowledge_Center_API.DataAccess.Demo;
using Knowledge_Center_API.Models.Seasons;
using Knowledge_Center_API.Services.Core;
using Knowledge_Center_API.Services.Security;
using Microsoft.AspNetCore.Mvc;

namespace Knowledge_Center_API.Controllers
{
    /// <summary>
    /// Seasons: time-boxed focus periods. A season gives existing Domains a role,
    /// Focus (the main attention, normally one or two) or Passive (still active,
    /// not the priority). Domains left out are resting. At most one season is
    /// active at a time, and a season only ends when it's ended on purpose.
    /// Requires JWT authentication.
    /// </summary>
    [RequireToken]
    [ApiController]
    [Route("api/seasons")]
    public class SeasonController : ControllerBase
    {
        private readonly SeasonService _seasonService;

        public SeasonController(SeasonService seasonService)
        {
            _seasonService = seasonService;
        }

        /* ===================== SEASONS ===================== */

        /// <summary>
        /// Retrieves seasons without their domains or stats: the active one first,
        /// then planned, then ended.
        /// </summary>
        /// <param name="status">Optional filter: Planned, Active or Ended.</param>
        /// <response code="200">List of seasons.</response>
        /// <response code="400">Unknown status.</response>
        [HttpGet]
        public IActionResult GetSeasons([FromQuery] string? status)
        {
            if (User.HasClaim("demo", "true"))
                return Ok(DemoData.Seasons.Where(s => status == null || s.Status == status));

            try
            {
                return Ok(_seasonService.GetSeasons(status));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Retrieves the active season with its domains and stats.
        /// </summary>
        /// <response code="200">The active season.</response>
        /// <response code="204">No season is active.</response>
        [HttpGet("current")]
        public IActionResult GetCurrent()
        {
            if (User.HasClaim("demo", "true"))
            {
                var demoActive = DemoData.Seasons.FirstOrDefault(s => s.Status == "Active");
                return demoActive == null ? NoContent() : Ok(BuildDemoDetails(demoActive.SeasonId));
            }

            var season = _seasonService.GetCurrentSeasonDetails();
            return season == null ? NoContent() : Ok(season);
        }

        /// <summary>
        /// Retrieves a season with its domains and, once it has started, its stats:
        /// activity by domain and role, the most-worked Knowledge Nodes, and how much
        /// of it went to Focus domains.
        /// </summary>
        /// <param name="id">The season ID.</param>
        /// <response code="200">Season found.</response>
        /// <response code="404">Season not found.</response>
        [HttpGet("{id}")]
        public IActionResult GetSeason(int id)
        {
            if (User.HasClaim("demo", "true"))
            {
                var demo = BuildDemoDetails(id);
                if (demo == null)
                    return NotFound(new { message = $"Demo season with ID {id} not found." });

                return Ok(demo);
            }

            var season = _seasonService.GetSeasonDetails(id);
            if (season == null)
                return NotFound(new { message = $"Season with ID {id} not found." });

            return Ok(season);
        }

        /// <summary>
        /// Creates a planned season with no domains. Planned dates can't overlap a
        /// season that hasn't ended.
        /// </summary>
        /// <param name="dto">Name, optional intention, and planned start and end dates.</param>
        /// <response code="201">Season created.</response>
        /// <response code="400">Invalid input or overlapping dates.</response>
        /// <response code="429">Rate limit exceeded.</response>
        [HttpPost]
        public IActionResult CreateSeason([FromBody] SeasonCreateDto dto)
        {
            var demoResult = AuthHelper.HandleDemoCreate(User, () => new SeasonDetailsDto
            {
                SeasonId = 9999,
                Name = dto.Name,
                Intention = dto.Intention,
                Status = "Planned",
                PlannedStart = dto.PlannedStart,
                PlannedEnd = dto.PlannedEnd,
                CreatedAt = AppClock.Now,
                UpdatedAt = AppClock.Now
            });

            if (demoResult != null)
                return demoResult;

            if (!RateLimiter.IsAllowed(HttpContext))
                return StatusCode(429, new { message = "Rate limit exceeded. Try again later." });

            try
            {
                int newId = _seasonService.CreateSeason(dto);
                if (newId <= 0)
                    return StatusCode(500, new { message = "Failed to create season." });

                return CreatedAtAction(nameof(GetSeason), new { id = newId }, _seasonService.GetSeasonDetails(newId));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "An unexpected error occurred." });
            }
        }

        /// <summary>
        /// Updates a season's name, intention, planned dates and/or reflection. Only
        /// supplied fields change; an empty Intention or Reflection clears it.
        /// </summary>
        /// <param name="id">The season ID.</param>
        /// <param name="dto">Fields to update.</param>
        /// <response code="200">Season updated; returns the season with domains and stats.</response>
        /// <response code="400">Invalid input or overlapping dates.</response>
        /// <response code="404">Season not found.</response>
        /// <response code="409">Planned dates of an ended season can't change.</response>
        [HttpPut("{id}")]
        public IActionResult UpdateSeason(int id, [FromBody] SeasonUpdateDto dto)
        {
            if (User.HasClaim("demo", "true"))
                return StatusCode(403, new { message = "Update operations are disabled in demo mode." });

            if (!RateLimiter.IsAllowed(HttpContext))
                return StatusCode(429, new { message = "Rate limit exceeded. Try again later." });

            try
            {
                if (!_seasonService.UpdateSeason(id, dto))
                    return NotFound(new { message = $"Season with ID {id} was not found." });

                return Ok(_seasonService.GetSeasonDetails(id));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "An unexpected error occurred." });
            }
        }

        /// <summary>
        /// Deletes a season. Its domains are untouched.
        /// </summary>
        /// <param name="id">The season ID.</param>
        /// <response code="204">Season deleted.</response>
        /// <response code="404">Season not found.</response>
        [HttpDelete("{id}")]
        public IActionResult DeleteSeason(int id)
        {
            if (User.HasClaim("demo", "true"))
                return StatusCode(403, new { message = "Deleting operations are disabled in demo mode." });

            if (!RateLimiter.IsAllowed(HttpContext))
                return StatusCode(429, new { message = "Rate limit exceeded. Try again later." });

            try
            {
                if (!_seasonService.DeleteSeason(id))
                    return NotFound(new { message = $"Season with ID {id} was not found." });

                return NoContent();
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "An unexpected error occurred." });
            }
        }

        /* ===================== DOMAINS ===================== */

        /// <summary>
        /// Replaces the season's lineup of Focus and Passive domains. Adding a Focus
        /// domain past the second isn't recommended: the first request returns 409
        /// with a warning and saves nothing. Resend the same lineup with OverloadAck
        /// (the warning's ChallengeToken plus whatever its Requires asks for) to go
        /// ahead. Each extra Focus domain asks for more.
        /// </summary>
        /// <param name="id">The season ID.</param>
        /// <param name="dto">The full lineup, and the overload confirmation when needed.</param>
        /// <response code="200">Lineup saved; returns the season with domains and stats.</response>
        /// <response code="400">Invalid input, e.g. a missing or archived domain.</response>
        /// <response code="404">Season not found.</response>
        /// <response code="409">A focus overload warning to confirm (body has Warning = "FocusOverload"), or the season has ended.</response>
        [HttpPut("{id}/domains")]
        public IActionResult SetDomains(int id, [FromBody] SeasonDomainsSetDto dto)
        {
            if (User.HasClaim("demo", "true"))
                return StatusCode(403, new { message = "Update operations are disabled in demo mode." });

            if (!RateLimiter.IsAllowed(HttpContext))
                return StatusCode(429, new { message = "Rate limit exceeded. Try again later." });

            try
            {
                var result = _seasonService.SetDomains(id, dto, DateTime.UtcNow);
                if (result == null)
                    return NotFound(new { message = $"Season with ID {id} was not found." });

                if (result.Warning != null)
                    return Conflict(result.Warning);

                return Ok(_seasonService.GetSeasonDetails(id));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "An unexpected error occurred." });
            }
        }

        /* ===================== LIFECYCLE ===================== */

        /// <summary>
        /// Starts a planned season. It needs at least one Focus domain, and no other
        /// season can be active.
        /// </summary>
        /// <param name="id">The season ID.</param>
        /// <response code="200">Season started; returns it with domains and stats.</response>
        /// <response code="404">Season not found.</response>
        /// <response code="409">Not planned, no Focus domain, or another season is active.</response>
        [HttpPost("{id}/start")]
        public IActionResult StartSeason(int id) =>
            RunLifecycle(id, () => _seasonService.StartSeason(id));

        /// <summary>
        /// Ends the active season, optionally recording a reflection on how it went.
        /// </summary>
        /// <param name="id">The season ID.</param>
        /// <param name="dto">Optional reflection.</param>
        /// <response code="200">Season ended; returns it with domains and final stats.</response>
        /// <response code="404">Season not found.</response>
        /// <response code="409">The season isn't active.</response>
        [HttpPost("{id}/end")]
        public IActionResult EndSeason(int id, [FromBody] SeasonReflectionDto? dto) =>
            RunLifecycle(id, () => _seasonService.EndSeason(id, dto?.Reflection));

        /// <summary>
        /// Rotates to this planned season: ends the active one (with an optional
        /// reflection) and starts this one in a single step.
        /// </summary>
        /// <param name="id">The planned season to start.</param>
        /// <param name="dto">Optional reflection for the season being ended.</param>
        /// <response code="200">Rotated; returns the newly active season.</response>
        /// <response code="404">Season not found.</response>
        /// <response code="409">Not planned, no Focus domain, or no season is active.</response>
        [HttpPost("{id}/rotate")]
        public IActionResult RotateSeason(int id, [FromBody] SeasonReflectionDto? dto) =>
            RunLifecycle(id, () => _seasonService.RotateTo(id, dto?.Reflection));

        /* ===================== HELPERS ===================== */

        private IActionResult RunLifecycle(int id, Func<bool> transition)
        {
            if (User.HasClaim("demo", "true"))
                return StatusCode(403, new { message = "Update operations are disabled in demo mode." });

            if (!RateLimiter.IsAllowed(HttpContext))
                return StatusCode(429, new { message = "Rate limit exceeded. Try again later." });

            try
            {
                if (!transition())
                    return NotFound(new { message = $"Season with ID {id} was not found." });

                return Ok(_seasonService.GetSeasonDetails(id));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "An unexpected error occurred." });
            }
        }

        private static SeasonDetailsDto? BuildDemoDetails(int id)
        {
            var s = DemoData.Seasons.FirstOrDefault(x => x.SeasonId == id);
            if (s == null) return null;

            var domains = DemoData.SeasonDomains.GetValueOrDefault(id, new List<SeasonDomain>());

            return new SeasonDetailsDto
            {
                SeasonId = s.SeasonId,
                Name = s.Name,
                Intention = s.Intention,
                Status = s.Status,
                PlannedStart = s.PlannedStart,
                PlannedEnd = s.PlannedEnd,
                StartedAt = s.StartedAt,
                EndedAt = s.EndedAt,
                Reflection = s.Reflection,
                OverloadReason = s.OverloadReason,
                CreatedAt = s.CreatedAt,
                UpdatedAt = s.UpdatedAt,
                FocusCount = s.FocusCount,
                PassiveCount = s.PassiveCount,
                DaysPlanned = s.DaysPlanned,
                DaysElapsed = s.DaysElapsed,
                DaysRemaining = s.DaysRemaining,
                IsOverdue = s.IsOverdue,
                Domains = domains,
                Stats = s.StartedAt == null ? null : BuildDemoStats(s.StartedAt.Value, s.EndedAt ?? AppClock.Now, domains)
            };
        }

        // The same stats the database path produces, computed over the in-memory demo data.
        private static SeasonStatsDto BuildDemoStats(DateTime from, DateTime to, List<SeasonDomain> members)
        {
            bool InWindow(DateTime? at) => at.HasValue && at.Value >= from && at.Value <= to;

            var sessionNodes = DemoData.LearnSessions.ToDictionary(s => s.SessionId, s => s.NodeId);
            var learnInWindow = DemoData.LearnEntries.Where(e => InWindow(e.CreatedAt)).ToList();

            var nodes = DemoData.KnowledgeNodes
                .Select(kn => new SeasonStatsBuilder.NodeActivity(
                    kn.Id,
                    kn.Title,
                    kn.DomainId,
                    DemoData.Domains.First(d => d.DomainId == kn.DomainId).DomainName,
                    DemoData.LogEntries.Count(l => l.NodeId == kn.Id && InWindow(l.EntryDate)),
                    DemoData.Actions.Count(a => a.KnowledgeNodeId == kn.Id && InWindow(a.CreatedAt)),
                    DemoData.Actions.Count(a => a.KnowledgeNodeId == kn.Id && InWindow(a.CompletedAt)),
                    learnInWindow.Count(e => sessionNodes.GetValueOrDefault(e.SessionId) == kn.Id)))
                .Where(n => n.Logs + n.ActionsCreated + n.ActionsCompleted + n.LearnEntries > 0)
                .ToList();

            int activeDays = DemoData.LogEntries.Where(l => InWindow(l.EntryDate)).Select(l => l.EntryDate.Date)
                .Concat(DemoData.Actions.Where(a => InWindow(a.CreatedAt)).Select(a => a.CreatedAt.Date))
                .Concat(DemoData.Actions.Where(a => InWindow(a.CompletedAt)).Select(a => a.CompletedAt!.Value.Date))
                .Concat(learnInWindow.Select(e => e.CreatedAt.Date))
                .Distinct()
                .Count();

            return SeasonStatsBuilder.Build(
                from, to, nodes, members, activeDays,
                learnInWindow.Select(e => e.SessionId).Distinct().Count(),
                learnInWindow.Count(e => sessionNodes.GetValueOrDefault(e.SessionId) == null));
        }
    }
}
