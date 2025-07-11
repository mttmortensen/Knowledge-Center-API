namespace Knowledge_Center_API.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string PasswordHash { get; set; }
        
        // Auth flow helpers
        public bool IsAuthenticated { get; set; } = false;
        public string Token { get; set; } = null;
    }
}
