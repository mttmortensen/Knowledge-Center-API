using Knowledge_Center_API.Models;
using Knowledge_Center_API.Services.Core;
using Knowledge_Center_API.Services.Security;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Knowledge_Center_API.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : Controller
    {
        private readonly UserService _userService;

        public AuthController(UserService userService)
        {
            _userService = userService;            
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest loginData)
        {    
            // === Step 0: Rate limit check ===
            if (!RateLimiter.IsAllowed(HttpContext))
            {
                return StatusCode(429, new { message = "Rate limit exceeded. Try again later." });
            }

            try
            {
                // === Step 1: Null check on loginData itself ===
                if (loginData == null)
                {
                    return BadRequest(new { message = "Missing login data." });
                }

                // === Step 2: Authenticate ===
                var user = _userService.AuthenticateUser(loginData.Username, loginData.Password);

                if (!user.IsAuthenticated)
                {
                    return BadRequest(new { message = "Invalid username or password." });
                }

                // === Step 3: Return Token ===
                return Ok(new { token = user.Token });

            }
            catch (ValidationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch
            {
                return StatusCode(500, new { message = "Login request failed." });
            }
        }

        [HttpPost("logout")]
        public IActionResult Logout() 
        {
            return Ok();
        }
    }
}
