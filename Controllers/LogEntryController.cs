using Microsoft.AspNetCore.Mvc;
using Knowledge_Center_API.Models;
using Knowledge_Center_API.Services.Core;

namespace Knowledge_Center_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
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
            log.EntryDate = DateTime.Now;

            bool success = _logEntryService.CreateLogEntry(log);
            if (!success)
                return StatusCode(500, new { message = "Failed to create log entry." });

            return CreatedAtAction(nameof(GetById), new { id = log.LogId }, log);
        }
    }
}
