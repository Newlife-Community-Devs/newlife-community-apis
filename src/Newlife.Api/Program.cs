using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Newlife.Api.Filters;
using Newlife.Api.Logging;
using Newlife.Api.Middleware;
using Newlife.Api.OpenApi;
using Newlife.Application;
using Newlife.Infrastructure;
using Newlife.Infrastructure.Persistence;
using Newlife.Infrastructure.Persistence.Seed;
using Serilog;
using Serilog.Formatting.Display;
using Serilog.Formatting.Json;

var builder = WebApplication.CreateBuilder(args);

// WebApplicationFactory hosts (integration tests) manage their own configuration;
// never let a developer-local .env.local leak into them.
var runningInIntegrationTests =
    Environment.GetEnvironmentVariable("ASPNETCORE_TEST_CONTEXT") is not null ||
    System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name == "testhost";

if (!runningInIntegrationTests)
{
    LoadLocalDotEnv(builder);

    // Neon (and most managed Postgres providers) hand out DATABASE_URL; prefer it over the
    // appsettings default unless ConnectionStrings__Default is set explicitly (e.g. on Render).
    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ConnectionStrings__Default")))
    {
        var databaseUrl = builder.Configuration["DATABASE_URL"];
        if (!string.IsNullOrWhiteSpace(databaseUrl))
        {
            builder.Configuration["ConnectionStrings:Default"] = databaseUrl;
        }
    }
}

var configuredConnectionString = builder.Configuration.GetConnectionString("Default");
if (!string.IsNullOrWhiteSpace(configuredConnectionString))
{
    builder.Configuration["ConnectionStrings:Default"] =
        Newlife.Infrastructure.Persistence.PostgresConnectionString.Normalize(configuredConnectionString);
}

static void LoadLocalDotEnv(WebApplicationBuilder builder)
{
    if (!builder.Environment.IsDevelopment())
    {
        return;
    }

    string? envFilePath = null;
    var directory = new DirectoryInfo(builder.Environment.ContentRootPath);

    for (var depth = 0; depth < 4 && directory is not null; depth++, directory = directory.Parent)
    {
        var candidate = Path.Combine(directory.FullName, ".env.local");

        if (File.Exists(candidate))
        {
            envFilePath = candidate;
            break;
        }
    }

    if (envFilePath is null)
    {
        return;
    }

    var values = new Dictionary<string, string?>();

    foreach (var line in File.ReadAllLines(envFilePath))
    {
        var separator = line.IndexOf('=');

        if (separator <= 0 || line.TrimStart().StartsWith('#'))
        {
            continue;
        }

        values[line[..separator].Trim()] = line[(separator + 1)..].Trim().Trim('"');
    }

    builder.Configuration.AddInMemoryCollection(values);
}

if (builder.Environment.IsProduction())
{
    var hubtelSection = builder.Configuration.GetSection("Hubtel");

    if (!hubtelSection.GetValue("EnforceCallbackIp", true))
    {
        throw new InvalidOperationException(
            "Hubtel:EnforceCallbackIp must be true in Production unless disabling it is an explicit, logged decision.");
    }

    if (string.IsNullOrWhiteSpace(hubtelSection["CallbackToken"]))
    {
        throw new InvalidOperationException("Hubtel:CallbackToken is required in Production.");
    }
}

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.With<RedactSecretsEnricher>()
    .WriteTo.Console(context.HostingEnvironment.IsDevelopment()
        ? new MessageTemplateTextFormatter("[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
        : new JsonFormatter()));

var reverseProxyEnabled = builder.Configuration.GetValue<bool>("ReverseProxy:Enabled");
if (reverseProxyEnabled)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtSigningKey = jwtSection["SigningKey"] ?? string.Empty;
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "name",
            RoleClaimType = "role"
        };
    });
builder.Services.AddAuthorization();

builder.Services
    .AddControllers(options => options.Filters.Add<ValidationFilter>())
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions["traceId"] =
            System.Diagnostics.Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
var allowVercelPreviews = builder.Configuration.GetValue<bool>("Cors:AllowVercelPreviews");
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .SetIsOriginAllowed(origin => IsAllowedOrigin(origin, allowedOrigins, allowVercelPreviews))
    .AllowAnyHeader()
    .AllowAnyMethod()));

var rateLimitingEnabled = builder.Configuration.GetValue("RateLimits:Enabled", true);
if (rateLimitingEnabled)
{
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            RateLimitPartition.GetFixedWindowLimiter(ClientKey(context), _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

        AddIpPolicy(options, "auth", permitLimit: 5, window: TimeSpan.FromMinutes(1));
        AddIpPolicy(options, "prayer-submit", permitLimit: 5, window: TimeSpan.FromMinutes(10));
        AddIpPolicy(options, "orders", permitLimit: 10, window: TimeSpan.FromMinutes(1));
        AddIpPolicy(options, "ping", permitLimit: 3, window: TimeSpan.FromMinutes(1));
    });
}

builder.Services.AddOpenApi("v1", options =>
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

var connectionString = builder.Configuration.GetConnectionString("Default") ?? string.Empty;
builder.Services.AddHealthChecks().AddNpgSql(connectionString, name: "database");

var app = builder.Build();

if (reverseProxyEnabled)
{
    app.UseForwardedHeaders();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseStatusCodePages();
app.UseSerilogRequestLogging();
app.UseCors();
app.UseRouting();
app.UseStaticFiles();
if (rateLimitingEnabled)
{
    app.UseRateLimiter();
}
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Newlife API v1");
        options.RoutePrefix = "swagger";
    });
}

app.MapControllers();
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = WriteHealthResponse
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    ResponseWriter = WriteHealthResponse
});

try
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (app.Configuration.GetValue<bool>("Database:RunMigrationsOnStartup"))
    {
        await dbContext.Database.MigrateAsync();
    }

    await DbSeeder.SeedAsync(scope.ServiceProvider, app.Configuration);
}
catch (Exception exception) when (app.Environment.IsDevelopment())
{
    app.Logger.LogWarning(exception, "Database initialization skipped because the database is unavailable.");
}

app.Run();

static string ClientKey(HttpContext context)
    => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

static void AddIpPolicy(RateLimiterOptions options, string name, int permitLimit, TimeSpan window)
    => options.AddPolicy(name, context =>
        RateLimitPartition.GetFixedWindowLimiter(ClientKey(context), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = window,
            QueueLimit = 0,
            AutoReplenishment = true
        }));

static bool IsAllowedOrigin(string origin, string[] allowedOrigins, bool allowVercelPreviews)
{
    if (allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
    {
        return true;
    }

    return allowVercelPreviews &&
           Uri.TryCreate(origin, UriKind.Absolute, out var uri) &&
           uri.Scheme == Uri.UriSchemeHttps &&
           (uri.Host.Equals("vercel.app", StringComparison.OrdinalIgnoreCase) ||
            uri.Host.EndsWith(".vercel.app", StringComparison.OrdinalIgnoreCase));
}

static Task WriteHealthResponse(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";

    var payload = new
    {
        status = report.Status == HealthStatus.Healthy ? "healthy" : "unhealthy",
        checks = report.Entries.ToDictionary(
            entry => entry.Key,
            entry => entry.Value.Status.ToString().ToLowerInvariant())
    };

    return context.Response.WriteAsync(JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
}

public partial class Program { }
