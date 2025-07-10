using Knowledge_Center_API.Models;
using Knowledge_Center_API.Services.Core;
using Knowledge_Center_API.Services.Security;
using Microsoft.AspNetCore.Mvc;
using System.Xml.Linq;

namespace Knowledge_Center_API.Controllers
{
    [RequireToken]
    [ApiController]
    [Route("api/domains")]
    public class DomainController : ControllerBase
    {
        private readonly DomainService _domainService;

        public DomainController(DomainService domainService)
        {
            _domainService = domainService;
        }

        // === GET /api/domains ===
        [HttpGet]
        public IActionResult GetAll()
        {
            var domains = _domainService.GetAllDomains();
            return Ok(domains);
        }

        // === GET /api/domains/{id} ===
        [HttpGet("{id}")]
        public IActionResult GetById(int id)
        {
            var domain = _domainService.GetDomainById(id);
            if (domain == null)
                return NotFound(new { message = $"Domain with ID {id} not found." });

            return Ok(domain);
        }

        // === POST /api/domains ===
        [HttpPost]
        public IActionResult Create([FromBody] Domain domain)
        { 
            // Rate Limit Check
            if (!RateLimiter.IsAllowed(HttpContext))
            {
                return StatusCode(429, new { message = "Rate limit exceeded. Try again later." });
            }

            try
            {
                // === Step 1: Call service — it handles FieldValidator logic ===
                bool success = _domainService.CreateDomain(domain);
                if (!success)
                    return StatusCode(500, new { message = "Domain creation failed. " });

                return CreatedAtAction(nameof(GetById), new { id = domain.DomainId }, domain);
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

        // === PUT /api/domains/{id} ===
        [HttpPut("{id}")]
        public IActionResult Update(int id, [FromBody] Domain domain)
        {            
            // === Step 0: Rate Limit Check ===
            if (!RateLimiter.IsAllowed(HttpContext))
            {
                return StatusCode(429, new { message = "Rate limit exceeded. Try again later." });
            }

            try
            {
                // === Step 1: Call service — it handles FieldValidator logic ===
                domain.DomainId = id;

                bool success = _domainService.UpdateDomain(domain);
                if (!success)
                    return StatusCode(500, new { message = "Domain update failed. " });

                return Ok(domain);
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


        // === DELETE /api/domains/{id} ===
        [HttpDelete("{id}")]
        public IActionResult Delete(int id)
        {
            // Rate Limit Check
            if (!RateLimiter.IsAllowed(HttpContext))
            {
                return StatusCode(429, new { message = "Rate limit exceeded. Try again later." });
            }

            bool success = _domainService.DeleteDomain(id);
            if (!success)
                return StatusCode(500, new { message = "Domain not found or delete failed." });

            return Ok(new { message = "Domain deleted" });
        }
    }
}
