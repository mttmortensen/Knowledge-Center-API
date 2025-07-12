using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Knowledge_Center_API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // === Custom Kestrel port ===
            builder.WebHost.ConfigureKestrel((context, options) =>
            {
                options.Configure(context.Configuration.GetSection("Kestrel"));
            });

            // === Inject raw connection string into custom Database class ===
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
            builder.Services.AddSingleton(new DataAccess.Database(connectionString));

            // === Register your Services ===
            builder.Services.AddScoped<Services.Core.KnowledgeNodeService>();
            builder.Services.AddScoped<Services.Core.DomainService>();
            builder.Services.AddScoped<Services.Core.LogEntryService>();
            builder.Services.AddScoped<Services.Core.TagService>();
            builder.Services.AddScoped<Services.Core.UserService>();

            // === Add CORS Policy ===
            string KCFrontendCors = "KCFrontendCors";
            builder.Services.AddCors(options =>
            {
                options.AddPolicy(name: KCFrontendCors, policy =>
                {
                    policy.WithOrigins("https://kc.mortensens.xyz", "http://localhost:8081", "http://localhost:3000")
                          .AllowAnyHeader()
                          .AllowAnyMethod();
                });
            });

            // === JWT Authentication Setup for Demo Mode ===
            var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET");
            if (string.IsNullOrEmpty(jwtSecret))
            {
                throw new Exception("JWT_SECRET environment variable is not set.");
            }

            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(jwtSecret)),
                        ValidateIssuer = false,
                        ValidateAudience = false
                    };
                });

            // === Add Controllers and Swagger ===
            builder.Services.AddControllers()
                .AddJsonOptions(options =>
                {
                    // I don't want camel case
                    options.JsonSerializerOptions.PropertyNamingPolicy = null;
                });

            builder.Services.AddOpenApi();

            var app = builder.Build();

            // === Dev Swagger ===
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            app.UseHttpsRedirection();

            // === Apply CORS before routing ===
            app.UseCors(KCFrontendCors);
            app.UsePathBase("/kc");

            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();

            app.Run();
        }
    }
}
