using Knowledge_Center_API.DataAccess.Demo;
using Knowledge_Center_API.Models;
using Knowledge_Center_API.Models.DTOs;
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
            // Demo mode: Read-only
            if (User.HasClaim("demo", "true"))
            {
                // Use in-memory demo data
                return Ok(DemoData.Domains);
            }


            var domains = _domainService.GetAllDomains();
            return Ok(domains);
        }

        // === GET /api/domains/{id} ===
        [HttpGet("{id}")]
        public IActionResult GetById(int id)
        {
            // Demo mode: Read-only
            if (User.HasClaim("demo", "true"))
            {
                Domain demoDomain = DemoData.Domains.FirstOrDefault(d => d.DomainId == id);

                if (demoDomain == null)
                    return NotFound($"Demo domain with ID {id} is not found");

                return Ok(demoDomain);
            }

            var domain = _domainService.GetDomainByIdWithKNs(id);
            if (domain == null)
                return NotFound(new { message = $"Domain with ID {id} not found." });

            return Ok(domain);
        }

        // === POST /api/domains ===
        [HttpPost]
        public IActionResult Create([FromBody] Domain domain)
        {
            // Check for demo mode, return fake object if true
            var demoResult = AuthHelper.HandleDemoCreate(User, () => new Domain
            {
                DomainId = domain.DomainId,
                DomainName = domain.DomainName,
                DomainDescription = domain.DomainDescription,
                DomainStatus = domain.DomainStatus,
                CreatedAt = DateTime.UtcNow
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
        public IActionResult Update(int id, [FromBody] DomainUpdateDto domain)
        {
            // Demo mode: Updating is disabled
            if (User.HasClaim("demo", "true"))
            {
                return Forbid("Update operations are disabled in demo mode.");
            }

            // === Step 0: Rate Limit Check ===
            if (!RateLimiter.IsAllowed(HttpContext))
            {
                return StatusCode(429, new { message = "Rate limit exceeded. Try again later." });
            }

            try
            {
                // === Step 1: Call service — it handles FieldValidator logic ===
                domain.DomainId = id;

                bool success = _domainService.UpdateDomainFromDto(domain.DomainId, domain);
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

            bool success = _domainService.DeleteDomain(id);
            if (!success)
                return StatusCode(500, new { message = "Domain not found or delete failed." });

            return Ok(new { message = "Domain deleted" });
        }
    }
}
