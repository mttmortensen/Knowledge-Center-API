using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Knowledge_Center_API.Services.Security
{
    public class RequireTokenAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            string authHeader = context.HttpContext.Request.Headers["Authorization"].ToString();

            if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ")) 
            {
                context.Result = new UnauthorizedObjectResult(new { message = "Missing or invalid Authorization header." });
                return;
            }

            string token = authHeader.Substring("Bearer ".Length);

            if (!AuthSession.IsValidToken(token))
                context.Result = new UnauthorizedObjectResult(new { message = "Invalid token." });

        }
    }
}
