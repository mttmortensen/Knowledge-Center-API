using Microsoft.AspNetCore.Mvc;
using Knowledge_Center_API.Models;
using Knowledge_Center_API.Services.Core;

namespace Knowledge_Center_API.Controllers
{
    [ApiController]
    [Route("api/tags")]
    public class TagController : ControllerBase
    {
        private readonly TagService _tagService;

        public TagController(TagService tagService)
        {
            _tagService = tagService;
        }
    }

            // === GET /api/tags ===
        [HttpGet]
        public IActionResult GetAll()
        {
            var tags = _tagService.GetAllTags();
            return Ok(tags);
        }

        // === GET /api/tags/{id} ===
        [HttpGet("{id}")]
        public IActionResult GetById(int id)
        {
            var tag = _tagService.GetTagById(id);
            if (tag == null)
                return NotFound(new { message = $"Tag with ID {id} not found." });

            return Ok(tag);
        }

    }
