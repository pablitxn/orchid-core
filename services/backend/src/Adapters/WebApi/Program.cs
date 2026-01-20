using System.Globalization;
using Application.Interfaces;
using Application.UseCases.User.CreateUser;
using Application.UseCases.User.GetUser;
using HealthChecks.UI.Client;
using Infrastructure;
using Infrastructure.Ai.AgentFramework;
using Infrastructure.Messaging;
using Infrastructure.Persistence;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Application.UseCases.Workflow.GetWorkflowById;
using Infrastructure.Telemetry;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using FluentValidation;
using FluentValidation.AspNetCore;
using WebApi.Hubs;
using WebApi.Middleware;
using Infrastructure.Services;
using WebApi;
using WebApi.Services;
using Serilog;
using Serilog.Events;
using Infrastructure.Messaging.RabbitMq;
using Sentry.Profiling;

var builder = WebApplication.CreateBuilder(args);
var env = builder.Environment;

// ─── Load optional local settings (overrides for local development) ──────────
builder.Configuration.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);

#region Sentry Configuration

// ─── Sentry Error Tracking & Performance Monitoring ──────────────────────────
// Sentry is configured via appsettings.json (Sentry section)
// DSN should be set via environment variable SENTRY__DSN or in configuration
var sentryDsn = builder.Configuration["Sentry:Dsn"];
if (!string.IsNullOrEmpty(sentryDsn))
{
    // Read debug setting from config, defaulting to false to avoid log spam
    var sentryDebug = builder.Configuration.GetValue<bool>("Sentry:Debug", false);

    builder.Logging.AddSentry(o =>
    {
        o.Dsn = sentryDsn;
        o.Environment = env.EnvironmentName;
        o.Debug = sentryDebug;
        o.AutoSessionTracking = true;
        o.TracesSampleRate = env.IsDevelopment() ? 1.0 : 0.1;
        o.ProfilesSampleRate = env.IsDevelopment() ? 1.0 : 0.1;
        o.SendDefaultPii = env.IsDevelopment();
        o.AttachStacktrace = true;
        o.AddIntegration(new ProfilingIntegration(TimeSpan.FromMilliseconds(500)));
    });
}

#endregion

#region Logging Configuration

// ─── Serilog with Correlation ID ──────────────────────────────────────────────
// Configure Serilog with environment-appropriate log levels
// Framework noise is suppressed to Warning level to improve log readability
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    // Microsoft framework logging - reduce noise
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore.SignalR", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore.Http.Connections", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
    // System and third-party logging
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
    .MinimumLevel.Override("MassTransit", LogEventLevel.Warning)
    // Enrich with correlation ID and context
    .Enrich.FromLogContext()
    .Enrich.WithCorrelationId()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

Log.Information("Starting application setup...");

#endregion

#region CQRS Configuration

// ─── MediatR (CQRS) ───────────────────────────────────────────────────────────
builder.Services.AddMediatR(cfg =>
{
    var licenseKey = builder.Configuration["MediatR:LicenseKey"];
    if (!string.IsNullOrEmpty(licenseKey))
    {
        cfg.LicenseKey = licenseKey;
    }

    cfg.RegisterServicesFromAssembly(typeof(CreateUserCommandHandler).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(GetUserHandler).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(GetWorkflowByIdHandler).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
});
Log.Information("MediatR handlers registered");


#endregion

#region Infrastructure Services

// ─── Infrastructure (DB, repos, etc.) ─────────────────────────────────────────
await builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
Log.Information("Infrastructure services registered");

// ─── Telemetry Services (including Langfuse for AI operations) ─────────────────
builder.Services.AddTelemetryServices(builder.Configuration);
Log.Information("Telemetry services registered");

// ─── Agent Framework (AI) ──────────────────────────────────────────────────────
builder.Services.AddAgentFramework(builder.Configuration);
builder.Services.AddAgentFrameworkTools();

// ─── WebApi Adapters (SignalR adapters for notifications) ─────────────────────
builder.Services.AddWebApiAdapters();
Log.Information("WebApi adapters registered");

#endregion

#region Web Framework Configuration

// ─── MVC + SignalR + Minimal APIs ─────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSignalR();
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// ─── Swagger / OpenAPI ────────────────────────────────────────────────────────
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Playground Dotnet API", Version = "v1" });
});
Log.Information("Swagger configured");

// ─── HTTP Context accessor ────────────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();

// ─── Correlation ID accessor for distributed tracing ─────────────────────────
builder.Services.AddScoped<ICorrelationIdAccessor, CorrelationIdAccessor>();

#endregion

#region Authentication & Security

// ─── Cookie settings (shared by any auth scheme that uses cookies) ────────────
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
    options.SlidingExpiration = true;
});

#endregion

#region Messaging Configuration

// ─── MassTransit (RabbitMQ) ───────────────────────────────────────────────────
builder.Services.AddMassTransit(x =>
{
    // Register consumers
    x.AddConsumer<Infrastructure.Messaging.Consumers.CreditNotificationHandler>();

    x.UsingRabbitMq((context, cfg) =>
    {
        var host = builder.Configuration["RabbitMq:Host"] ?? "localhost";
        var username = builder.Configuration["RabbitMq:Username"] ?? "guest";
        var password = builder.Configuration["RabbitMq:Password"] ?? "guest";

        cfg.Host(host, "/", h =>
        {
            h.Username(username);
            h.Password(password);
        });

        // Add correlation ID filters for distributed tracing
        cfg.UseCorrelationIdFilters(context);

        // Configure endpoints
        cfg.ConfigureEndpoints(context);
    });
});
builder.Services.AddScoped<IEventPublisher,
    MassTransitEventPublisher>();

#endregion

#region CORS Configuration

// ─── CORS (frontend) ──────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("MyPolicy", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
        if (allowedOrigins == null || allowedOrigins.Length == 0)
        {
            var frontendUrl = builder.Configuration["FrontendUrl"];
            if (!string.IsNullOrWhiteSpace(frontendUrl))
            {
                allowedOrigins = [frontendUrl];
            }
        }

        if (!env.IsDevelopment())
        {
            if (allowedOrigins == null || allowedOrigins.Length == 0)
            {
                throw new InvalidOperationException(
                    "CORS allowed origins must be configured for non-development environments.");
            }

            if (allowedOrigins.Any(origin => origin.Contains('*')))
            {
                throw new InvalidOperationException(
                    "CORS allowed origins cannot include wildcards in non-development environments.");
            }
        }

        if (allowedOrigins != null && allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
        }
    });
});
Log.Information("CORS policy added");

#endregion

#region Health Checks

// ─── Health checks ────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()

    // ─── Databases ───────────────────────────────
    .AddNpgSql(
        builder.Configuration.GetConnectionString("DefaultConnection")!,
        name: "postgres",
        tags: ["db", "postgres"])
    .AddRedis(
        builder.Configuration["Redis:Configuration"] ?? "localhost:6379",
        "redis",
        tags: ["db", "redis", "cache"])

    // ─── Message Bus ─────────────────────────────
    // .AddRabbitMQ(
    //     builder.Configuration["RabbitMq:Connection"]
    //     ?? $"amqp://{builder.Configuration["RabbitMq:Username"]}:{builder.Configuration["RabbitMq:Password"]}@{builder.Configuration["RabbitMq:Host"]}:5672",
    //     name: "rabbitmq",
    //     tags: ["queue", "rabbit"])
    // .AddUrlGroup(new Uri("https://api.openai.com/v1/models"),      name: "openai")
    // .AddUrlGroup(new Uri(builder.Configuration["Langfuse:BaseUrl"] ?? "https://api.langfuse.com/health"), name: "langfuse")

    // ─── System resources (memory) ───
    .AddProcessAllocatedMemoryHealthCheck(512 /* MB */, "memory")

    // ─── Self probe ─────────────────────────────
    .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"])

    // ─── Telemetry ───────────────────────────────
    .AddLangfuseHealthCheck(builder.Configuration);

#endregion

#region Caching Configuration

// ─── Redis distributed cache for chat history ─────────────────────────────────
builder.Services.AddStackExchangeRedisCache(options =>
{
    // Use configuration or default to localhost
    options.Configuration = builder.Configuration.GetValue<string>("Redis:Configuration") ?? "localhost:6379";
    options.InstanceName = builder.Configuration.GetValue<string>("Redis:InstanceName") ?? "ChatHistory:";
});

#endregion

#region Environment-Specific Services

// ─── Authentication (test-double vs. JWT Bearer) ────────────────────────
if (!env.IsEnvironment("Testing"))
{
    // Security: Validate JWT key configuration
    var jwtKey = builder.Configuration["Jwt:Key"];
    var insecureKeys = new[]
    {
        "CHANGE_THIS_IN_PRODUCTION",
        "dev_only_secret_key",
        "test_only_secret_key"
    };

    if (string.IsNullOrEmpty(jwtKey))
    {
        throw new InvalidOperationException(
            "JWT Key is not configured. Set 'Jwt:Key' via environment variable 'Jwt__Key' or configuration.");
    }

    if (jwtKey.Length < 32)
    {
        throw new InvalidOperationException(
            "JWT Key must be at least 32 characters (256 bits) for secure signing.");
    }

    if (!env.IsDevelopment() && insecureKeys.Any(k => jwtKey.Contains(k, StringComparison.OrdinalIgnoreCase)))
    {
        throw new InvalidOperationException(
            "Insecure JWT Key detected in non-development environment. " +
            "Please configure a secure key via environment variable 'Jwt__Key'.");
    }

    // Security: Validate AllowedHosts in production
    var allowedHosts = builder.Configuration["AllowedHosts"];
    if (!env.IsDevelopment() && (string.IsNullOrEmpty(allowedHosts) || allowedHosts == "*"))
    {
        throw new InvalidOperationException(
            "AllowedHosts must be explicitly configured in production. " +
            "Set 'AllowedHosts' to your domain(s), e.g., 'example.com;api.example.com'.");
    }

    // Security: Validate Entity Framework logging configuration in production
    var efLogLevel = builder.Configuration["Logging:LogLevel:Microsoft.EntityFrameworkCore.Database.Command"];
    if (!env.IsDevelopment() && efLogLevel != null &&
        (efLogLevel.Equals("Debug", StringComparison.OrdinalIgnoreCase) ||
         efLogLevel.Equals("Trace", StringComparison.OrdinalIgnoreCase)))
    {
        throw new InvalidOperationException(
            "Entity Framework command logging is set to detailed level in non-development environment. " +
            "This can expose sensitive SQL parameters. Set 'Logging:LogLevel:Microsoft.EntityFrameworkCore.Database.Command' to 'Warning' or higher.");
    }

    // JWT Bearer authentication for Development and Production
    builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = !env.IsDevelopment();
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = builder.Configuration["Jwt:Audience"],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
            };

            // Configure JWT Bearer Authentication for SignalR
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];

                    // If the request is for our hub...
                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(accessToken) &&
                        (path.StartsWithSegments("/chatHub") ||
                         path.StartsWithSegments("/huddleHub") ||
                         path.StartsWithSegments("/hubs/credits")))
                    {
                        // Read the token out of the query string
                        context.Token = accessToken;
                    }

                    return Task.CompletedTask;
                }
            };
        });
}

#endregion

#region AI & Activity Services

// Register Activity publisher for broadcasting backend activities
builder.Services.AddSingleton<IActivityPublisher, ActivityPublisher>();

#endregion

#region Configuration Options

// Register CreditSystem configuration
builder.Services.Configure<WebApi.Configuration.CreditSystemConfiguration>(
    builder.Configuration.GetSection("CreditSystem"));

#endregion

// ─── Build the pipeline ───────────────────────────────────────────────────────
var app = builder.Build();

#region Development-specific Middleware

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

#endregion

#region Request Pipeline Configuration

// ─── Correlation ID (must be first to capture all logs) ───────────────────────
app.UseCorrelationId();

// ─── Sentry Performance Monitoring ────────────────────────────────────────────
app.UseSentryTracing();

// ─── Global Exception Handler ─────────────────────────────────────────────────
app.UseMiddleware<GlobalExceptionMiddleware>();

// ─── Security Headers ─────────────────────────────────────────────────────────
app.Use(async (context, next) =>
{
    // Prevent clickjacking attacks
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    // Prevent MIME type sniffing
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    // XSS protection for older browsers
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    // Control referrer information
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    // Permissions policy
    context.Response.Headers.Append("Permissions-Policy", "geolocation=(), microphone=(), camera=()");
    // Content Security Policy (adjust as needed for your frontend)
    if (!context.Request.Path.StartsWithSegments("/swagger"))
    {
        context.Response.Headers.Append("Content-Security-Policy",
            "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: https:; font-src 'self'; connect-src 'self' wss: https:; frame-ancestors 'none';");
    }
    await next();
});

app.UseRouting();

// ─── Localization ─────────────────────────────────────────────────────────────
var supportedCultures = new[] { "en", "es", "fr", "ru", "ja" };
var localizationOptions = new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("en"),
    SupportedCultures = supportedCultures.Select(c => new CultureInfo(c)).ToList(),
    SupportedUICultures = supportedCultures.Select(c => new CultureInfo(c)).ToList()
};
app.UseRequestLocalization(localizationOptions);

// ─── Static Files ─────────────────────────────────────────────────────────────
// Note: File serving is handled via IFileStorageService (GCP/Memory in production)
// Local filesystem serving removed for cloud deployment compatibility

// ─── CORS, Auth & Custom Middleware ───────────────────────────────────────────
app.UseCors("MyPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<AuthorizationMiddleware>();

#endregion

#region Endpoint Mapping

// ─── Route Mapping ────────────────────────────────────────────────────────────
app.MapControllers();
app.MapHub<ChatHub>("/chatHub");
app.MapHub<NotificationHub>("/notificationHub");
app.MapHub<CreditHub>("/hubs/credits");
// app.MapHub<HuddleHub>("/huddleHub").RequireCors("MyPolicy");

// ─── Health Check Endpoints ───────────────────────────────────────────────────
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});

// ─── Sentry Test Endpoint ─────────────────────────────────────────────────────
// TODO: Remove before production - temporary endpoint for Sentry verification
app.MapGet("/sentry-test", () =>
{
    throw new InvalidOperationException("Sentry test exception - this is intentional!");
});

#endregion

#region Database Initialization

if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
    var fileStorage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
    await DatabaseSeeder.SeedAsync(db, hasher, fileStorage, scope.ServiceProvider);
}

#endregion

Log.Information("Application configuration complete - running.");

try
{
    app.Run();
}
finally
{
    Log.CloseAndFlush();
}

// Make Program accessible to tests
public partial class Program { }
