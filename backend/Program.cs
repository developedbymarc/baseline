using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using backend.Configuration;
using backend.Controllers;
using backend.Data;
using backend.Responses;
using backend.Services.Clock;
using backend.Services.Missions;
using backend.Services.Seed;

DotNetEnv.Env.Load();
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(p => p
        .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
        .AllowCredentials()
        .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
        .WithHeaders("Content-Type"));
});

// --- Controllers ---
builder.Services
    .AddControllers(options => options.Filters.Add<ApiExceptionFilter>())
    .AddJsonOptions(options =>
    {
        // Accepts both names and numbers when reading, writes names
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        // Safety net: endpoints return DTOs, but never let an EF navigation cycle crash a response.
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(entry => entry.Value?.Errors.Count > 0)
                .ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value!.Errors.Select(error => error.ErrorMessage).ToArray());

            return new BadRequestObjectResult(ApiResponse.Failed(
                code: "VALIDATION_FAILURE",
                message: "One or more validation errors occurred.",
                details: errors));
        };
    });

// --- Health config (YAML under config/, loaded once, validated at start) ---
builder.Services.AddSingleton(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var env = sp.GetRequiredService<IWebHostEnvironment>();
    var dir = cfg["Health:ConfigDirectory"] ?? "../config";
    return HealthConfig.LoadFromDirectory(Path.GetFullPath(dir, env.ContentRootPath));
});

// --- Database (SQLite, single file, no setup) ---
builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
    options.UseSqlite(sp.GetRequiredService<IConfiguration>().GetConnectionString("Default") ?? "Data Source=baseline.db"));

// --- Services ---
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<MissionClockService>();
builder.Services.AddScoped<MissionService>();
builder.Services.AddScoped<DemoSeeder>();

// API docs
builder.Services.AddOpenApi();

var app = builder.Build();

// Fail fast on bad YAML, log content warnings (never fatal), then make sure the demo mission exists.
// `dotnet run -- reseed` wipes the SQLite file and reloads the synthetic mission.
var healthConfig = app.Services.GetRequiredService<HealthConfig>();
var configLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("HealthConfig");
configLogger.LogInformation("Loaded metrics.yaml v{Version}, {Metrics} metrics, protocols: {Protocols}",
    healthConfig.MetricsVersion, healthConfig.Metrics.Count,
    string.Join(", ", healthConfig.Protocols.Values.Select(p => $"{p.Id} v{p.Version}")));
foreach (var warning in healthConfig.Warnings)
    configLogger.LogWarning("Config warning: {Warning}", warning);
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var seeder = scope.ServiceProvider.GetRequiredService<DemoSeeder>();
    if (args.Contains("reseed"))
        await seeder.ResetAndSeedAsync();
    else if (await db.Database.EnsureCreatedAsync() || !await db.Missions.AnyAsync())
        await seeder.SeedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseCors();
app.MapControllers();

app.Run();

public partial class Program;
