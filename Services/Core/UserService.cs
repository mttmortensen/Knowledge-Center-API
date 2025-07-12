using Knowledge_Center_API.DataAccess;
using Knowledge_Center_API.Models;
using Knowledge_Center_API.Services.Security;
using Knowledge_Center_API.Services.Validation;
using Microsoft.Data.SqlClient;

namespace Knowledge_Center_API.Services.Core
{
    public class UserService
    {
        private readonly Database _db;

        public UserService(Database db)
        {
            _db = db;
        }

        /* ==== Validate User ==== */
        public User AuthenticateUser(string username, string password) 
        {
            // Validate Inputs 
            FieldValidator.ValidateRequiredString(username, "Username", 100);
            FieldValidator.ValidateRequiredString(password, "Password", 100);

            // Query User from DB
            List<SqlParameter> parameters = new List<SqlParameter> 
            {
                new SqlParameter("@Username", username)
            };

            var result = _db.ExecuteQuery(UserQueries.GerUserByUsername, parameters);

            if (result.Count == 0)
                return new User { IsAuthenticated = false };

            var user = ConvertDBRowToUser(result[0]);

            // Verifying password
            if (!AuthHelper.VerifyPassword(password, user.PasswordHash)) 
            {
                user.IsAuthenticated = false;
                return user;
            }

            // Generating Token
            user.Token = AuthSession.CreateSession(user.Username);
            user.IsAuthenticated = true;
            return user;
        }
        
        
        /* ===================== DATA TYPE CONVERTERS (MAPPERS) ===================== */
        private User ConvertDBRowToUser(Dictionary<string, object> row)
        {
            return new User
            {
                Id = Convert.ToInt32(row["Id"]),
                Username = row["Username"].ToString(),
                PasswordHash = row["PasswordHash"].ToString()
                // IsAuthenticated and Token are set during login
            };
        }
    }
}
