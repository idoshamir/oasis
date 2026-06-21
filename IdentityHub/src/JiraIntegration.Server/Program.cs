using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using FluentValidation;
using FluentValidation.AspNetCore;
using JiraIntegration.Server;
using JiraIntegration.Server.Auth;
using JiraIntegration.Server.Configuration;
using JiraIntegration.Server.Data;
using JiraIntegration.Server.Implementations;
using JiraIntegration.Server.Interfaces;
using JiraIntegration.Server.Middleware;
using JiraIntegration.Server.Models.Common;
using JiraIntegration.Server.Pipeline;
using JiraIntegration.Server.Validators;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<AtlassianOptions>(builder.Configuration.GetSection(AtlassianOptions.SectionName));
builder.Services.Configure<AppDataProtectionOptions>(builder.Configuration.GetSection(AppDataProtectionOptions.SectionName));

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddMemoryCache();

var dataProtectionOptions = builder.Configuration
    .GetSection(AppDataProtectionOptions.SectionName)
    .Get<AppDataProtectionOptions>() ?? new AppDataProtectionOptions();
var dataProtectionKeysDirectory = Path.IsPathRooted(dataProtectionOptions.KeysPath)
    ? new DirectoryInfo(dataProtectionOptions.KeysPath)
    : new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, dataProtectionOptions.KeysPath));
Directory.CreateDirectory(dataProtectionKeysDirectory.FullName);

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(dataProtectionKeysDirectory)
    .SetApplicationName("JiraIntegration.Server");

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IJiraConnectionRepository, JiraConnectionRepository>();
builder.Services.AddScoped<IApiKeyRepository, ApiKeyRepository>();
builder.Services.AddScoped<IApiKeyService, ApiKeyService>();
builder.Services.AddScoped<INhiTicketLedgerRepository, NhiTicketLedgerRepository>();
builder.Services.AddScoped<IRevokedTokenRepository, RevokedTokenRepository>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<ITokenEncryptionService, TokenEncryptionService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<ITokenRevocationService, TokenRevocationService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IJiraOAuthService, JiraOAuthService>();
builder.Services.AddScoped<IJiraTicketService, JiraTicketService>();
builder.Services.AddSingleton<IOAuthStateStore, OAuthStateStore>();
builder.Services.AddScoped<ITicketCreationPipeline, TicketCreationPipeline>();
builder.Services.AddScoped<IJiraOAuthPipeline, JiraOAuthPipeline>();
builder.Services.AddScoped<DbSeeder>();
builder.Services.AddHostedService<DatabaseInitializer>();

builder.Services.AddHttpClient("Atlassian");

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Jwt configuration is required.");

if (!jwtOptions.IsConfigured())
{
    throw new InvalidOperationException(
        "Jwt:Secret is not configured or is too short (minimum 32 characters). " +
        "In Development, run: dotnet user-secrets set \"Jwt:Secret\" \"<your-secret>\". " +
        "In production, set the Jwt__Secret environment variable.");
}

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                if (!context.Request.Headers.TryGetValue("Authorization", out var headerValues))
                {
                    return;
                }

                var header = headerValues.ToString();
                const string prefix = "Bearer ";
                if (!header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                var token = header[prefix.Length..].Trim();
                var revocationService = context.HttpContext.RequestServices
                    .GetRequiredService<ITokenRevocationService>();

                if (await revocationService.IsRevokedAsync(token, context.HttpContext.RequestAborted))
                {
                    context.Fail("Token has been revoked.");
                }
            }
        };
    })
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationDefaults.AuthenticationScheme,
        _ => { });

builder.Services.AddAuthorization();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<CreateNhiFindingRequestValidator>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(
            new ErrorResponse("Rate limit exceeded. Please try again later.", "rate_limit_exceeded"),
            token);
    };

    options.AddPolicy("Login", httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            $"login:{ip}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    });

    options.AddPolicy("ExternalApi", httpContext =>
    {
        var apiKey = httpContext.Request.Headers[ApiKeyAuthenticationDefaults.HeaderName].ToString();
        var partitionKey = !string.IsNullOrWhiteSpace(apiKey)
            ? $"key:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(apiKey)))}"
            : $"ip:{httpContext.Connection.RemoteIpAddress}";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    });
});

builder.Services.AddControllers();

var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("UiCors", policy =>
    {
        policy.WithOrigins(corsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    var atlassianOptions = app.Configuration.GetSection(AtlassianOptions.SectionName).Get<AtlassianOptions>();
    if (atlassianOptions is not null && !atlassianOptions.IsConfigured())
    {
        app.Logger.LogWarning(
            "Atlassian OAuth is not configured. Jira connect will be unavailable until secrets are set. See README.md for setup.");
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseExceptionHandler();
app.UseCors("UiCors");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
