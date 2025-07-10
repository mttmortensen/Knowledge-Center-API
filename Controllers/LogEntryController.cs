using Azure;
using Knowledge_Center_API.Models;
using Knowledge_Center_API.Services.Core;
using Knowledge_Center_API.Services.Security;
using Microsoft.AspNetCore.Mvc;

namespace Knowledge_Center_API.Controllers
{
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

        // === GET /api/logs ===
        [HttpGet]
        public IActionResult GetAll()
        {
            var logs = _logEntryService.GetAllLogEntries();
            return Ok(logs);
        }

        // === GET /api/logs/{id} ===
        [HttpGet("{id}")]
        public IActionResult GetById(int id)
        {
            var log = _logEntryService.GetLogEntryByLogId(id);
            if (log == null)
                return NotFound(new { message = $"Log with ID {id} not found." });

            return Ok(log);
        }

        // === POST /api/logs ===
        [HttpPost]
        public IActionResult Create([FromBody] LogEntry log)
        {
            // Rate Limit Check
            if (!RateLimiter.IsAllowed(HttpContext))
            {
                return StatusCode(429, new { message = "Rate limit exceeded. Try again later." });
            }

            try
            {
                // === Step 1: Call service — it handles FieldValidator logic ===
                log.EntryDate = DateTime.Now;

                bool success = _logEntryService.CreateLogEntry(log);
                if (!success)
                    return StatusCode(500, new { message = "Failed to create log entry." });

                return CreatedAtAction(nameof(GetById), new { id = log.LogId }, log);
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
