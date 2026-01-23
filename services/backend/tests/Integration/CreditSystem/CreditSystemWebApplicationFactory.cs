using Application.Interfaces;
using Application.UseCases.Agent.VerifyAgentAccess;
using Application.UseCases.Subscription.ConsumeCredits;
using CreditSystem.IntegrationTests.Mocks;
using Domain.Entities;
using Infrastructure.Persistence;
using Integration.Shared.Fixtures;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using WebApi.Hubs;

namespace CreditSystem.IntegrationTests;

/// <summary>
/// Custom WebApplicationFactory for CreditSystem integration tests.
/// Includes all necessary mocks for ChatHub and credit operations.
/// </summary>
public class CreditSystemWebApplicationFactory : WebApplicationFactory<Program>
{
    public string DatabaseName { get; } = $"CreditSystemTestDb_{Guid.NewGuid()}";

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
            // Seed subscription plans at startup
            services.AddHostedService<CreditSystemTestDataSeeder>();

            // Remove all DbContext related services
            var dbContextServices = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                d.ServiceType == typeof(ApplicationDbContext) ||
                d.ServiceType.Name.Contains("DbContext")).ToList();

            foreach (var descriptor in dbContextServices)
            {
                services.Remove(descriptor);
            }

            // Add factory for creating DbContext with InMemory provider with shared database name
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
                .AddScheme<AuthenticationSchemeOptions, CreditSystemTestAuthHandler>("Test", _ => { });

            services.AddAuthorization();

            // Add missing CreditSystem configuration for testing
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
            services.AddSingleton<IVectorStorePort, Integration.Shared.Fixtures.TestVectorStore>();

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
            services.AddSingleton<IEventPublisher, Integration.Shared.Fixtures.TestEventPublisher>();

            // Add mocks for ChatHub dependencies
            services.AddSingleton<IChatCompletionPort, MockChatCompletionPort>();
            services.AddSingleton<ITokenCounter, MockTokenCounter>();

            // Use singleton for chat session repository to persist data across requests in tests
            services.AddSingleton<MockChatSessionRepository>();
            services.AddSingleton<IChatSessionRepository>(provider => provider.GetRequiredService<MockChatSessionRepository>());

            services.AddSingleton<IStringLocalizer<ChatHub>, MockStringLocalizer<ChatHub>>();
            services.AddSingleton<IActivityPublisher, MockActivityPublisher>();

            // Ensure SignalR is properly configured with detailed errors for testing
            services.AddSignalR(options =>
            {
                options.EnableDetailedErrors = true;
            });

            // Remove the actual handlers and their implementations
            var consumeCreditsHandler = services.SingleOrDefault(d =>
                d.ServiceType == typeof(IRequestHandler<ConsumeCreditsCommand, SubscriptionEntity>));
            if (consumeCreditsHandler != null)
            {
                services.Remove(consumeCreditsHandler);
            }

            var verifyAccessHandler = services.SingleOrDefault(d =>
                d.ServiceType == typeof(IRequestHandler<VerifyAgentAccessQuery, VerifyAgentAccessResult>));
            if (verifyAccessHandler != null)
            {
                services.Remove(verifyAccessHandler);
            }

            // Register mock handlers that will use the same DbContext instance
            services.AddScoped<IRequestHandler<ConsumeCreditsCommand, SubscriptionEntity>>(provider =>
            {
                var context = provider.GetRequiredService<ApplicationDbContext>();
                return new MockConsumeCreditsHandler(context);
            });

            services.AddScoped<IRequestHandler<VerifyAgentAccessQuery, VerifyAgentAccessResult>, MockVerifyAgentAccessHandler>();
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
/// Collection definition for CreditSystem integration tests.
/// </summary>
[CollectionDefinition("CreditSystem")]
public class CreditSystemTestCollection : ICollectionFixture<CreditSystemWebApplicationFactory>
{
}

#pragma warning disable CS0618 // ISystemClock is obsolete
public class CreditSystemTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public CreditSystemTestAuthHandler(
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
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
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

public class CreditSystemTestDataSeeder : IHostedService
{
    private readonly IServiceProvider _serviceProvider;

    public CreditSystemTestDataSeeder(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Seed subscription plans if not present
        if (!await context.SubscriptionPlans.AnyAsync(cancellationToken))
        {
            context.SubscriptionPlans.AddRange(
                new SubscriptionPlanEntity
                {
                    Id = Guid.NewGuid(),
                    PlanEnum = Domain.Enums.SubscriptionPlanEnum.Monthly100,
                    Price = 29.99m,
                    Credits = 100
                },
                new SubscriptionPlanEntity
                {
                    Id = Guid.NewGuid(),
                    PlanEnum = Domain.Enums.SubscriptionPlanEnum.Package5,
                    Price = 4.99m,
                    Credits = 5
                },
                new SubscriptionPlanEntity
                {
                    Id = Guid.NewGuid(),
                    PlanEnum = Domain.Enums.SubscriptionPlanEnum.Package10,
                    Price = 9.99m,
                    Credits = 10
                },
                new SubscriptionPlanEntity
                {
                    Id = Guid.NewGuid(),
                    PlanEnum = Domain.Enums.SubscriptionPlanEnum.Package25,
                    Price = 19.99m,
                    Credits = 25
                },
                new SubscriptionPlanEntity
                {
                    Id = Guid.NewGuid(),
                    PlanEnum = Domain.Enums.SubscriptionPlanEnum.Unlimited,
                    Price = 99.99m,
                    Credits = 0
                }
            );
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
