using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Knowledge_Center_API.Services.Security
{
    // Custom attribute to enforce authentication for API endpoints.
    // Supports both:
    // - Session-based tokens for real users (AuthSession)
    // - JWT-based tokens for demo users (with "demo" claim)

    public class RequireTokenAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            // Get the Authorization header from the request
            string authHeader = context.HttpContext.Request.Headers["Authorization"].ToString();

            // Validate header format and presence
            if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                context.Result = new UnauthorizedObjectResult(new { message = "Missing or invalid Authorization header." });
                return;
            }

            // Extract the token portion from "Bearer <token>"
            string token = authHeader.Substring("Bearer ".Length);

            // === Option A: Try to interpret token as a JWT for demo users ===
            try
            {
                // Decode the token without verifying signature (we're just inspecting claims)
                var handler = new JwtSecurityTokenHandler();
                var jwt = handler.ReadJwtToken(token);

                // Check for the custom "demo" claim
                var isDemo = jwt.Claims.Any(c => c.Type == "demo" && c.Value == "true");

                if (isDemo)
                    return; // Valid demo token — allow access
            }
            catch
            {
                // Ignore errors and fall back to session token logic
            }

            // === Option B: Fallback to session token validation for real users ===
            if (!AuthSession.IsValidToken(token))
            {
                // Token not found in active session map — deny access
                context.Result = new UnauthorizedObjectResult(new { message = "Invalid token." });
            }
        }
    }
}
