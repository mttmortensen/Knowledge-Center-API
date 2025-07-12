using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using Knowledge_Center_API.Models;
using BCrypt.Net;

namespace Knowledge_Center_API.Services.Security
{
    public class AuthHelper
    {
        public static string HashPassword(string plainPassword) 
        {
            return BCrypt.Net.BCrypt.HashPassword(plainPassword);
        }

        public static bool VerifyPassword(string plainpassword, string storedHash) 
        {
            return BCrypt.Net.BCrypt.Verify(plainpassword, storedHash);
        }

        public static string GenerateDemoToken(string jwtSecret)
        {
            // Create a token handler that can generate and write JWT tokens
            var tokenHandler = new JwtSecurityTokenHandler();

            // Convert the secret key string into a byte array for signing
            var key = Encoding.ASCII.GetBytes(jwtSecret);

            // Describe what will go into the token: user identity, claims, expiration, and signing credentials
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                // Define claims: a unique ID and a custom "demo" flag to mark this as a demo user
                Subject = new ClaimsIdentity(new[]
                {
            new Claim(ClaimTypes.NameIdentifier, "demo-user"), // identifies the user
            new Claim("demo", "true") // custom claim to flag this as a demo session
        }),

                // Set token to expire in 30 minutes
                Expires = DateTime.UtcNow.AddMinutes(30),

                // Sign the token using your app's secret key and HMAC SHA256
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            // Create the token using the handler and the descriptor
            var token = tokenHandler.CreateToken(tokenDescriptor);

            // Convert the token into a string to return to the frontend
            return tokenHandler.WriteToken(token);
        }
    }
}
