using Microsoft.AspNetCore.Mvc;
using Knowledge_Center_API.Models;
using Knowledge_Center_API.Services.Core;

namespace Knowledge_Center_API.Controllers
{
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

        // === GET /api/knowledge-nodes ===
        [HttpGet]
        public IActionResult GetAll()
        {
            var nodes = _knowledgeNodeService.GetAllNodes();
            return Ok(nodes);
        }

        // === GET /api/knowledge-nodes/{id} ===
        [HttpGet("{id}")]
        public IActionResult GetById(int id)
        {
            var node = _knowledgeNodeService.GetNodeById(id);
            if (node == null)
                return NotFound(new { message = $"Knowledge Node with ID {id} not found." });

            return Ok(node);
        }

        // === POST /api/knowledge-nodes ===
        [HttpPost]
        public IActionResult Create([FromBody] KnowledgeNode node)
        {
            // (Optional) Auth & rate limit middleware would go here

            bool success = _knowledgeNodeService.CreateNode(node);
            if (!success)
                return StatusCode(500, new { message = "Failed to create node." });

            return CreatedAtAction(nameof(GetById), new { id = node.Id }, node);
        }

        // === PUT /api/knowledge-nodes/{id} ===
        [HttpPut("{id}")]
        public IActionResult Update(int id, [FromBody] KnowledgeNode node)
        {
            node.Id = id;

            bool success = _knowledgeNodeService.UpdateNode(node);
            if (!success)
                return StatusCode(500, new { message = "Node not found or update failed." });

            return Ok(node);
        }

        // === DELETE /api/knowledge-nodes/{id} ===
        [HttpDelete("{id}")]
        public IActionResult Delete(int id)
        {
            // Delete all logs associated with the node
            bool logsDeleted = _logEntryService.DeleteAllLogEntriesByNodeId(id);
            if (!logsDeleted)
                return StatusCode(500, new { message = "Failed to delete related logs." });

            // Now delete the node
            bool nodeDeleted = _knowledgeNodeService.DeleteNode(id);
            if (!nodeDeleted)
                return StatusCode(500, new { message = "Node not found or delete failed." });

            return Ok(new { message = "Node and related logs deleted successfully." });
        }
    }
}
