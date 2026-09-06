using System;
using System.Collections.Concurrent;
using System.Linq;

namespace Knowledge_Center_API.Services.Security
{
    public class AuthSession
    {
        private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(8);

        // username -> session (thread-safe, since requests can create/read/end sessions concurrently)
        private static readonly ConcurrentDictionary<string, SessionEntry> _activeSessions = new();

        public static string CreateSession(string username)
        {
            string token = Guid.NewGuid().ToString();
            _activeSessions[username] = new SessionEntry(token, DateTime.UtcNow.Add(SessionLifetime));
            return token;
        }

        public static bool IsValidToken(string token)
        {
            var now = DateTime.UtcNow;
            return _activeSessions.Values.Any(s => s.Token == token && s.ExpiresAt > now);
        }

        public static void EndSession(string username)
        {
            _activeSessions.TryRemove(username, out _);
        }

        public static string GetUsernameByToken(string token)
        {
            var now = DateTime.UtcNow;
            foreach (var kvp in _activeSessions)
            {
                if (kvp.Value.Token == token && kvp.Value.ExpiresAt > now)
                    return kvp.Key;
            }

            return null;
        }

        private sealed class SessionEntry
        {
            public string Token { get; }
            public DateTime ExpiresAt { get; }

            public SessionEntry(string token, DateTime expiresAt)
            {
                Token = token;
                ExpiresAt = expiresAt;
            }
        }
    }
}
