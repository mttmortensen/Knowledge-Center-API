using System.Text;
using System.Text.RegularExpressions;
using Knowledge_Center_API.DataAccess.Demo;
using Knowledge_Center_API.Models.Learn;
using Knowledge_Center_API.Services.Core;
using Knowledge_Center_API.Services.Security;
using Microsoft.AspNetCore.Mvc;

namespace Knowledge_Center_API.Controllers
{
    /// <summary>
    /// Learn: progressive-learning notebooks. A session is an ordered timeline of
    /// markdown entries (notes, images, code, ?? questions, @answers(id) follow-ups
    /// and @log(id) log references).
    /// Requires JWT authentication.
    /// </summary>
    [RequireToken]
    [ApiController]
    [Route("api/learn")]
    public class LearnController : ControllerBase
    {
        private readonly LearnService _learnService;

        public LearnController(LearnService learnService)
        {
            _learnService = learnService;
        }

        /* ===================== SESSIONS ===================== */

        /// <summary>
        /// Retrieves all Learn sessions (without entries), most recently updated first.
        /// </summary>
        /// <response code="200">List of sessions with entry and open-question counts.</response>
        [HttpGet("sessions")]
        public IActionResult GetSessions()
        {
            if (User.HasClaim("demo", "true"))
                return Ok(DemoData.LearnSessions);

            return Ok(_learnService.GetAllSessions());
        }

        /// <summary>
        /// Retrieves a Learn session with all of its entries in timeline order.
        /// </summary>
        /// <param name="id">The session ID.</param>
        /// <response code="200">Session found.</response>
        /// <response code="404">Session not found.</response>
        [HttpGet("sessions/{id}")]
        public IActionResult GetSession(int id)
        {
            if (User.HasClaim("demo", "true"))
            {
                var demo = BuildDemoSession(id);
                if (demo == null)
                    return NotFound(new { message = $"Demo Learn session with ID {id} not found." });

                return Ok(demo);
            }

            var session = _learnService.GetSessionWithEntries(id);
            if (session == null)
                return NotFound(new { message = $"Learn session with ID {id} not found." });

            return Ok(session);
        }

        /// <summary>
        /// Creates a new, empty Learn session.
        /// </summary>
        /// <param name="dto">Title (required), optional Topic and Knowledge Node.</param>
        /// <response code="201">Session created.</response>
        /// <response code="400">Invalid input.</response>
        /// <response code="429">Rate limit exceeded.</response>
        /// <response code="500">Server error during creation.</response>
        [HttpPost("sessions")]
        public IActionResult CreateSession([FromBody] LearnSessionCreateDto dto)
        {
            var demoResult = AuthHelper.HandleDemoCreate(User, () => new LearnSessionDetailsDto
            {
                SessionId = 9999,
                Title = dto.Title,
                Topic = dto.Topic,
                NodeId = dto.NodeId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            if (demoResult != null)
                return demoResult;

            if (!RateLimiter.IsAllowed(HttpContext))
                return StatusCode(429, new { message = "Rate limit exceeded. Try again later." });

            try
            {
                int newId = _learnService.CreateSession(dto);
                if (newId <= 0)
                    return StatusCode(500, new { message = "Failed to create Learn session." });

                return CreatedAtAction(nameof(GetSession), new { id = newId }, _learnService.GetSessionWithEntries(newId));
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
        /// Updates a session's title, topic and/or Knowledge Node. Only supplied fields
        /// change; an empty Topic clears it and NodeId 0 unlinks the node.
        /// </summary>
        /// <param name="id">The session ID.</param>
        /// <param name="dto">Fields to update.</param>
        /// <response code="200">Session updated; returns the session with entries.</response>
        /// <response code="400">Invalid input.</response>
        /// <response code="404">Session not found.</response>
        [HttpPut("sessions/{id}")]
        public IActionResult UpdateSession(int id, [FromBody] LearnSessionUpdateDto dto)
        {
            if (User.HasClaim("demo", "true"))
                return StatusCode(403, new { message = "Update operations are disabled in demo mode." });

            if (!RateLimiter.IsAllowed(HttpContext))
                return StatusCode(429, new { message = "Rate limit exceeded. Try again later." });

            try
            {
                if (!_learnService.SessionExists(id))
                    return NotFound(new { message = $"Learn session with ID {id} was not found." });

                if (!_learnService.UpdateSession(id, dto))
                    return StatusCode(500, new { message = "Failed to update Learn session." });

                return Ok(_learnService.GetSessionWithEntries(id));
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
        /// Deletes a Learn session and all of its entries. Logs referenced with
        /// @log(id) are not touched.
        /// </summary>
        /// <param name="id">The session ID.</param>
        /// <response code="204">Session deleted.</response>
        /// <response code="404">Session not found.</response>
        [HttpDelete("sessions/{id}")]
        public IActionResult DeleteSession(int id)
        {
            if (User.HasClaim("demo", "true"))
                return StatusCode(403, new { message = "Deleting operations are disabled in demo mode." });

            if (!RateLimiter.IsAllowed(HttpContext))
                return StatusCode(429, new { message = "Rate limit exceeded. Try again later." });

            try
            {
                if (!_learnService.SessionExists(id))
                    return NotFound(new { message = $"Learn session with ID {id} was not found." });

                if (!_learnService.DeleteSession(id))
                    return StatusCode(500, new { message = "Failed to delete Learn session." });

                return NoContent();
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "An unexpected error occurred." });
            }
        }

        /// <summary>
        /// Exports a session as a single markdown file in timeline order. @log(id)
        /// entries are expanded to the log's content.
        /// </summary>
        /// <param name="id">The session ID.</param>
        /// <response code="200">text/markdown attachment.</response>
        /// <response code="404">Session not found.</response>
        [HttpGet("sessions/{id}/export")]
        [Produces("text/markdown")]
        public IActionResult ExportSession(int id)
        {
            string? markdown;
            string? title;

            if (User.HasClaim("demo", "true"))
            {
                var demo = BuildDemoSession(id);
                markdown = demo == null
                    ? null
                    : LearnMarkdownExporter.Build(demo, logId => DemoData.LogEntries.FirstOrDefault(l => l.LogId == logId));
                title = demo?.Title;
            }
            else
            {
                markdown = _learnService.ExportSessionMarkdown(id);
                title = markdown == null ? null : _learnService.GetSessionWithEntries(id)?.Title;
            }

            if (markdown == null)
                return NotFound(new { message = $"Learn session with ID {id} not found." });

            return File(Encoding.UTF8.GetBytes(markdown), "text/markdown; charset=utf-8", $"{Slugify(title)}.md");
        }

        /* ===================== ENTRIES ===================== */

        /// <summary>
        /// Adds an entry to a session. Set BeforeEntryId to insert directly above that
        /// entry; leave it null to append at the end.
        /// </summary>
        /// <param name="sessionId">The session ID.</param>
        /// <param name="dto">Raw markdown source and optional insert position.</param>
        /// <response code="201">Entry created.</response>
        /// <response code="400">Invalid input, e.g. an @answers target that doesn't exist or is in another session.</response>
        /// <response code="404">Session not found.</response>
        /// <response code="429">Rate limit exceeded.</response>
        [HttpPost("sessions/{sessionId}/entries")]
        public IActionResult CreateEntry(int sessionId, [FromBody] LearnEntryCreateDto dto)
        {
            var demoResult = AuthHelper.HandleDemoCreate(User, () =>
            {
                var fake = new LearnEntry
                {
                    EntryId = 9999,
                    SessionId = sessionId,
                    Source = dto.Source,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                LearnSourceParser.Annotate(new List<LearnEntry> { fake });
                return fake;
            });

            if (demoResult != null)
                return demoResult;

            if (!RateLimiter.IsAllowed(HttpContext))
                return StatusCode(429, new { message = "Rate limit exceeded. Try again later." });

            try
            {
                if (!_learnService.SessionExists(sessionId))
                    return NotFound(new { message = $"Learn session with ID {sessionId} was not found." });

                int newId = _learnService.CreateEntry(sessionId, dto);
                if (newId <= 0)
                    return StatusCode(500, new { message = "Failed to create entry." });

                return CreatedAtAction(nameof(GetEntry), new { id = newId }, _learnService.GetEntryById(newId));
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
        /// Retrieves a single entry.
        /// </summary>
        /// <param name="id">The entry ID.</param>
        /// <response code="200">Entry found.</response>
        /// <response code="404">Entry not found.</response>
        [HttpGet("entries/{id}")]
        public IActionResult GetEntry(int id)
        {
            if (User.HasClaim("demo", "true"))
            {
                var demoEntry = DemoData.LearnEntries.FirstOrDefault(e => e.EntryId == id);
                if (demoEntry == null)
                    return NotFound(new { message = $"Demo entry with ID {id} not found." });

                return Ok(demoEntry);
            }

            var entry = _learnService.GetEntryById(id);
            if (entry == null)
                return NotFound(new { message = $"Entry with ID {id} not found." });

            return Ok(entry);
        }

        /// <summary>
        /// Replaces an entry's markdown source.
        /// </summary>
        /// <param name="id">The entry ID.</param>
        /// <param name="dto">The new raw markdown source.</param>
        /// <response code="200">Entry updated.</response>
        /// <response code="400">Invalid input, e.g. a new @answers target that doesn't exist or is in another session.</response>
        /// <response code="404">Entry not found.</response>
        [HttpPut("entries/{id}")]
        public IActionResult UpdateEntry(int id, [FromBody] LearnEntryUpdateDto dto)
        {
            if (User.HasClaim("demo", "true"))
                return StatusCode(403, new { message = "Update operations are disabled in demo mode." });

            if (!RateLimiter.IsAllowed(HttpContext))
                return StatusCode(429, new { message = "Rate limit exceeded. Try again later." });

            try
            {
                if (_learnService.GetEntryById(id) == null)
                    return NotFound(new { message = $"Entry with ID {id} was not found." });

                if (!_learnService.UpdateEntrySource(id, dto))
                    return StatusCode(500, new { message = "Failed to update entry." });

                return Ok(_learnService.GetEntryById(id));
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
        /// Deletes an entry. Follow-ups that answered it are kept and keep their
        /// @answers(id) text; they are shown as answering a deleted entry.
        /// </summary>
        /// <param name="id">The entry ID.</param>
        /// <response code="204">Entry deleted.</response>
        /// <response code="404">Entry not found.</response>
        [HttpDelete("entries/{id}")]
        public IActionResult DeleteEntry(int id)
        {
            if (User.HasClaim("demo", "true"))
                return StatusCode(403, new { message = "Deleting operations are disabled in demo mode." });

            if (!RateLimiter.IsAllowed(HttpContext))
                return StatusCode(429, new { message = "Rate limit exceeded. Try again later." });

            try
            {
                if (_learnService.GetEntryById(id) == null)
                    return NotFound(new { message = $"Entry with ID {id} was not found." });

                if (!_learnService.DeleteEntry(id))
                    return StatusCode(500, new { message = "Failed to delete entry." });

                return NoContent();
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "An unexpected error occurred." });
            }
        }

        /* ===================== QUESTIONS ===================== */

        /// <summary>
        /// Retrieves questions that have no @answers follow-up yet, across every
        /// session or within one.
        /// </summary>
        /// <param name="sessionId">Optional session ID to limit results to.</param>
        /// <response code="200">List of open questions.</response>
        [HttpGet("questions/open")]
        public IActionResult GetOpenQuestions([FromQuery] int? sessionId)
        {
            if (User.HasClaim("demo", "true"))
            {
                var open = DemoData.LearnEntries
                    .Where(e => e.QuestionStatus == "open" && (!sessionId.HasValue || e.SessionId == sessionId.Value))
                    .Select(e => new LearnOpenQuestionDto
                    {
                        EntryId = e.EntryId,
                        SessionId = e.SessionId,
                        SessionTitle = DemoData.LearnSessions.First(s => s.SessionId == e.SessionId).Title,
                        Source = e.Source,
                        CreatedAt = e.CreatedAt
                    });

                return Ok(open);
            }

            return Ok(_learnService.GetOpenQuestions(sessionId));
        }

        /* ===================== HELPERS ===================== */

        private static LearnSessionDetailsDto? BuildDemoSession(int id)
        {
            var s = DemoData.LearnSessions.FirstOrDefault(x => x.SessionId == id);
            if (s == null) return null;

            return new LearnSessionDetailsDto
            {
                SessionId = s.SessionId,
                Title = s.Title,
                Topic = s.Topic,
                NodeId = s.NodeId,
                NodeTitle = s.NodeTitle,
                CreatedAt = s.CreatedAt,
                UpdatedAt = s.UpdatedAt,
                EntryCount = s.EntryCount,
                OpenQuestionCount = s.OpenQuestionCount,
                Entries = DemoData.LearnEntries.Where(e => e.SessionId == id).OrderBy(e => e.Position).ToList()
            };
        }

        private static string Slugify(string? title)
        {
            string slug = Regex.Replace((title ?? "learn-session").ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
            return string.IsNullOrEmpty(slug) ? "learn-session" : slug;
        }
    }
}
