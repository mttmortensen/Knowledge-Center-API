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
            // Step 1: Get the Authorization header from the request
            // Step 2: Check if the header is missing or doesn't start with "Bearer "
            // Step 3: Extract the token from the header
            // Step 4: Find the username associated with this token
            // Step 5: If the token is valid and a username is found, end the session
            // If no matching session found, return unauthorized
        }

        [HttpPost("demo")]
        public IActionResult DemoLogin() 
        {
            // === Step 0: Rate limit to prevent abuse ===
            if (!RateLimiter.IsAllowed(HttpContext))
            {
                return StatusCode(429, new { message = "Rate limit exceeded. Try again later." });
            }

            try 
            {
                // === Step 1: Get the JWT secret from environment variables ===
                var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET");

                // === Step 2: Fail safely if not set ===
                if (string.IsNullOrWhiteSpace(jwtSecret))
                    return StatusCode(500, new { message = "Server misconfigured. JWT Secret missing." });

                // === Step 3: Generate a temporary JWT token for the demo user ===
                var demoToken = AuthHelper.GenerateDemoToken(jwtSecret);

                // === Step 4: Return the token to the frontend ===
                return Ok(new 
                {
                    token = demoToken,
                    isDemo = true
                });

            }
            catch 
            {
                // === Catch all ===
                return StatusCode(500, new { message = "Demo login failed. " });
            }
        }
    }
}
