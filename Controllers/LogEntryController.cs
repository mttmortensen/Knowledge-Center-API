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

                // Get the created ID back
                int newLogId = _logEntryService.CreateLogEntryAndReturnId(log);

                if (newLogId <= 0)
                    return StatusCode(500, new { message = "Failed to create log entry." });

                // Refetch full log including tags
                var createdLog = _logEntryService.GetLogEntryByLogId(newLogId);

                return CreatedAtAction(nameof(GetById), new { id = newLogId }, createdLog);

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

        /// <summary>
        /// Replaces all tags on a specific log entry.
        /// </summary>
        /// <param name="logId">ID of the log entry to update.</param>
        /// <param name="dto">DTO containing the list of new tag IDs.</param>
        /// <returns>204 No Content if successful.</returns>
        /// <response code="204">Tags updated successfully.</response>
        /// <response code="400">Invalid input.</response>
        /// <response code="404">Log not found.</response>
        /// <response code="500">Server error during update.</response>
        [HttpPut("{logId}/tags")]
        public IActionResult ReplaceTags(int logId, [FromBody] LogTagUpdateDto dto) 
        {
            try 
            {
                // Validate log
                var existingLog = _logEntryService.GetLogEntryByLogId(logId);
                if (existingLog == null)
                    return NotFound(new { message = $"Log with ID {logId} was not found." });

                _logEntryService.ReplaceTagsOnLog(logId, dto.TagIds);

                return NoContent();
            }
            catch(ArgumentException ex) 
            {
                return BadRequest(new { message = ex.Message });
            }
            catch(Exception ex) 
            {
                return StatusCode(500, new { message = "EXCEPTION: " + ex.Message });
            }
        }

        /// <summary>
        /// Deletes all tags from a specific log entry.
        /// </summary>
        /// <param name="logId">ID of the log entry.</param>
        /// <returns>204 No Content if successful.</returns>
        /// <response code="204">Tags deleted successfully.</response>
        /// <response code="404">Log not found or no tags to delete.</response>
        /// <response code="500">Server error during tag deletion.</response>
        [HttpDelete("{logId}/tags")]
        public IActionResult DeleteAllTagsFromLog(int logId) 
        {
            try 
            {
                var log = _logEntryService.GetLogEntryByLogId(logId);
                if (log == null)
                    return NotFound(new { message = $"Log with ID {logId} was not found. " });

                bool result = _logEntryService.DeleteAllLogEntriesByNodeId(logId);

                return result
                    ? NoContent()
                    : NotFound(new { message = $"No tags were found on Log ID {logId} to delete. " });
            }
            catch(Exception) 
            {
                return StatusCode(500, new { message = "An unexpected error occurred." });
            }
        }

        /// <summary>
        /// Deletes specific tags from a log entry.
        /// </summary>
        /// <param name="logId">ID of the log entry.</param>
        /// <param name="dto">DTO containing tag IDs to remove.</param>
        /// <returns>204 No Content if successful.</returns>
        /// <response code="204">Specified tags deleted successfully.</response>
        /// <response code="400">Invalid or missing tag IDs.</response>
        /// <response code="404">Log not found.</response>
        /// <response code="500">Server error during tag deletion.</response>
        [HttpDelete("{logId}/tags/specific")]
        public IActionResult DeleteSpecificTagsFromLog(int logId, [FromBody] LogTagDeleteDto dto) 
        {
            try
            {
                var log = _logEntryService.GetLogEntryByLogId(logId);
                if (log == null)
                    return NotFound(new { message = $"Log with ID: {logId} was not found. " });

                if (dto?.TagIds == null || dto.TagIds.Count == 0)
                    return BadRequest(new { message = "No tag IDs provided for deletion" });

                _logEntryService.RemoveSpecificTagsFromLog(logId, dto.TagIds);

                return NoContent();
            }
            catch (Exception) 
            {
                return StatusCode(500, new { message = "An unexpected error occurred." });
            }
        }
    }
}
