using Microsoft.AspNetCore.Mvc.Testing;

namespace Knowledge_Center_API.Tests
{
    /*
     * Boots the real API in-process. Program.cs reads JWT_SECRET, KC_UPLOAD_DIR and
     * the connection string from the environment, so they're set before it starts.
     * Without KC_TEST_DB the connection string points nowhere, which is fine for
     * demo-mode tests: they never touch the database.
     */
    public class TestApiFactory : WebApplicationFactory<Program>
    {
        public const string JwtSecret = "learn-tests-jwt-secret-at-least-32-bytes!!";

        public TestApiFactory()
        {
            Environment.SetEnvironmentVariable("JWT_SECRET", JwtSecret);
            Environment.SetEnvironmentVariable("KC_UPLOAD_DIR", Path.Combine(Path.GetTempPath(), "kc-learn-tests-uploads"));
            Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection",
                TestDatabase.ConnectionString ?? "Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none");
        }
    }
}
