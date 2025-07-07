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
    }
}
