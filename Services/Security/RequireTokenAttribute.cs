using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

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

            // === Option A: JWT bearer tokens (e.g. demo tokens) ===
            // The JWT authentication middleware (configured in Program.cs) already ran earlier
            // in the pipeline and verified the signature/expiry before populating HttpContext.User.
            // A request only reaches here as "authenticated" if that verification succeeded, so we
            // can trust it directly instead of re-parsing the token ourselves without checking its signature.
            if (context.HttpContext.User.Identity?.IsAuthenticated == true)
                return;

            // === Option B: Fallback to session token validation for real users ===
            if (!AuthSession.IsValidToken(token))
            {
                // Token not found in active session map — deny access
                context.Result = new UnauthorizedObjectResult(new { message = "Invalid token." });
            }
        }
    }
}
