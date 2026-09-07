using Knowledge_Center_API.Services.Core;
using Knowledge_Center_API.Services.Security;
using Microsoft.AspNetCore.Mvc;

namespace Knowledge_Center_API.Controllers
{
    /// <summary>
    /// Handles image uploads for embedding in log entry content.
    /// Files are stored on local disk and served back via static file middleware
    /// at /kc/uploads/{fileName}.
    /// </summary>
    [RequireToken]
    [ApiController]
    [Route("api/images")]
    public class ImagesController : ControllerBase
    {
        private readonly ImageService _imageService;

        public ImagesController(ImageService imageService)
        {
            _imageService = imageService;
        }

        /// <summary>
        /// Uploads an image and returns its public URL.
        /// </summary>
        /// <param name="file">The image file (PNG, JPEG, GIF, or WEBP; max 10 MB).</param>
        /// <returns>201 Created with the image's public URL.</returns>
        /// <response code="201">Image uploaded successfully.</response>
        /// <response code="400">Invalid or missing file.</response>
        /// <response code="429">Rate limit exceeded.</response>
        /// <response code="500">Server error during upload.</response>
        [HttpPost]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            // Demo mode: fake the response, never touch disk.
            if (User.HasClaim("demo", "true"))
            {
                return Ok(new { url = "https://placehold.co/600x400?text=Demo+Image" });
            }

            if (!RateLimiter.IsAllowed(HttpContext))
            {
                return StatusCode(429, new { message = "Rate limit exceeded. Try again later." });
            }

            try
            {
                string fileName = await _imageService.SaveImageAsync(file);
                string url = $"{Request.Scheme}://{Request.Host}{Request.PathBase}/uploads/{fileName}";

                return Created(url, new { url });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Image upload failed." });
            }
        }
    }
}
