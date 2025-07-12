using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
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
    }
}
