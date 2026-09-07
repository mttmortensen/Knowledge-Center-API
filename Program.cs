using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.FileProviders;
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
             * IMAGE UPLOAD STORAGE
             * KC_UPLOAD_DIR should be set in production (e.g. a dedicated
             * directory on MRTN-LAPPS's disk). Falls back to a local
             * "uploads" folder next to the app for development.
             * ======================================================= */
            var uploadDirectory = Environment.GetEnvironmentVariable("KC_UPLOAD_DIR")
                ?? Path.Combine(AppContext.BaseDirectory, "uploads");

            /* =======================================================
             * DEPENDENCY INJECTION (SERVICES)
             * ======================================================= */
            builder.Services.AddScoped<Services.Core.KnowledgeNodeService>();
            builder.Services.AddScoped<Services.Core.DomainService>();
            builder.Services.AddScoped<Services.Core.LogEntryService>();
            builder.Services.AddScoped<Services.Core.TagService>();
            builder.Services.AddScoped<Services.Core.UserService>();
            builder.Services.AddSingleton(new Services.Core.ImageService(uploadDirectory));

            /* =======================================================
             * FORWARDED HEADERS (reverse proxy)
             * Without this, Connection.RemoteIpAddress is always the proxy's
             * own IP, which breaks per-client rate limiting.
             * ======================================================= */
            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                // The reverse proxy isn't on a fixed, known address, so clear the default
                // known-proxy allowlist. Only do this because the app is not directly
                // internet-facing (it always sits behind our proxy) — if that ever
                // changes, restrict KnownProxies/KnownNetworks instead of clearing them.
                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();
            });

            /* =======================================================
             * CORS CONFIGURATION
             * ======================================================= */
            const string KCFrontendCors = "KCFrontendCors";
            builder.Services.AddCors(options =>
            {
                options.AddPolicy(name: KCFrontendCors, policy =>
                {
                    policy.WithOrigins(
                        "https://kc.mortensens.cc",
                        "http://localhost:8081",
                        "http://localhost:3000",
                        "http://localhost:5173"
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

            // Must run before anything that reads the connection/scheme (rate limiting, HTTPS redirection, etc.)
            app.UseForwardedHeaders();

            // Base Path for API (reverse proxy scenario)
            app.UsePathBase("/kc");
            app.Use((context, next) =>
            {
                context.Request.PathBase = "/kc";
                return next();
            });
            app.UseStaticFiles();

            // Serve uploaded images at /kc/uploads/{fileName} directly from disk —
            // keeps the API out of the hot path for repeat image loads, no NFS
            // mount needed since nginx already proxies all of /kc/* to this app.
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(uploadDirectory),
                RequestPath = "/uploads"
            });

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
