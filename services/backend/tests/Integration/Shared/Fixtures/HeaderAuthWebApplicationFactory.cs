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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Integration.Shared.Fixtures;

/// <summary>
/// WebApplicationFactory for tests that use X-Test-User-Id header for authentication.
/// </summary>
public class HeaderAuthWebApplicationFactory : WebApplicationFactory<Program>
{
    public string DatabaseName { get; } = $"HeaderAuthTestDb_{Guid.NewGuid()}";

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

            // Configure test authentication with header-based user ID
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "Test";
                options.DefaultChallengeScheme = "Test";
            }).AddScheme<AuthenticationSchemeOptions, HeaderTestAuthHandler>("Test", _ => { });

            services.AddAuthorization();

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

            // Seed subscription plans at startup
            services.AddHostedService<TestDataSeeder>();

            // Ensure SignalR is properly configured with detailed errors for testing
            services.AddSignalR(options =>
            {
                options.EnableDetailedErrors = true;
            });
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
/// Collection definition for tests using header-based authentication.
/// </summary>
[CollectionDefinition("HeaderAuth")]
public class HeaderAuthTestCollection : ICollectionFixture<HeaderAuthWebApplicationFactory>
{
}

/// <summary>
/// Authentication handler that uses X-Test-User-Id header for user identification.
/// </summary>
public class HeaderTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public HeaderTestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Check for test user ID header
        if (!Request.Headers.TryGetValue("X-Test-User-Id", out var userIdHeader)
            || string.IsNullOrEmpty(userIdHeader.FirstOrDefault()))
        {
            // No user ID provided - allow anonymous access (no authentication)
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!Guid.TryParse(userIdHeader.FirstOrDefault(), out var userId))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid user ID"));
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, "Test User"),
            new Claim(ClaimTypes.Email, "test@example.com")
        };

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
