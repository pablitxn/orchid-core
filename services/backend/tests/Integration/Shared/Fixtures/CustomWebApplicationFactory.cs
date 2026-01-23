using System.Security.Claims;
using System.Text.Encodings.Web;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Integration.Shared.Fixtures;

/// <summary>
/// Custom WebApplicationFactory for integration tests.
/// This factory configures all necessary test services and should be shared
/// across all integration tests via ICollectionFixture.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public string DatabaseName { get; } = $"TestDb_{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Override Redis configuration to prevent connection attempts
        builder.UseSetting("Redis:Configuration", "");

        // Override RabbitMQ configuration to prevent connection attempts
        builder.UseSetting("RabbitMQ:Host", "");
        builder.UseSetting("MassTransit:RabbitMq:Host", "");

        // Disable Sentry for testing
        builder.UseSetting("Sentry:Dsn", "");
        builder.UseSetting("Sentry:Enabled", "false");

        builder.ConfigureTestServices(services =>
        {
            // Remove all DbContext related services
            var dbContextServices = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                d.ServiceType == typeof(ApplicationDbContext) ||
                d.ServiceType.Name.Contains("DbContext")).ToList();

            foreach (var descriptor in dbContextServices)
            {
                services.Remove(descriptor);
            }

            // Add factory for creating DbContext with InMemory provider
            var dbContextOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: DatabaseName)
                .ConfigureWarnings(w =>
                {
                    w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning);
                    w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning);
                })
                .Options;

            services.AddSingleton(dbContextOptions);

            services.AddScoped<ApplicationDbContext>(provider =>
                new ApplicationDbContext(provider.GetRequiredService<DbContextOptions<ApplicationDbContext>>()));

            // Configure test authentication
            services.AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });

            services.AddAuthorization();

            // Add CreditSystem configuration for testing
            services.Configure<WebApi.Configuration.CreditSystemConfiguration>(
                options =>
                {
                    options.TokensPerCredit = 100;
                    options.MinimumCreditsPerMessage = 5;
                });

            // Replace Redis cache with in-memory test cache
            var cacheDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ICacheStore));
            if (cacheDescriptor != null)
            {
                services.Remove(cacheDescriptor);
            }
            services.AddSingleton<ICacheStore, TestCacheStore>();

            // Replace VectorStore with test implementation
            var vectorStoreDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IVectorStorePort));
            if (vectorStoreDescriptor != null)
            {
                services.Remove(vectorStoreDescriptor);
            }
            services.AddSingleton<IVectorStorePort, TestVectorStore>();

            // Remove MassTransit bus control to prevent RabbitMQ connection attempts
            var busDescriptors = services.Where(d =>
                d.ServiceType.FullName?.Contains("MassTransit") == true ||
                d.ServiceType.FullName?.Contains("IBus") == true ||
                d.ImplementationType?.FullName?.Contains("MassTransit") == true).ToList();
            foreach (var descriptor in busDescriptors)
            {
                services.Remove(descriptor);
            }

            // Add Sentry services with disabled hub to prevent SentryTracingMiddleware errors
            var sentryDescriptors = services.Where(d =>
                d.ServiceType == typeof(Sentry.IHub) ||
                d.ServiceType == typeof(Func<Sentry.IHub>)).ToList();
            foreach (var descriptor in sentryDescriptors)
            {
                services.Remove(descriptor);
            }
            var mockHub = new Moq.Mock<Sentry.IHub>();
            mockHub.Setup(h => h.IsEnabled).Returns(false);
            services.AddSingleton<Sentry.IHub>(mockHub.Object);
            services.AddSingleton<Func<Sentry.IHub>>(() => mockHub.Object);

            // Add mock event publisher
            var eventPublisherDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IEventPublisher));
            if (eventPublisherDescriptor != null)
            {
                services.Remove(eventPublisherDescriptor);
            }
            services.AddSingleton<IEventPublisher, TestEventPublisher>();

            // Replace rate limit service with permissive test implementation
            var rateLimitDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IRateLimitService));
            if (rateLimitDescriptor != null)
            {
                services.Remove(rateLimitDescriptor);
            }
            services.AddSingleton<IRateLimitService, TestRateLimitService>();

            // Seed subscription plans at startup
            services.AddHostedService<TestDataSeeder>();

            // Ensure SignalR is properly configured with detailed errors for testing
            services.AddSignalR(options =>
            {
                options.EnableDetailedErrors = true;
            });

            // Replace health checks with mock versions that always pass
            // Remove all existing health check registrations
            var healthCheckDescriptors = services.Where(d =>
                d.ServiceType == typeof(HealthCheckService) ||
                d.ServiceType.FullName?.Contains("HealthCheck") == true).ToList();
            foreach (var descriptor in healthCheckDescriptors)
            {
                services.Remove(descriptor);
            }

            // Add mock health checks that always return Healthy
            services.AddHealthChecks()
                .AddCheck("postgres", () => HealthCheckResult.Healthy("Test database"))
                .AddCheck("redis", () => HealthCheckResult.Healthy("Test cache"))
                .AddCheck("memory", () => HealthCheckResult.Healthy("Memory OK"))
                .AddCheck("seeding", () => HealthCheckResult.Healthy("Seeding complete"))
                .AddCheck("self", () => HealthCheckResult.Healthy("Self check OK"))
                .AddCheck("langfuse", () => HealthCheckResult.Healthy("Telemetry OK"));
        });
    }

    /// <summary>
    /// Creates a new DbContext instance for test setup/assertions.
    /// </summary>
    public ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: DatabaseName)
            .ConfigureWarnings(w =>
            {
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning);
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning);
            })
            .Options;

        var context = new ApplicationDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}

/// <summary>
/// Test authentication handler that extracts user ID from Bearer token.
/// </summary>
#pragma warning disable CS0618 // ISystemClock is obsolete
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock)
        : base(options, logger, encoder, clock)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authHeader = Request.Headers["Authorization"].ToString();

        // If no Authorization header, use the default test user ID
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            // Use GuidTestAuthHandler.UserId as default for tests without token
            var defaultClaims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, GuidTestAuthHandler.UserId.ToString()),
                new Claim(ClaimTypes.Name, "TestUser"),
                new Claim(ClaimTypes.Email, "test@example.com")
            };

            var defaultIdentity = new ClaimsIdentity(defaultClaims, "Test");
            var defaultPrincipal = new ClaimsPrincipal(defaultIdentity);
            var defaultTicket = new AuthenticationTicket(defaultPrincipal, "Test");

            return Task.FromResult(AuthenticateResult.Success(defaultTicket));
        }

        var token = authHeader.Substring("Bearer ".Length).Trim();

        try
        {
            var userIdBytes = Convert.FromBase64String(token);
            var userId = System.Text.Encoding.UTF8.GetString(userIdBytes);

            if (Guid.TryParse(userId, out var parsedUserId))
            {
                var claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, parsedUserId.ToString()),
                    new Claim(ClaimTypes.Name, $"TestUser_{parsedUserId}")
                };

                var identity = new ClaimsIdentity(claims, "Test");
                var principal = new ClaimsPrincipal(identity);
                var ticket = new AuthenticationTicket(principal, "Test");

                return Task.FromResult(AuthenticateResult.Success(ticket));
            }
        }
        catch
        {
            // Invalid token format
        }

        return Task.FromResult(AuthenticateResult.Fail("Invalid token"));
    }
}
#pragma warning restore CS0618

/// <summary>
/// Test vector store implementation that stores chunks in memory.
/// </summary>
public class TestVectorStore : IVectorStorePort
{
    private readonly List<VectorChunk> _chunks = new();

    public Task UpsertChunkAsync(VectorChunk chunk, CancellationToken ct = default)
    {
        var existing = _chunks.FindIndex(c => c.ChunkId == chunk.ChunkId);
        if (existing >= 0)
        {
            _chunks[existing] = chunk;
        }
        else
        {
            _chunks.Add(chunk);
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SearchHit>> SearchAsync(
        string query,
        int k = 8,
        CancellationToken ct = default)
    {
        var results = _chunks
            .Take(k)
            .Select(c => new SearchHit(c, 0.0))
            .ToList();
        return Task.FromResult<IReadOnlyList<SearchHit>>(results);
    }
}

/// <summary>
/// Test event publisher that does nothing (swallows all events).
/// </summary>
public class TestEventPublisher : IEventPublisher
{
    public Task PublishAsync<T>(T domainEvent) where T : Domain.Common.IDomainEvent
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// Test rate limit service that always allows requests (no rate limiting in tests).
/// </summary>
public class TestRateLimitService : IRateLimitService
{
    public Task<RateLimitResult> CheckAsync(string key, int limit, TimeSpan window, CancellationToken cancellationToken = default)
    {
        // Always allow in tests
        return Task.FromResult(RateLimitResult.Allowed(0, limit, DateTime.UtcNow.Add(window)));
    }

    public Task RecordRequestAsync(string key, TimeSpan window, CancellationToken cancellationToken = default)
    {
        // No-op in tests
        return Task.CompletedTask;
    }
}

