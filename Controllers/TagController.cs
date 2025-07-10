using Microsoft.AspNetCore.Mvc;
using Knowledge_Center_API.Models;
using Knowledge_Center_API.Services.Core;
using Knowledge_Center_API.Services.Security;

namespace Knowledge_Center_API.Controllers
{
    [RequireToken]
    [ApiController]
    [Route("api/tags")]
    public class TagController : ControllerBase
    {
        private readonly TagService _tagService;

        public TagController(TagService tagService)
        {
            _tagService = tagService;
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

        // === POST /api/tags ===
        [HttpPost]
        public IActionResult Create([FromBody] Tags tag)
        {
            // Rate Limit Check
            if (!RateLimiter.IsAllowed(HttpContext))
            {
                return StatusCode(429, new { message = "Rate limit exceeded. Try again later." });
            }

            bool success = _tagService.CreateTag(tag);
            if (!success)
                return StatusCode(500, new { message = "Failed to create tag." });

            return CreatedAtAction(nameof(GetById), new { id = tag.TagId }, tag);
        }

        // === PUT /api/tags/{id} ===
        [HttpPut("{id}")]
        public IActionResult Update(int id, [FromBody] Tags tag)
        {
            // Rate Limit Check
            if (!RateLimiter.IsAllowed(HttpContext))
            {
                return StatusCode(429, new { message = "Rate limit exceeded. Try again later." });
            }

            tag.TagId = id;

            bool success = _tagService.UpdateTag(tag);
            if (!success)
                return StatusCode(500, new { message = "Failed to update tag." });

            return Ok(tag);
        }

        // === DELETE /api/tags/{id} ===
        [HttpDelete("{id}")]
        public IActionResult Delete(int id)
        {
            // Rate Limit Check
            if (!RateLimiter.IsAllowed(HttpContext))
            {
                return StatusCode(429, new { message = "Rate limit exceeded. Try again later." });
            }

            bool success = _tagService.DeleteTag(id);
            if (!success)
                return NotFound(new { message = "Tag not found or delete failed." });

            return NoContent();
        }

    }
}