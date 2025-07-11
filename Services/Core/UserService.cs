using Knowledge_Center_API.Models;

namespace Knowledge_Center_API.Services.Core
{
    public class UserService
    {
        
        
        
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
