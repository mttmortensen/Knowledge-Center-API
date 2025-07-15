using Knowledge_Center_API.DataAccess.Demo;
using Knowledge_Center_API.Models;
using Knowledge_Center_API.Models.DTOs;
using Knowledge_Center_API.Services.Core;
using Knowledge_Center_API.Services.Security;
using Microsoft.AspNetCore.Mvc;

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

        public KnowledgeNodeController(KnowledgeNodeService knService, LogEntryService lgService) 
        {
            _knowledgeNodeService = knService;
            _logEntryService = lgService;
        }

        /// <summary>
        /// Retrieves all knowledge nodes.
        /// </summary>
        [HttpGet]
        public IActionResult GetAll()
        {
            // Demo mode: Read-only
            if (User.HasClaim("demo", "true"))
            {
                // Use in-memory demo data
                return Ok(DemoData.KnowledgeNodes);
            }

            var nodes = _knowledgeNodeService.GetAllKnolwedgeNodes();
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

                return Ok(demoKN);
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
                CreatedAt = DateTime.UtcNow
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
                return StatusCode(500, new { message = "An unexpected error occured" });
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
                return Forbid("Update operations are disabled in demo mode.");
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
                return StatusCode(500, new { message = "An unexpected error occured" });
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
                return Forbid("Deleting operations are disabled in demo mode.");
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

            // Now delete the node
            bool nodeDeleted = _knowledgeNodeService.DeleteKnowledgeNode(id);
            if (!nodeDeleted)
                return StatusCode(500, new { message = "Node not found or delete failed." });

            return Ok(new { message = "Node and related logs deleted successfully." });
        }
    }
}
