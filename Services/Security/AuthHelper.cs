using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Knowledge_Center_API.Models;

namespace Knowledge_Center_API.Services.Security
{
    public class AuthHelper
    {
        public static bool RequireAuth(HttpListenerRequest request, HttpListenerResponse response) 
        {
            string authHeader = request.Headers["Authorization"];

            if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ")) 
            {
                response.StatusCode = (int)HttpStatusCode.Unauthorized;
                byte[] error = System.Text.Encoding.UTF8.GetBytes("Missing or invalid Authorization header.");
                response.OutputStream.Write(error, 0, error.Length);
                response.OutputStream.Close();
                return false;
            }

            string token = authHeader.Substring("Bearer ".Length);

            if (!AuthSession.IsValidToken(token)) 
            {
                response.StatusCode = (int)HttpStatusCode.Unauthorized;
                byte[] error = System.Text.Encoding.UTF8.GetBytes("Invalid token.");
                response.OutputStream.Write(error, 0, error.Length);
                response.OutputStream.Close();
                return false;
            }

            return true;
        }

        public static (string Username, string Password) LoadTestCredentials() 
        {
            string path = "test-auth.json";
            if (!File.Exists(path)) 
            {
                throw new FileNotFoundException("Missing test-auth.json with test creds");
            }

            string json = File.ReadAllText(path);
            LoginRequest creds = JsonSerializer.Deserialize<LoginRequest>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return (creds.Username, creds.Password);
        }
    }
}
