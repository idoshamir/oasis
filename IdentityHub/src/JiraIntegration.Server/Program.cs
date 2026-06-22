using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using AspNetCore.Authentication.ApiKey;
using FluentValidation;
using FluentValidation.AspNetCore;
using JiraIntegration.Server;
using JiraIntegration.Server.Auth;
using JiraIntegration.Server.Configuration;
using JiraIntegration.Server.Data;
using JiraIntegration.Server.Data.Entities;
using JiraIntegration.Server.Implementations;
using JiraIntegration.Server.Implementations.Atlassian;
using JiraIntegration.Server.Interfaces;
using JiraIntegration.Server.Middleware;
using JiraIntegration.Server.Models.Common;
using JiraIntegration.Server.Pipeline;
using JiraIntegration.Server.Validators;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Validation.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<AtlassianOptions>(builder.Configuration.GetSection(AtlassianOptions.SectionName));
builder.Services.Configure<AppDataProtectionOptions>(builder.Configuration.GetSection(AppDataProtectionOptions.SectionName));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

var dataProtectionOptions = builder.Configuration
    .GetSection(AppDataProtectionOptions.SectionName)
    .Get<AppDataProtectionOptions>() ?? new AppDataProtectionOptions();
var dataProtectionKeysDirectory = Path.IsPathRooted(dataProtectionOptions.KeysPath)
    ? new DirectoryInfo(dataProtectionOptions.KeysPath)
    : new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, dataProtectionOptions.KeysPath));
Directory.CreateDirectory(dataProtectionKeysDirectory.FullName);

var dataProtectionBuilder = builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(dataProtectionKeysDirectory)
    .SetApplicationName("JiraIntegration.Server");

if (OperatingSystem.IsWindows())
{
    dataProtectionBuilder.ProtectKeysWithDpapi();
}

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Jwt configuration is required.");

if (!jwtOptions.IsConfigured())
{
    throw new InvalidOperationException(
        "Jwt:Secret is not configured or is too short (minimum 32 characters). " +
        "In Development, run: dotnet user-secrets set \"Jwt:Secret\" \"<your-secret>\". " +
        "In production, set the Jwt__Secret environment variable.");
}

builder.Services.Configure<OpenIddictClientOptions>(options =>
{
    var configured = builder.Configuration.GetSection(OpenIddictClientOptions.SectionName).Get<OpenIddictClientOptions>()
        ?? new OpenIddictClientOptions();
    options.ClientId = string.IsNullOrWhiteSpace(configured.ClientId)
        ? "jira-integration"
        : configured.ClientId;
    options.ClientSecret = string.IsNullOrWhiteSpace(configured.ClientSecret)
        ? jwtOptions.Secret
        : configured.ClientSecret;
});

var signingKeyMaterial = SHA256.HashData(Encoding.UTF8.GetBytes(jwtOptions.Secret));
var encryptionKey = new SymmetricSecurityKey(signingKeyMaterial);
var tokenIssuer = Uri.TryCreate(jwtOptions.Issuer, UriKind.Absolute, out var issuerUri)
    ? issuerUri
    : new Uri("http://localhost/");

builder.Services
    .AddIdentity<User, IdentityRole<Guid>>(options =>
    {
        options.User.RequireUniqueEmail = false;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 1;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
            .UseDbContext<AppDbContext>();
    })
    .AddServer(options =>
    {
        options.SetTokenEndpointUris("/connect/token");
        options.SetIssuer(tokenIssuer);
        options.AllowPasswordFlow()
            .AllowRefreshTokenFlow();
        options.SetAccessTokenLifetime(TimeSpan.FromMinutes(jwtOptions.ExpiryMinutes));
        options.SetRefreshTokenLifetime(TimeSpan.FromDays(jwtOptions.RefreshTokenExpiryDays));
        options.AddDevelopmentSigningCertificate()
            .AddDevelopmentEncryptionCertificate();
        options.AddEncryptionKey(encryptionKey);
        options.DisableAccessTokenEncryption();
        var aspNetCore = options.UseAspNetCore()
            .EnableTokenEndpointPassthrough();
        if (builder.Environment.IsDevelopment())
        {
            aspNetCore.DisableTransportSecurityRequirement();
        }
    })
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
        options.AddEncryptionKey(encryptionKey);
    });

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IJiraConnectionRepository, JiraConnectionRepository>();
builder.Services.AddScoped<IJiraConnectionValidator, JiraConnectionValidator>();
builder.Services.AddScoped<IApiKeyRepository, ApiKeyRepository>();
builder.Services.AddScoped<IApiKeyService, ApiKeyService>();
builder.Services.AddScoped<INhiTicketLedgerRepository, NhiTicketLedgerRepository>();
builder.Services.AddScoped<IApiKeyHasher, ApiKeyHasher>();
builder.Services.AddScoped<ITokenEncryptionService, TokenEncryptionService>();
builder.Services.AddScoped<IAuthService, OpenIddictAuthService>();
builder.Services.AddScoped<IJiraOAuthService, JiraOAuthService>();
builder.Services.AddScoped<JiraCloudApiClient>();
builder.Services.AddScoped<IJiraTicketService, JiraTicketService>();
builder.Services.AddSingleton<IJiraTokenRefreshService, JiraTokenRefreshService>();
builder.Services.AddSingleton<IOAuthStateStore, OAuthStateProtector>();
builder.Services.AddScoped<ITicketCreationPipeline, TicketCreationPipeline>();
builder.Services.AddScoped<IJiraOAuthPipeline, JiraOAuthPipeline>();
builder.Services.AddScoped<DbSeeder>();
builder.Services.AddScoped<DemoUserSeeder>();
builder.Services.AddScoped<OpenIddictClientBootstrap>();
builder.Services.AddHostedService<DatabaseInitializer>();
builder.Services.AddHostedService<OpenIddictSeeder>();

var atlassianOptions = builder.Configuration.GetSection(AtlassianOptions.SectionName).Get<AtlassianOptions>()
    ?? new AtlassianOptions();
builder.Services.AddHttpClient("Atlassian", client =>
{
    client.Timeout = TimeSpan.FromSeconds(atlassianOptions.HttpTimeoutSeconds);
});
builder.Services.AddHttpClient("AtlassianOAuth", client =>
{
    client.Timeout = TimeSpan.FromSeconds(atlassianOptions.HttpTimeoutSeconds);
});
builder.Services.AddHttpClient("OpenIddictInternal");

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
    })
    .AddApiKeyInHeader<ApiKeyProvider>(ApiKeyAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.KeyName = ApiKeyAuthenticationDefaults.HeaderName;
        options.SuppressWWWAuthenticateHeader = true;
    });

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
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    var devAtlassianOptions = app.Configuration.GetSection(AtlassianOptions.SectionName).Get<AtlassianOptions>();
    if (devAtlassianOptions is not null && !devAtlassianOptions.IsConfigured())
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

if (args.Contains("--seed-demo", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = app.Services.CreateAsyncScope();
    var bootstrap = scope.ServiceProvider.GetRequiredService<OpenIddictClientBootstrap>();
    await bootstrap.EnsureRegisteredAsync();

    var demoSeeder = scope.ServiceProvider.GetRequiredService<DemoUserSeeder>();
    await demoSeeder.SeedAsync();
    return;
}

app.Run();
