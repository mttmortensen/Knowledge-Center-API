using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Knowledge_Center_API.Services.Security
{
    public class AuthSession
    {
        // username -> token
        private static Dictionary<string, string> _activeSessions = new();

        public static string CreateSession(string username) 
        {
            string token = Guid.NewGuid().ToString();
            _activeSessions[username] = token;
            return token;
        }

        public static bool IsValidToken(string token) 
        {
            return _activeSessions.ContainsValue(token);
        }

        public static void EndSession(string username) 
        {
            _activeSessions.Remove(username);
        }

        public static string GetUsernameByToken(string token)
        {
            foreach (var kvp in _activeSessions)
            {
                if (kvp.Value == token)
                    return kvp.Key;
            }

            return null;
        }
    }
}
