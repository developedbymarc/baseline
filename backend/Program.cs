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
    options.UseSqlite()
});

// --- Identity ---
builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
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
    .AddRoles<ApplicationRole>()
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

builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
    {
        var jwtOptions = jwt.Value;

        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,

            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),

            ValidAlgorithms = [jwtOptions.Algorithm],
            ValidateLifetime = true,
            ClockSkew = jwtOptions.ClockSkew,
        };

        bearer.Events = new JwtBearerEvents
        {
            // A null token falls through to the Authorization header
            OnMessageReceived = ctx =>
            {
                ctx.Token = ctx.Request.Cookies[jwtOptions.CookieName];
                return Task.CompletedTask;
            },
            // Reject disabled accounts
            OnTokenValidated = async ctx =>
            {
                var userIdValue = ctx.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                  ?? ctx.Principal?.FindFirst("sub")?.Value;
                if (!Guid.TryParse(userIdValue, out var userId))
                {
                    ctx.Fail("The token does not identify a valid user.");
                    return;
                }

                var dbContext = ctx.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
                var now = DateTimeOffset.UtcNow;

                var accountIsEnabled = await dbContext.Users
                    .AsNoTracking()
                    .AnyAsync(user =>
                        user.Id == userId
                        && user.Status != AccountStatus.Disabled
                        && (!user.LockoutEnabled
                            || user.LockoutEnd == null
                            || user.LockoutEnd <= now),
                    ctx.HttpContext.RequestAborted);

                if (!accountIsEnabled)
                {
                    ctx.Fail("The account is disabled or locked.");
                }
            },
        };
    });

// Authz and policies
builder.Services.AddAuthorization();
builder.Services.AddCourseAccessPolicies();

// API docs
builder.Services.AddOpenApi();

// My Services
builder.Services.AddScoped<RootUserBootstrapper>();
builder.Services.AddScoped<AbstractRefreshTokenService, RefreshTokenService>();
builder.Services.AddSingleton<AbstractAccessTokenService, AccessTokenService>();
builder.Services.AddSingleton<EmailTemplateStore>();

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
    var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();

    var startupLogger = services.GetRequiredService<ILogger<Program>>();
    foreach (var roleName in RoleNames.All)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            var result = await roleManager.CreateAsync(new ApplicationRole(roleName));
            if (!result.Succeeded)
                startupLogger.LogCritical("Failed to seed role {Role}: {Errors}", roleName,
                    string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }

    // bootstrapping root
    if (args.Contains("bootstrap-root"))
    {
        Environment.ExitCode = await BootstrapRootCommand.RunAsync(services);
        return;   // exits without starting the web server
    }

    var bootstrapper = services.GetRequiredService<RootUserBootstrapper>();
    if (!await bootstrapper.RootExistsAsync())
    {
        startupLogger.LogWarning(
            "No root user exists. Create one with: podman compose run --rm api bootstrap-root");
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
