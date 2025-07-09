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

            // === Add Controllers and Swagger ===
            builder.Services.AddControllers();
            builder.Services.AddOpenApi();

            var app = builder.Build();

            // === Dev Swagger ===
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            app.UseHttpsRedirection();
            app.UseAuthorization();
            app.MapControllers();

            app.Run();
        }
    }
}
