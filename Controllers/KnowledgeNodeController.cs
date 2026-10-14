using Knowledge_Center_API.DataAccess.Demo;
using Knowledge_Center_API.Models.KnowledgeNodes;
using Knowledge_Center_API.Models.LogEntries;
using Knowledge_Center_API.Services.Core;
using Knowledge_Center_API.Services.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Knowledge_Center_API.Controllers
{
    /// <summary>
    /// Handles knowledge node operations (CRUD).
    /// Requires JWT authentication.
    /// </summary>
    [RequireToken]
    [ApiController]
    [Route("/api/knowledge-nodes")]
    public class KnowledgeNodeController : ControllerBase
    {
        private readonly KnowledgeNodeService _knowledgeNodeService;
        private readonly LogEntryService _logEntryService;
        private readonly ActionService _actionService;
        private readonly DomainService _domainService;

        public KnowledgeNodeController(KnowledgeNodeService knService, LogEntryService lgService, ActionService actionService, DomainService domainService)
        {
            _knowledgeNodeService = knService;
            _logEntryService = lgService;
            _actionService = actionService;
            _domainService = domainService;
        }

        /// <summary>
        /// Retrieves all knowledge nodes.
        /// </summary>
        /// <param name="includeArchived">When true, includes archived knowledge nodes in the results.</param>
        [HttpGet]
        public IActionResult GetAll([FromQuery] bool includeArchived = false)
        {
            // Demo mode: Read-only
            if (User.HasClaim("demo", "true"))
            {
                // Use in-memory demo data
                var demoNodes = includeArchived
                    ? DemoData.KnowledgeNodes
                    : DemoData.KnowledgeNodes.Where(n => !n.IsArchived);

                return Ok(demoNodes);
            }

            var nodes = _knowledgeNodeService.GetAllKnowledgeNodes(includeArchived);
            return Ok(nodes);
        }

        /// <summary>
        /// Retrieves a specific knowledge node by ID.
        /// </summary>
        /// <param name="id">Knowledge node ID.</param>
        [HttpGet("{id}")]
        public IActionResult GetById(int id)
        {
            // Demo mode: Read-only
            if (User.HasClaim("demo", "true"))
            {
                KnowledgeNode demoKN = DemoData.KnowledgeNodes.FirstOrDefault(kn => kn.Id == id);

                if (demoKN == null)
                    return NotFound($"Demo Knowledge Node with ID {id} is not found");

                // Nest this node's demo log entries, matching the shape the real
                // endpoint returns (KnowledgeNodeDetailsWithLogsDto) — otherwise
                // the frontend's node detail page has nothing to list.
                var demoLogs = DemoData.LogEntries
                    .Where(log => log.NodeId == id)
                    .Select(log => new LogEntryDetailsInlineDto
                    {
                        LogId = log.LogId,
                        Title = log.Title,
                        Content = log.Content,
                        EntryDate = log.EntryDate
                    })
                    .ToList();

                return Ok(new KnowledgeNodeDetailsWithLogsDto
                {
                    Id = demoKN.Id,
                    Title = demoKN.Title,
                    DomainId = demoKN.DomainId,
                    NodeType = demoKN.NodeType,
                    Description = demoKN.Description,
                    ConfidenceLevel = demoKN.ConfidenceLevel,
                    Status = demoKN.Status,
                    CreatedAt = demoKN.CreatedAt,
                    LastUpdated = demoKN.LastUpdated,
                    Logs = demoLogs
                });
            }

            var node = _knowledgeNodeService.GetKnowledgeNodeWithLogsById(id);
            if (node == null)
                return NotFound(new { message = $"Knowledge Node with ID {id} not found." });

            return Ok(node);
        }

        /// <summary>
        /// Creates a new knowledge node.
        /// </summary>
        /// <param name="node">Knowledge node details.</param>
        [HttpPost]
        public IActionResult Create([FromBody] KnowledgeNode node)
        {
            // Check for demo mode, return fake object if true
            var demoResult = AuthHelper.HandleDemoCreate(User, () => new KnowledgeNode
            {
                Id = 9999,
                Title = node.Title,
                DomainId = node.DomainId,
                NodeType = node.NodeType,
                Description = node.Description,
                ConfidenceLevel = node.ConfidenceLevel,
                Status = node.Status,
                CreatedAt = AppClock.Now
            });

            if (demoResult != null)
                return demoResult;

            // === Step 0: Rate Limit Check ===
            if (!RateLimiter.IsAllowed(HttpContext))
            {
                return StatusCode(429, new { message = "Rate limit exceeded. Try again later." });
            }

            try 
            {
                // === Step 1: Call service — it handles FieldValidator logic ===
                bool success = _knowledgeNodeService.CreateKnowledgeNode(node);
                if (!success)
                    return StatusCode(500, new { message = "Failed to create node." });

                return CreatedAtAction(nameof(GetById), new { id = node.Id }, node);
            }
            catch(ArgumentException ex) 
            {
                // === Step 2: Catch validation failures cleanly ===
                return BadRequest(new { message = ex.Message });
            }
            catch(Exception) 
            {
                return StatusCode(500, new { message = "An unexpected error occurred." });
            }


        }

        /// <summary>
        /// Updates a knowledge node by ID.
        /// </summary>
        /// <param name="id">Knowledge node ID.</param>
        /// <param name="node">Updated node data.</param>
        [HttpPut("{id}")]
        public IActionResult Update(int id, [FromBody] KnowledgeNodeUpdateDto node)
        {
            // Demo mode: Updating is disabled
            if (User.HasClaim("demo", "true"))
            {
                return StatusCode(403, new { message = "Update operations are disabled in demo mode." });
            }

            // Rate Limit Check
            if (!RateLimiter.IsAllowed(HttpContext))
            {
                return StatusCode(429, new { message = "Rate limit exceeded. Try again later." });
            }

            try { 
                node.Id = id;

                // Call service — it handles FieldValidator logic
                bool success = _knowledgeNodeService.UpdateKnowledgeNodeFromDto(node.Id, node);
                if (!success)
                    return StatusCode(500, new { message = "Node not found or update failed." });

                return Ok(node);

            }
            catch(ArgumentException ex) 
            {
                // Catch validation failures cleanly
                return BadRequest(new { message = ex.Message });
            }
            catch(Exception)
            {
                return StatusCode(500, new { message = "An unexpected error occurred." });
            }
        }

        /// <summary>
        /// Deletes a knowledge node and its associated logs.
        /// </summary>
        /// <param name="id">Knowledge node ID.</param>
        [HttpDelete("{id}")]
        public IActionResult Delete(int id)
        {
            // Demo mode: Deleting is disabled
            if (User.HasClaim("demo", "true"))
            {
                return StatusCode(403, new { message = "Deleting operations are disabled in demo mode." });
            }

            // Rate Limit Check
            if (!RateLimiter.IsAllowed(HttpContext))
            {
                return StatusCode(429, new { message = "Rate limit exceeded. Try again later." });
            }

            // Delete all logs associated with the node
            bool logsDeleted = _logEntryService.DeleteAllLogEntriesByNodeId(id);
            if (!logsDeleted)
                return StatusCode(500, new { message = "Failed to delete related logs." });

            // Delete all actions associated with the node
            bool actionsDeleted = _actionService.DeleteAllActionsByNodeId(id);
            if (!actionsDeleted)
                return StatusCode(500, new { message = "Failed to delete related actions." });

            // Now delete the node
            bool nodeDeleted = _knowledgeNodeService.DeleteKnowledgeNode(id);
            if (!nodeDeleted)
                return StatusCode(500, new { message = "Node not found or delete failed." });

            return Ok(new { message = "Node and related logs deleted successfully." });
        }

        /// <summary>
        /// Moves a knowledge node (with its logs and actions) to another domain.
        /// </summary>
        /// <param name="id">Knowledge node ID.</param>
        /// <param name="dto">The domain to move it to.</param>
        [HttpPut("{id}/move")]
        public IActionResult Move(int id, [FromBody] KnowledgeNodeMoveDto dto)
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
                bool success = _knowledgeNodeService.MoveKnowledgeNode(id, dto.DomainId);
                if (!success)
                    return NotFound(new { message = $"Knowledge Node with ID {id} not found." });

                return Ok(_knowledgeNodeService.GetKnowledgeNodeWithLogsById(id));
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
        /// Promotes a knowledge node to a domain. The new domain takes the node's
        /// title and description, and the node (with its logs and actions) moves under it.
        /// The optional body renames the domain and/or the moved node.
        /// </summary>
        /// <param name="id">Knowledge node ID.</param>
        [HttpPost("{id}/promote")]
        public IActionResult Promote(int id,
            [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] KnowledgeNodePromoteDto? dto)
        {
            if (User.HasClaim("demo", "true"))
            {
                return StatusCode(403, new { message = "Promote operations are disabled in demo mode." });
            }

            if (!RateLimiter.IsAllowed(HttpContext))
            {
                return StatusCode(429, new { message = "Rate limit exceeded. Try again later." });
            }

            try
            {
                int? domainId = _domainService.PromoteKnowledgeNodeToDomain(id, dto?.DomainName, dto?.KnowledgeNodeTitle);
                if (domainId == null)
                    return NotFound(new { message = $"Knowledge Node with ID {id} not found." });

                var domain = _domainService.GetDomainByIdWithKNs(domainId.Value);
                return CreatedAtAction(nameof(DomainController.GetById), "Domain", new { id = domainId.Value }, domain);
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
        /// Archives a knowledge node by ID.
        /// </summary>
        /// <param name="id">Knowledge node ID.</param>
        [HttpPut("{id}/archive")]
        public IActionResult Archive(int id)
        {
            if (User.HasClaim("demo", "true"))
            {
                return StatusCode(403, new { message = "Archive operations are disabled in demo mode." });
            }

            if (!RateLimiter.IsAllowed(HttpContext))
            {
                return StatusCode(429, new { message = "Rate limit exceeded. Try again later." });
            }

            bool success = _knowledgeNodeService.ArchiveKnowledgeNode(id);
            if (!success)
                return NotFound(new { message = $"Knowledge Node with ID {id} not found." });

            return Ok(_knowledgeNodeService.GetKnowledgeNodeWithLogsById(id));
        }

        /// <summary>
        /// Unarchives a knowledge node by ID.
        /// </summary>
        /// <param name="id">Knowledge node ID.</param>
        [HttpPut("{id}/unarchive")]
        public IActionResult Unarchive(int id)
        {
            if (User.HasClaim("demo", "true"))
            {
                return StatusCode(403, new { message = "Archive operations are disabled in demo mode." });
            }

            if (!RateLimiter.IsAllowed(HttpContext))
            {
                return StatusCode(429, new { message = "Rate limit exceeded. Try again later." });
            }

            bool success = _knowledgeNodeService.UnarchiveKnowledgeNode(id);
            if (!success)
                return NotFound(new { message = $"Knowledge Node with ID {id} not found." });

            return Ok(_knowledgeNodeService.GetKnowledgeNodeWithLogsById(id));
        }
    }
}
