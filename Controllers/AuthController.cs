using Knowledge_Center_API.Models;
using Knowledge_Center_API.Services.Security;
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
            try
            {
                // === Step 1: Validate (sortof) login data
                if (loginData == null || string.IsNullOrWhiteSpace(loginData.Username) || string.IsNullOrWhiteSpace(loginData.Password))
                {
                    return BadRequest(new { message = "Missing Creds" });
                }

                // === Step 2: Authenticate
                // This avoids hard coded creds
                var (expectedUsername, expectedPassword) = AuthHelper.LoadTestCredentials();

                if (loginData.Username == expectedUsername && loginData.Password == expectedPassword) 
                {
                    string token = AuthSession.CreateSession(expectedUsername);
                    return Ok(new { token });
                }
                else 
                {
                    return BadRequest(new { message = "Invalid Creds" });
                }
            }
            catch 
            {
                return StatusCode(500, new { messsage = "Login Request Failed." });
            }
        }

        [HttpPost("logout")]
        public IActionResult Logout() 
        {
            return Ok();
        }
    }
}
