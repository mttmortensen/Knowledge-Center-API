using Azure;
using Azure.Core;
using Knowledge_Center_API.DataAccess.Demo;
using Knowledge_Center_API.Models;
using Knowledge_Center_API.Models.DTOs;
using Knowledge_Center_API.Services.Core;
using Knowledge_Center_API.Services.Security;
using Microsoft.AspNetCore.Mvc;

namespace Knowledge_Center_API.Controllers
{
    /// <summary>
    /// Handles operations related to log entries (CRUD).
    /// Requires JWT authentication.
    /// </summary>
    [RequireToken]
    [ApiController]
    [Route("api/logs")]
    public class LogEntriesController : ControllerBase
    {
        private readonly LogEntryService _logEntryService;

        public LogEntriesController(LogEntryService logEntryService)
        {
            _logEntryService = logEntryService;
        }

        /// <summary>
        /// Retrieves all log entries, optionally filtered by Knowledge Node ID.
        /// </summary>
        /// <returns>200 OK with a list of logs.</returns>
        /// <param name="nodeId">Optional Knowledge Node ID to filter logs.</param>
        /// <response code="200">List of logs retrieved successfully.</response>
        [HttpGet]
        public IActionResult GetAll([FromQuery] int? nodeId)
        {
            // Demo mode: Read-only
            if (User.HasClaim("demo", "true"))
            {
                // Use in-memory demo data
                return Ok(DemoData.LogEntries);
            }

            var logs = nodeId.HasValue
                ? _logEntryService.GetLogsForKnowledgeNode(nodeId.Value)
                : _logEntryService.GetAllLogEntries();
            return Ok(logs);
        }

        /// <summary>
        /// Retrieves a specific log entry by ID.
        /// </summary>
        /// <param name="id">The ID of the log entry.</param>
        /// <returns>200 OK with the log entry, or 404 if not found.</returns>
        /// <response code="200">Log entry found.</response>
        /// <response code="404">Log entry not found.</response>
        [HttpGet("{id}")]
        public IActionResult GetById(int id)
        {
            // Demo mode: Read-only
            if (User.HasClaim("demo", "true"))
            {
                LogEntry demoLog = DemoData.LogEntries.FirstOrDefault(lg => lg.LogId == id);

                if (demoLog == null)
                    return NotFound($"Demo Log Entry with ID {id} is not found");

                return Ok(demoLog);
            }

            var log = _logEntryService.GetLogEntryByLogId(id);
            if (log == null)
                return NotFound(new { message = $"Log with ID {id} not found." });

            return Ok(log);
        }

        /// <summary>
        /// Creates a new log entry.
        /// </summary>
        /// <param name="log">Log entry details.</param>
        /// <returns>201 Created with the created log entry.</returns>
        /// <response code="201">Log entry created successfully.</response>
        /// <response code="400">Invalid input.</response>
        /// <response code="429">Rate limit exceeded.</response>
        /// <response code="500">Server error during creation.</response>
        [HttpPost]
        public IActionResult Create([FromBody] LogEntryCreateDto log)
        {
            // Check for demo mode, return fake object if true
            var demoResult = AuthHelper.HandleDemoCreate(User, () => new LogEntry
            {
                LogId = 9999,
                NodeId = log.NodeId,
                Content = log.Content,
                EntryDate = DateTime.UtcNow
            });

            if (demoResult != null)
                return demoResult;

            // Rate Limit Check
            if (!RateLimiter.IsAllowed(HttpContext))
            {
                return StatusCode(429, new { message = "Rate limit exceeded. Try again later." });
            }

            try
            {
                // === Step 1: Call service — it handles FieldValidator logic ===
                log.EntryDate = DateTime.Now;

                bool success = _logEntryService.CreateLogEntryFromDto(log);
                if (!success)
                    return StatusCode(500, new { message = "Failed to create log entry." });

                return CreatedAtAction(nameof(GetById), new { id = log.Id }, log);
            }
            catch (ArgumentException ex)
            {
                // === Step 2: Catch validation failures cleanly ===
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "An unexpected error occured" });
            }
        }
    }
}
