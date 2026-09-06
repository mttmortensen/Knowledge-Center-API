using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Knowledge_Center_API.Services.Security
{
    public static class RateLimiter
    {
        // Format: routeKey = "POST:/api/login"
        // value = num of rate limits for that route
        private static readonly Dictionary<string, int> LimitsPerRoute = new()
        {
            { "POST:/api/login", 5 },
            { "POST:/api/knowledge-nodes", 20 },
            { "PUT:/api/knowledge-nodes", 20 },
            { "DELETE:/api/knowledge-nodes", 10 },
            { "POST:/api/logs", 30 },
            { "POST:/api/domains", 10 },
            { "PUT:/api/domains", 10 },
            { "DELETE:/api/domains", 10 },
            { "POST:/api/tags", 10 },
            { "PUT:/api/tags", 10 },
            { "DELETE:/api/tags", 10 }
        };

        private static readonly TimeSpan TimeWindow = TimeSpan.FromMinutes(1);

        // Tracks requests per IP per route: Dictionary<"IP|routeKey">, List<Timestamps>>
        private static readonly Dictionary<string, List<DateTime>> RequestLog = new();

        public static bool IsAllowed(HttpContext context)
        {
            string ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            string method = context.Request.Method;
            string path = context.Request.Path.Value?.ToLower() ?? string.Empty;

            // Match against configured route templates by prefix, so routes with an
            // {id} segment (e.g. "/api/knowledge-nodes/5") still match their template
            // ("PUT:/api/knowledge-nodes") instead of silently falling back to the
            // default limit and getting a fresh bucket per distinct id.
            string matchedRouteKey = null;
            int limit = 100;

            foreach (var route in LimitsPerRoute)
            {
                int separatorIndex = route.Key.IndexOf(':');
                string routeMethod = route.Key.Substring(0, separatorIndex);
                string routePath = route.Key.Substring(separatorIndex + 1);

                if (!string.Equals(routeMethod, method, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (path == routePath || path.StartsWith(routePath + "/"))
                {
                    matchedRouteKey = route.Key;
                    limit = route.Value;
                    break;
                }
            }

            // Bucket by the matched route template (not the raw path) so all requests
            // to that route share one quota regardless of which id is in the path.
            string key = $"{ip}|{matchedRouteKey ?? $"{method}:{path}"}";

            lock (RequestLog)
            {
                if (!RequestLog.ContainsKey(key))
                {
                    RequestLog[key] = new List<DateTime>();
                }

                // Clean up old requests outside the time window
                DateTime now = DateTime.UtcNow;
                RequestLog[key].RemoveAll(t => (now - t) > TimeWindow);

                // Check limit
                if (RequestLog[key].Count >= limit)
                {
                    return false; // 🚫 Rate limit exceeded
                }

                // Add current request timestamp
                RequestLog[key].Add(now);
                return true; // ✅ Allowed
            }
        }
    }
}
