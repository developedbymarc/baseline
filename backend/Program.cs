using System.Text;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using backend.Responses;
using backend.Entities;
using backend.Data;
using backend.Constants;
using backend.Services.ApplicationServices.TokenService;

DotNetEnv.Env.Load();
var builder = WebApplication.CreateBuilder(args);

// Add extra configuration providers (vaults, etc.) here, BEFORE Build()

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(p => p
        .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()!)
        .AllowCredentials()
        .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
        .WithHeaders("Content-Type"));
});

// --- Controllers ---
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        // Accepts both names and numbers when reading (e.g. "Instructor" / 0), writes names
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
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

// --- Database ---
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    // sqlite
});

// --- Identity ---
builder.Services
    .AddIdentityCore<CrewMember>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.User.AllowedUserNameCharacters = Characters.Lowercase + Characters.Uppercase + Characters.Digits + "-._";

        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireDigit = true;
        options.Password.RequiredUniqueChars = 1;

        options.SignIn.RequireConfirmedEmail = true;

        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(30);
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
{
    options.TokenLifespan = TimeSpan.FromHours(1);
});

// --- JWT authentication ---
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["AccessToken:Issuer"]!,
        ValidAudience = builder.Configuration["AccessToken:Audience"]!,
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["AccessToken:SigningKey"]!)
        )
    };

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessTokenCookieName = builder.Configuration["Cookie:AccessToken"]!;
            if (context.Request.Cookies.ContainsKey(accessTokenCookieName))
            {
                context.Token = context.Request.Cookies[accessTokenCookieName];
            }

            return Task.CompletedTask;
        }
    };
});


// Authz and policies
builder.Services.AddAuthorization();

// API docs
builder.Services.AddOpenApi();

// My Services
builder.Services.AddScoped<AbstractRefreshTokenService, RefreshTokenService>();
builder.Services.AddSingleton<AbstractAccessTokenService, AccessTokenService>();

var app = builder.Build();

if (args is ["migrate"])
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
        .Database.MigrateAsync();
    return;
}

app.Services.GetRequiredService<IStartupValidator>().Validate();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    // seeding roles
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

    var startupLogger = services.GetRequiredService<ILogger<Program>>();
    foreach (var roleName in RoleNames.All)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            var result = await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
            if (!result.Succeeded)
                startupLogger.LogCritical("Failed to seed role {Role}: {Errors}", roleName,
                    string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
