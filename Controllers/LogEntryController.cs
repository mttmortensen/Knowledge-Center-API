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
            // Demo mode: Read-only
            if (User.HasClaim("demo", "true"))
            {
                // Use in-memory demo data
                return Ok(DemoData.LogEntries);
            }

            var logs = _logEntryService.GetAllLogEntries();
            return Ok(logs);
        }

        // === GET /api/logs/{id} ===
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

        // === POST /api/logs ===
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
