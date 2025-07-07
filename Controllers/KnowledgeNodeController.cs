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

        // === GET /api/knowledgeNodes ===
        [HttpGet]
        public IActionResult GetAll()
        {
            var nodes = _knowledgeNodeService.GetAllNodes();
            return Ok(nodes);
        }

        // === GET /api/knowledgeNodes/{id} ===
        [HttpGet("{id}")]
        public IActionResult GetById(int id)
        {
            var node = _knowledgeNodeService.GetNodeById(id);
            if (node == null)
                return NotFound(new { message = $"Knowledge Node with ID {id} not found." });

            return Ok(node);
        }

    }
}
