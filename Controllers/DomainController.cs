using Microsoft.AspNetCore.Mvc;
using Knowledge_Center_API.Models;
using Knowledge_Center_API.Services.Core;

namespace Knowledge_Center_API.Controllers
{

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
            // Later: Adding Auth and Rate Limiting middleware later

            bool success = _domainService.CreateDomain(domain);
            if (!success)
                return StatusCode(500, new { message = "Domain creation failed. " });

            return CreatedAtAction(nameof(GetById), new { id = domain.DomainId }, domain);
        }

        // === PUT /api/domains/{id} ===
        [HttpPut("{id}")]
        public IActionResult Update(int id, [FromBody] Domain domain) 
        {
            domain.DomainId = id;

            bool success = _domainService.UpdateDomain(domain);
            if (!success)
                return StatusCode(500, new { message = "Domain update failed. " });

            return Ok(domain);
        }


        // === DELETE /api/domains/{id} ===
        [HttpDelete("{id}")]
        public IActionResult Delete(int id)
        {
            bool success = _domainService.DeleteDomain(id);
            if (!success)
                return StatusCode(500, new { message = "Domain not found or delete failed." });

            return Ok(new { message = "Domain deleted" });
        }
    }
}
