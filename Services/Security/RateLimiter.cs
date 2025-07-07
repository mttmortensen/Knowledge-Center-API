using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

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

        public static bool IsAllowed(HttpListenerRequest request)
        {
            string ip = request.RemoteEndPoint?.Address.ToString() ?? "unknown";
            string routeKey = $"{request.HttpMethod}:{request.Url.AbsolutePath.ToLower()}";
            string key = $"{ip}|{routeKey}";

            // default limit
            int limit = LimitsPerRoute.ContainsKey(routeKey) ? LimitsPerRoute[routeKey] : 100;

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
