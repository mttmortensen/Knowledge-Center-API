using Knowledge_Center_API.Models;
using Knowledge_Center_API.Services.Core;
using Knowledge_Center_API.Services.Security;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Knowledge_Center_API.Controllers
{
    /// <summary>
    /// Handles authentication operations, including login, logout, and demo access.
    /// </summary>
    [ApiController]
    [Route("api/auth")]
    public class AuthController : Controller
    {
        private readonly UserService _userService;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthController"/> class.
        /// </summary>
        /// <param name="userService">The user service for authentication logic.</param>
        public AuthController(UserService userService)
        {
            _userService = userService;
        }

        /// <summary>
        /// Authenticates a user and returns a JWT token if successful.
        /// </summary>
        /// <param name="loginData">The user's login credentials (username and password).</param>
        /// <returns>
        /// 200 OK with a JWT token if authentication succeeds.
        /// 400 Bad Request if credentials are invalid.
        /// 429 Too Many Requests if rate limit exceeded.
        /// 500 Internal Server Error if authentication fails unexpectedly.
        /// </returns>
        /// <response code="200">Returns a JWT token for the authenticated user.</response>
        /// <response code="400">Invalid username or password.</response>
        /// <response code="429">Rate limit exceeded.</response>
        /// <response code="500">Authentication process failed.</response>
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

        /// <summary>
        /// Logs out a user by invalidating their JWT token.
        /// </summary>
        /// <returns>
        /// 200 OK if the logout was successful.
        /// 401 Unauthorized if the token is invalid or missing.
        /// </returns>
        /// <response code="200">Logout successful.</response>
        /// <response code="401">Invalid or missing token.</response>
        [HttpPost("logout")]
        public IActionResult Logout()
        {
            // Step 1: Get the Authorization header from the request
            string authHeader = Request.Headers["Authorization"].ToString();

            // Step 2: Check if the header is missing or doesn't start with "Bearer "
            if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer "))
                return Unauthorized(new { message = "Missing or invalid Authorization Header. " });

            // Step 3: Extract the token from the header
            string token = authHeader.Substring("Bearer ".Length);

            // Step 4: Find the username associated with this token
            string username = AuthSession.GetUsernameByToken(token);

            // Step 5: If the token is valid and a username is found, end the session
            if(username != null) 
            {
                AuthSession.EndSession(username);
                return Ok(new { message = "Logout Successfull. " });
            }

            // If no matching session found, return unauthorized
            return Unauthorized(new { messsage = "Invalid Token. " });
        }

        /// <summary>
        /// Generates a demo JWT token for temporary access (for testing purposes only).
        /// </summary>
        /// <returns>
        /// 200 OK with a temporary JWT token.
        /// 429 Too Many Requests if rate limit exceeded.
        /// 500 Internal Server Error if JWT secret is missing or another error occurs.
        /// </returns>
        /// <response code="200">Returns a demo token.</response>
        /// <response code="429">Rate limit exceeded.</response>
        /// <response code="500">JWT secret missing or internal error occurred.</response>
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
