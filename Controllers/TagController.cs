using Knowledge_Center_API.DataAccess.Demo;
using Knowledge_Center_API.Models;
using Knowledge_Center_API.Services.Core;
using Knowledge_Center_API.Services.Security;
using Microsoft.AspNetCore.Mvc;

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
            // Demo mode: Read-only
            if (User.HasClaim("demo", "true"))
            {
                // Use in-memory demo data
                return Ok(DemoData.Tags);
            }

            var tags = _tagService.GetAllTags();
            return Ok(tags);
        }

        // === GET /api/tags/{id} ===
        [HttpGet("{id}")]
        public IActionResult GetById(int id)
        {
            // Demo mode: Read-only
            if (User.HasClaim("demo", "true"))
            {
                Tags demoTag = DemoData.Tags.FirstOrDefault(tg => tg.TagId == id);

                if (demoTag == null)
                    return NotFound($"Demo Log Entry with ID {id} is not found");

                return Ok(demoTag);
            }

            var tag = _tagService.GetTagById(id);
            if (tag == null)
                return NotFound(new { message = $"Tag with ID {id} not found." });

            return Ok(tag);
        }

        // === POST /api/tags ===
        [HttpPost]
        public IActionResult Create([FromBody] Tags tag)
        {
            // Demo mode: Creating is disabled
            if (User.HasClaim("demo", "true"))
            {
                return Forbid("Write operations are disabled in demo mode.");
            }

            // Rate Limit Check
            if (!RateLimiter.IsAllowed(HttpContext))
            {
                return StatusCode(429, new { message = "Rate limit exceeded. Try again later." });
            }

            try
            {
                // === Step 1: Call service — it handles FieldValidator logic ===
                bool success = _tagService.CreateTag(tag);
                if (!success)
                    return StatusCode(500, new { message = "Failed to create tag." });

                return CreatedAtAction(nameof(GetById), new { id = tag.TagId }, tag);
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

        // === PUT /api/tags/{id} ===
        [HttpPut("{id}")]
        public IActionResult Update(int id, [FromBody] Tags tag)
        {
            // Demo mode: Updating is disabled
            if (User.HasClaim("demo", "true"))
            {
                return Forbid("Updating operations are disabled in demo mode.");
            }

            // Rate Limit Check
            if (!RateLimiter.IsAllowed(HttpContext))
            {
                return StatusCode(429, new { message = "Rate limit exceeded. Try again later." });
            }

            try
            {
                // === Step 1: Call service — it handles FieldValidator logic ===
                tag.TagId = id;

                bool success = _tagService.UpdateTag(tag);
                if (!success)
                    return StatusCode(500, new { message = "Failed to update tag." });

                return Ok(tag);
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

        // === DELETE /api/tags/{id} ===
        [HttpDelete("{id}")]
        public IActionResult Delete(int id)
        {
            // Demo mode: Deletion is disabled
            if (User.HasClaim("demo", "true"))
            {
                return Forbid("Deleting operations are disabled in demo mode.");
            }

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