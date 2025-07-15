using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

namespace Knowledge_Center_API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            /* =======================================================
             * KESTREL CONFIGURATION
             * ======================================================= */
            builder.WebHost.ConfigureKestrel((context, options) =>
            {
                options.Configure(context.Configuration.GetSection("Kestrel"));
            });

            /* =======================================================
             * DATABASE SETUP
             * ======================================================= */
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
            builder.Services.AddSingleton(new DataAccess.Database(connectionString));

            /* =======================================================
             * DEPENDENCY INJECTION (SERVICES)
             * ======================================================= */
            builder.Services.AddScoped<Services.Core.KnowledgeNodeService>();
            builder.Services.AddScoped<Services.Core.DomainService>();
            builder.Services.AddScoped<Services.Core.LogEntryService>();
            builder.Services.AddScoped<Services.Core.TagService>();
            builder.Services.AddScoped<Services.Core.UserService>();

            /* =======================================================
             * CORS CONFIGURATION
             * ======================================================= */
            const string KCFrontendCors = "KCFrontendCors";
            builder.Services.AddCors(options =>
            {
                options.AddPolicy(name: KCFrontendCors, policy =>
                {
                    policy.WithOrigins(
                        "https://kc.mortensens.xyz",
                        "http://localhost:8081",
                        "http://localhost:3000"
                    )
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
                });
            });

            /* =======================================================
             * JWT AUTHENTICATION
             * ======================================================= */
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

            /* =======================================================
             * CONTROLLERS & JSON OPTIONS
             * ======================================================= */
            builder.Services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.PropertyNamingPolicy = null;
                });

            /* =======================================================
             * SWAGGER / OPENAPI
             * ======================================================= */
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "Knowledge Center API",
                    Version = "v1"
                });

                // Base URL for reverse proxy
                options.AddServer(new OpenApiServer { Url = "/kc" });

                // Enable XML comments
                var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                options.IncludeXmlComments(xmlPath);

                // Add JWT Auth to Swagger UI
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "JWT Authorization header using the Bearer scheme. Example: 'Bearer {token}'",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT"
                });

                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        new string[] {}
                    }
                });
            });

            /* =======================================================
             * BUILD THE APP
             * ======================================================= */
            var app = builder.Build();

            // Base Path for API (reverse proxy scenario)
            app.UsePathBase("/kc");
            app.UseStaticFiles();

            /* =======================================================
             * SWAGGER UI
             * ======================================================= */
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/kc/swagger/v1/swagger.json", "Knowledge Center API v1");
                c.RoutePrefix = "swagger"; // Access at /kc/swagger
            });

            /* =======================================================
             * MIDDLEWARE PIPELINE
             * ======================================================= */
            app.UseHttpsRedirection();
            app.UseCors(KCFrontendCors);
            app.UseAuthentication();
            app.UseAuthorization();

            /* =======================================================
             * ENDPOINT MAPPING
             * ======================================================= */
            app.MapControllers();

            app.Run();
        }
    }
}
