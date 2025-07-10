using Knowledge_Center_API.Models;
using Microsoft.AspNetCore.Mvc;

namespace Knowledge_Center_API.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : Controller
    {
        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest loginData) 
        {
            return Ok();
        }

        [HttpPost("logout")]
        public IActionResult Logout() 
        {
            return Ok();
        }
    }
}
