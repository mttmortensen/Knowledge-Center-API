namespace Knowledge_Center_API.DataAccess
{
    public static class UserQueries
    {
        public static readonly string GetUserByUsername = @"
            SELECT * FROM ""Users""
            WHERE ""Username"" = @Username;
        ";
    }
}
