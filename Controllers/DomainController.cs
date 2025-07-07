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
    }
}
