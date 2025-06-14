using System.Net.Http.Json;
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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using CreditSystem.IntegrationTests.Mocks;
using Microsoft.Extensions.Localization;
using WebApi.Hubs;
using MediatR;
using Application.UseCases.Subscription.ConsumeCredits;
using Application.UseCases.Agent.VerifyAgentAccess;

namespace CreditSystem.IntegrationTests;

public abstract class CreditSystemTestBase : IClassFixture<WebApplicationFactory<Program>>
{
    protected readonly WebApplicationFactory<Program> Factory;
    protected readonly HttpClient Client;
    private readonly string _dbName = $"TestDb_{Guid.NewGuid()}";

    protected CreditSystemTestBase(WebApplicationFactory<Program> factory)
    {
        Factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
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

                // Add factory for creating DbContext with InMemory provider with shared database name
                var dbContextOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseInMemoryDatabase(databaseName: _dbName)
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
                    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", options => { });
                
                services.AddAuthorization();
                
                // Add missing CreditSystem configuration for testing
                services.Configure<WebApi.Configuration.CreditSystemConfiguration>(
                    options =>
                    {
                        options.TokensPerCredit = 100;
                        options.MinimumCreditsPerMessage = 5;
                    });
                
                // Replace UnitOfWork with TestUnitOfWork for InMemory database
                var unitOfWorkDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IUnitOfWork));
                if (unitOfWorkDescriptor != null)
                {
                    services.Remove(unitOfWorkDescriptor);
                }
                services.AddScoped<IUnitOfWork, TestUnitOfWork>();
                
                // Replace Redis cache with in-memory test cache
                var cacheDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ICacheStore));
                if (cacheDescriptor != null)
                {
                    services.Remove(cacheDescriptor);
                }
                services.AddSingleton<ICacheStore, TestCacheStore>();
                
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
        });
        
        Client = Factory.CreateClient();
    }
    
    protected ApplicationDbContext CreateTestContext()
    {
        // Use the same database name as the one configured in the WebApplicationFactory
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: _dbName)
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
    
    protected async Task<string> GenerateTestToken(Guid userId, string email = "test@example.com", string[]? roles = null)
    {
        await using var context = CreateTestContext();
        
        // Create test user if not exists
        var user = await context.Users.FindAsync(userId);
        if (user == null)
        {
            user = new UserEntity
            {
                Id = userId,
                Email = email,
                Name = "Test User",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await context.Users.AddAsync(user);
            await context.SaveChangesAsync();
        }

        // For integration tests, we'll create a simple test token
        // In a real scenario, this would use the JWT service
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(userId.ToString()));
    }
}

[Obsolete]
public class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock)
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
        
        // For test purposes, we decode the base64 userId from the token
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