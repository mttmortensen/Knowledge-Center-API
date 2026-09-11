using Knowledge_Center_API.DataAccess.Demo;
using Knowledge_Center_API.Models.Actions;
using Knowledge_Center_API.Models.Stats;
using Knowledge_Center_API.Services.Core;
using Knowledge_Center_API.Services.Security;
using Microsoft.AspNetCore.Mvc;

namespace Knowledge_Center_API.Controllers
{
    /// <summary>
    /// Handles action-item operations (CRUD) scoped to a KnowledgeNode.
    /// Requires JWT authentication.
    /// </summary>
    [RequireToken]
    [ApiController]
    [Route("/api/actions")]
    public class ActionController : ControllerBase
    {
        private const int DefaultRecentActionsLimit = 8;

        private readonly ActionService _actionService;

        public ActionController(ActionService actionService)
        {
            _actionService = actionService;
        }

        /// <summary>
        /// Retrieves the open actions for a specific knowledge node.
        /// </summary>
        [HttpGet("knowledge-node/{knowledgeNodeId}")]
        public IActionResult GetOpenForNode(int knowledgeNodeId)
        {
            if (User.HasClaim("demo", "true"))
            {
                return Ok(DemoData.Actions.Where(a => a.KnowledgeNodeId == knowledgeNodeId && a.Status == "Open"));
            }

            var actions = _actionService.GetOpenActionsForNode(knowledgeNodeId);
            return Ok(actions);
        }

        /// <summary>
        /// Retrieves the completed actions for a specific knowledge node.
        /// </summary>
        [HttpGet("knowledge-node/{knowledgeNodeId}/completed")]
        public IActionResult GetCompletedForNode(int knowledgeNodeId)
        {
            if (User.HasClaim("demo", "true"))
            {
                return Ok(DemoData.Actions.Where(a => a.KnowledgeNodeId == knowledgeNodeId && a.Status == "Completed"));
            }

            var actions = _actionService.GetCompletedActionsForNode(knowledgeNodeId);
            return Ok(actions);
        }

        /// <summary>
        /// Retrieves a specific action by ID.
        /// </summary>
        [HttpGet("{id}")]
        public IActionResult GetById(int id)
        {
            if (User.HasClaim("demo", "true"))
            {
                ActionItem demoAction = DemoData.Actions.FirstOrDefault(a => a.Id == id);

                if (demoAction == null)
                    return NotFound($"Demo Action with ID {id} is not found");

                return Ok(demoAction);
            }

            var action = _actionService.GetActionById(id);
            if (action == null)
                return NotFound(new { message = $"Action with ID {id} not found." });

            return Ok(action);
        }

        /// <summary>
        /// Retrieves all open actions across every knowledge node.
        /// </summary>
        [HttpGet("open")]
        public IActionResult GetAllOpen()
        {
            if (User.HasClaim("demo", "true"))
            {
                return Ok(DemoData.Actions.Where(a => a.Status == "Open"));
            }

            var actions = _actionService.GetAllOpenActions();
            return Ok(actions);
        }

        /// <summary>
        /// Retrieves all completed actions across every knowledge node.
        /// </summary>
        [HttpGet("completed")]
        public IActionResult GetAllCompleted()
        {
            if (User.HasClaim("demo", "true"))
            {
                return Ok(DemoData.Actions.Where(a => a.Status == "Completed"));
            }

            var actions = _actionService.GetAllCompletedActions();
            return Ok(actions);
        }

        /// <summary>
        /// Retrieves open-action counts grouped by knowledge node.
        /// </summary>
        [HttpGet("open-counts")]
        public IActionResult GetOpenCounts()
        {
            if (User.HasClaim("demo", "true"))
            {
                var counts = DemoData.Actions
                    .Where(a => a.Status == "Open")
                    .GroupBy(a => a.KnowledgeNodeId)
                    .Select(g => new ActionOpenCountDto { KnowledgeNodeId = g.Key, OpenCount = g.Count() });

                return Ok(counts);
            }

            var openCounts = _actionService.GetOpenActionCountsByNode();
            return Ok(openCounts);
        }

        /// <summary>
        /// Retrieves the most recently created actions across every knowledge node.
        /// </summary>
        /// <param name="limit">Maximum number of actions to return.</param>
        [HttpGet("recent")]
        public IActionResult GetRecent([FromQuery] int limit = DefaultRecentActionsLimit)
        {
            if (User.HasClaim("demo", "true"))
            {
                return Ok(BuildDemoRecent(limit));
            }

            var recent = _actionService.GetRecentActions(limit);
            return Ok(recent);
        }

        /// <summary>
        /// Creates a new action for a knowledge node.
        /// </summary>
        /// <param name="dto">Action details.</param>
        [HttpPost]
        public IActionResult Create([FromBody] ActionItemCreateDto dto)
        {
            var demoResult = AuthHelper.HandleDemoCreate(User, () => new ActionItem
            {
                Id = 9999,
                KnowledgeNodeId = dto.KnowledgeNodeId,
                ActionText = dto.ActionText,
                Status = "Open",
                CreatedAt = DateTime.UtcNow
            });

            if (demoResult != null)
                return demoResult;

            if (!RateLimiter.IsAllowed(HttpContext))
            {
                return StatusCode(429, new { message = "Rate limit exceeded. Try again later." });
            }

            try
            {
                var action = new ActionItem
                {
                    KnowledgeNodeId = dto.KnowledgeNodeId,
                    ActionText = dto.ActionText
                };

                bool success = _actionService.CreateActionItem(action);
                if (!success)
                    return StatusCode(500, new { message = "Failed to create action." });

                return CreatedAtAction(nameof(GetById), new { id = action.Id }, action);
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
        /// Updates an action's text by ID.
        /// </summary>
        /// <param name="id">Action ID.</param>
        /// <param name="dto">Updated action data.</param>
        [HttpPut("{id}")]
        public IActionResult Update(int id, [FromBody] ActionItemUpdateDto dto)
        {
            if (User.HasClaim("demo", "true"))
            {
                return StatusCode(403, new { message = "Update operations are disabled in demo mode." });
            }

            if (!RateLimiter.IsAllowed(HttpContext))
            {
                return StatusCode(429, new { message = "Rate limit exceeded. Try again later." });
            }

            try
            {
                dto.Id = id;

                bool success = _actionService.UpdateActionText(id, dto);
                if (!success)
                    return StatusCode(500, new { message = "Action not found or update failed." });

                return Ok(dto);
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
        /// Marks an action as completed.
        /// </summary>
        /// <param name="id">Action ID.</param>
        [HttpPut("{id}/complete")]
        public IActionResult Complete(int id)
        {
            if (User.HasClaim("demo", "true"))
            {
                return StatusCode(403, new { message = "Update operations are disabled in demo mode." });
            }

            if (!RateLimiter.IsAllowed(HttpContext))
            {
                return StatusCode(429, new { message = "Rate limit exceeded. Try again later." });
            }

            bool success = _actionService.CompleteAction(id);
            if (!success)
                return NotFound(new { message = $"Action with ID {id} not found." });

            return Ok(_actionService.GetActionById(id));
        }

        /// <summary>
        /// Reopens a completed action.
        /// </summary>
        /// <param name="id">Action ID.</param>
        [HttpPut("{id}/reopen")]
        public IActionResult Reopen(int id)
        {
            if (User.HasClaim("demo", "true"))
            {
                return StatusCode(403, new { message = "Update operations are disabled in demo mode." });
            }

            if (!RateLimiter.IsAllowed(HttpContext))
            {
                return StatusCode(429, new { message = "Rate limit exceeded. Try again later." });
            }

            bool success = _actionService.ReopenAction(id);
            if (!success)
                return NotFound(new { message = $"Action with ID {id} not found." });

            return Ok(_actionService.GetActionById(id));
        }

        /// <summary>
        /// Deletes an action by ID.
        /// </summary>
        /// <param name="id">Action ID.</param>
        [HttpDelete("{id}")]
        public IActionResult Delete(int id)
        {
            if (User.HasClaim("demo", "true"))
            {
                return StatusCode(403, new { message = "Deleting operations are disabled in demo mode." });
            }

            if (!RateLimiter.IsAllowed(HttpContext))
            {
                return StatusCode(429, new { message = "Rate limit exceeded. Try again later." });
            }

            bool deleted = _actionService.DeleteActionItem(id);
            if (!deleted)
                return StatusCode(500, new { message = "Action not found or delete failed." });

            return Ok(new { message = "Action deleted successfully." });
        }

        private static List<RecentActionDto> BuildDemoRecent(int limit)
        {
            return DemoData.Actions
                .OrderByDescending(action => action.CreatedAt)
                .Take(limit)
                .Select(action => new RecentActionDto
                {
                    Id = action.Id,
                    KnowledgeNodeId = action.KnowledgeNodeId,
                    KnowledgeNodeTitle = DemoData.KnowledgeNodes
                        .FirstOrDefault(kn => kn.Id == action.KnowledgeNodeId)?.Title ?? "",
                    ActionText = action.ActionText,
                    Status = action.Status,
                    CreatedAt = action.CreatedAt,
                    CompletedAt = action.CompletedAt
                })
                .ToList();
        }
    }
}
