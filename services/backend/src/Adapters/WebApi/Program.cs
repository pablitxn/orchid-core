using System.Globalization;
using Application.Interfaces;
using Application.UseCases.Auth.GoogleLogin;
using Application.UseCases.User.CreateUser;
using Application.UseCases.User.GetUser;
using HealthChecks.UI.Client;
using Infrastructure;
using Infrastructure.Ai.SemanticKernel;
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
using Microsoft.Extensions.FileProviders;
using Microsoft.OpenApi.Models;
using WebApi.Hubs;
using WebApi.Middleware;
using Infrastructure.Services;
using WebApi;

var builder = WebApplication.CreateBuilder(args);

#region Logging Configuration

// ─── Logging ──────────────────────────────────────────────────────────────────
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var logger = LoggerFactory.Create(cfg => cfg.AddConsole())
    .CreateLogger("Startup");
logger.LogInformation("Starting application setup…");

#endregion

#region CQRS Configuration

// ─── MediatR (CQRS) ───────────────────────────────────────────────────────────
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(CreateUserCommandHandler).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(GetUserHandler).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(GoogleLoginHandler).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(GetWorkflowByIdHandler).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
});
logger.LogInformation("MediatR handlers registered");


#endregion

#region Infrastructure Services

// ─── Infrastructure (DB, repos, etc.) ─────────────────────────────────────────
await builder.Services.AddInfrastructure(builder.Configuration);
logger.LogInformation("Infrastructure services registered");

// ─── Telemetry Services (including Langfuse for AI operations) ─────────────────
builder.Services.AddTelemetryServices(builder.Configuration);
logger.LogInformation("Telemetry services registered");

// ─── Semantic Kernel ───────────────────────────────────────────────────────────
builder.Services.AddSemanticKernel(builder.Configuration);
builder.Services.AddSemanticKernelPlugins();

// ─── WebApi Adapters (SignalR adapters for notifications) ─────────────────────
builder.Services.AddWebApiAdapters();
logger.LogInformation("WebApi adapters registered");

#endregion

#region Web Framework Configuration

// ─── MVC + SignalR + Minimal APIs ─────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSignalR();
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// ─── Swagger / OpenAPI ────────────────────────────────────────────────────────
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Playground Dotnet API", Version = "v1" });
});
logger.LogInformation("Swagger configured");

// ─── HTTP Context accessor ────────────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();

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
        policy.WithOrigins(builder.Configuration["FrontendUrl"]!)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});
logger.LogInformation("CORS policy added");

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

    // ─── System resources (disk, memory, CPU) ───
    .AddProcessAllocatedMemoryHealthCheck(512 /* MB */, "memory")
    .AddDiskStorageHealthCheck(opt =>
        opt.AddDrive(Path.GetPathRoot(Environment.CurrentDirectory)!, 2048), "disk")

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
var env = builder.Environment;
if (!env.IsEnvironment("Testing"))
{
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
                        (path.StartsWithSegments("/chatHub") || path.StartsWithSegments("/huddleHub")))
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

#region Background Workers

// Register Recycle Bin Cleanup Worker
// builder.Services.AddHostedService<RecycleBinCleanupWorker>();

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

// ─── Global Exception Handler ─────────────────────────────────────────────────
app.UseMiddleware<GlobalExceptionMiddleware>();

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
// Serve uploaded files from 'data' directory at '/files'
var uploadsDir = Path.Combine(app.Environment.ContentRootPath, "data");
Directory.CreateDirectory(uploadsDir);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsDir),
    RequestPath = "/files"
});

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

// ─── Health Check Endpoint ────────────────────────────────────────────────────
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
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

logger.LogInformation("Application configuration complete — running.");
app.Run();

// Make Program accessible to tests
public partial class Program { }