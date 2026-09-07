using Knowledge_Center_API.DataAccess;
using Knowledge_Center_API.Models.Auth;
using Knowledge_Center_API.Services.Security;
using Knowledge_Center_API.Services.Validation;
using Npgsql;

namespace Knowledge_Center_API.Services.Core
{
    public class UserService
    {
        private readonly Database _db;

        // A precomputed hash with no matching plaintext, used to keep the bcrypt
        // comparison cost the same when a username isn't found — otherwise a
        // nonexistent username short-circuits (skips the hash check) while a real
        // one doesn't, and that timing difference can be used to enumerate usernames.
        private static readonly string DummyPasswordHash = AuthHelper.HashPassword(Guid.NewGuid().ToString());

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
            List<NpgsqlParameter> parameters = new List<NpgsqlParameter>
            {
                new NpgsqlParameter("@Username", username)
            };

            var result = _db.ExecuteQuery(UserQueries.GetUserByUsername, parameters);

            bool userExists = result.Count > 0;
            var user = userExists ? ConvertDBRowToUser(result[0]) : new User { IsAuthenticated = false };
            string hashToVerify = userExists ? user.PasswordHash : DummyPasswordHash;

            // Always run the bcrypt comparison, even for an unknown username, so the
            // response time doesn't reveal whether the username exists.
            bool passwordMatches = AuthHelper.VerifyPassword(password, hashToVerify);

            if (!userExists || !passwordMatches)
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
