using Knowledge_Center_API.DataAccess.Demo;
using Knowledge_Center_API.Models.Tags;
using Knowledge_Center_API.Services.Core;
using Knowledge_Center_API.Services.Security;
using Microsoft.AspNetCore.Mvc;

namespace Knowledge_Center_API.Controllers
{

    /// <summary>
    /// Manages tag-related operations.
    /// Requires JWT authentication.
    /// </summary>
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

        /// <summary>
        /// Retrieves all tags.
        /// </summary>
        /// <returns>200 OK with a list of tags.</returns>
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

        /// <summary>
        /// Retrieves a tag by its ID.
        /// </summary>
        /// <param name="id">Tag ID.</param>
        /// <returns>200 OK with the tag details or 404 if not found.</returns>
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

        /// <summary>
        /// Creates a new tag.
        /// </summary>
        /// <param name="tag">Tag details.</param>
        /// <returns>201 Created with the created tag.</returns>
        [HttpPost]
        public IActionResult Create([FromBody] Tags tag)
        {
            // Check for demo mode, return fake object if true
            var demoResult = AuthHelper.HandleDemoCreate(User, () => new Tags
            {
                TagId = tag.TagId,
                Name = tag.Name
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

        /// <summary>
        /// Updates an existing tag.
        /// </summary>
        /// <param name="id">Tag ID.</param>
        /// <param name="tag">Updated tag details.</param>
        /// <returns>200 OK if update is successful.</returns>
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

        /// <summary>
        /// Deletes a tag by its ID.
        /// </summary>
        /// <param name="id">Tag ID.</param>
        /// <returns>204 No Content if deleted successfully.</returns>
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