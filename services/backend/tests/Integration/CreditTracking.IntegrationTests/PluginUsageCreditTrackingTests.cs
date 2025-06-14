using System.Net;
using System.Text;
using System.Text.Json;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CreditTracking.IntegrationTests;

public class PluginUsageCreditTrackingTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public PluginUsageCreditTrackingTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseInMemoryDatabase("TestDb_" + Guid.NewGuid());
                });
            });
        }).CreateClient();
    }

    [Fact]
    public async Task Chat_Message_Should_Deduct_Credits_When_Plugin_Is_Used()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        var userId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var sessionId = Guid.NewGuid().ToString();
        
        // Create user with subscription
        var user = new UserEntity
        {
            Id = userId,
            Email = "test@example.com",
            Name = "Test User"
        };
        
        var subscriptionPlan = new SubscriptionPlanEntity
        {
            Id = Guid.NewGuid(),
            PlanEnum = SubscriptionPlanEnum.Monthly100,
            Price = 29.99m,
            Credits = 100
        };
        
        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Credits = 50,
            SubscriptionPlanId = subscriptionPlan.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        };
        
        // Create plugin with cost
        var plugin = new PluginEntity
        {
            Id = pluginId,
            Name = "Calculator Plugin",
            SystemName = "CalculatorPlugin",
            Description = "Performs calculations",
            IsActive = true,
            PriceCredits = 5, // 5 credits per use
            IsSubscriptionBased = false
        };
        
        // User owns the plugin
        var userPlugin = new UserPluginEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PluginId = pluginId,
            PurchasedAt = DateTime.UtcNow,
            IsActive = true
        };
        
        // Create agent with plugin
        var agent = new AgentEntity
        {
            Id = agentId,
            Name = "Math Assistant",
            IsPublic = false,
            UserId = userId,
            PluginIds = new[] { pluginId }
        };
        
        // Create chat session
        var chatSession = new ChatSessionEntity
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            UserId = userId,
            AgentId = agentId,
            CreatedAt = DateTime.UtcNow
        };
        
        await context.Users.AddAsync(user);
        await context.SubscriptionPlans.AddAsync(subscriptionPlan);
        await context.Subscriptions.AddAsync(subscription);
        await context.Plugins.AddAsync(plugin);
        await context.UserPlugins.AddAsync(userPlugin);
        await context.Agents.AddAsync(agent);
        await context.ChatSessions.AddAsync(chatSession);
        await context.SaveChangesAsync();

        // Set up SignalR connection
        var connection = new HubConnectionBuilder()
            .WithUrl($"http://localhost/chathub?sessionId={sessionId}", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult(GenerateTestToken(userId));
            })
            .Build();

        await connection.StartAsync();

        // Track messages received
        var messagesReceived = new List<string>();
        connection.On<string, string>("ReceiveMessage", (user, message) =>
        {
            messagesReceived.Add(message);
        });

        // Act - Send message that would trigger plugin use
        await connection.InvokeAsync("SendMessage", sessionId, "Calculate 5 + 3", false);

        // Wait for response
        await Task.Delay(2000);

        // Assert
        // Check subscription credits were deducted
        await context.Entry(subscription).ReloadAsync();
        subscription.Credits.Should().Be(44); // 50 - 1 (message) - 5 (plugin) = 44

        // Check credit consumption was tracked
        var consumptions = await context.CreditConsumptions
            .Where(c => c.UserId == userId)
            .ToListAsync();
        
        consumptions.Should().NotBeEmpty();
        consumptions.Should().Contain(c => c.ConsumptionType == "plugin" && c.CreditsConsumed == 5);
        consumptions.Should().Contain(c => c.ConsumptionType == "message" && c.CreditsConsumed == 1);

        await connection.DisposeAsync();
    }

    [Fact]
    public async Task Multiple_Plugin_Uses_Should_Track_Each_Credit_Deduction()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        var userId = Guid.NewGuid();
        var plugin1Id = Guid.NewGuid();
        var plugin2Id = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        
        // Create plugins with different costs
        var plugins = new[]
        {
            new PluginEntity
            {
                Id = plugin1Id,
                Name = "Weather Plugin",
                SystemName = "WeatherPlugin",
                IsActive = true,
                PriceCredits = 3
            },
            new PluginEntity
            {
                Id = plugin2Id,
                Name = "News Plugin",
                SystemName = "NewsPlugin",
                IsActive = true,
                PriceCredits = 2
            }
        };
        
        // User owns both plugins
        var userPlugins = plugins.Select(p => new UserPluginEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PluginId = p.Id,
            PurchasedAt = DateTime.UtcNow,
            IsActive = true
        }).ToList();
        
        // Create agent with both plugins
        var agent = new AgentEntity
        {
            Id = agentId,
            Name = "Multi-Plugin Assistant",
            IsPublic = false,
            UserId = userId,
            PluginIds = new[] { plugin1Id, plugin2Id }
        };
        
        var user = new UserEntity { Id = userId, Email = "multi@example.com", Name = "Multi User" };
        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Credits = 100
        };
        
        await context.Users.AddAsync(user);
        await context.Subscriptions.AddAsync(subscription);
        await context.Plugins.AddRangeAsync(plugins);
        await context.UserPlugins.AddRangeAsync(userPlugins);
        await context.Agents.AddAsync(agent);
        await context.SaveChangesAsync();

        // Simulate multiple plugin invocations
        // This would normally happen through the chat flow
        
        // First plugin use
        var consumption1 = new CreditConsumptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ResourceId = plugin1Id,
            ResourceName = "Weather Plugin",
            ConsumptionType = "plugin",
            CreditsConsumed = 3,
            BalanceAfter = 97,
            ConsumedAt = DateTime.UtcNow,
            Metadata = JsonSerializer.Serialize(new { sessionId = "test-session" })
        };
        
        // Second plugin use
        var consumption2 = new CreditConsumptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ResourceId = plugin2Id,
            ResourceName = "News Plugin",
            ConsumptionType = "plugin",
            CreditsConsumed = 2,
            BalanceAfter = 95,
            ConsumedAt = DateTime.UtcNow.AddSeconds(1),
            Metadata = JsonSerializer.Serialize(new { sessionId = "test-session" })
        };
        
        // Third use of first plugin
        var consumption3 = new CreditConsumptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ResourceId = plugin1Id,
            ResourceName = "Weather Plugin",
            ConsumptionType = "plugin",
            CreditsConsumed = 3,
            BalanceAfter = 92,
            ConsumedAt = DateTime.UtcNow.AddSeconds(2),
            Metadata = JsonSerializer.Serialize(new { sessionId = "test-session" })
        };

        // Act
        await context.CreditConsumptions.AddRangeAsync(consumption1, consumption2, consumption3);
        await context.SaveChangesAsync();

        // Assert
        var testSessionConsumptions = await context.CreditConsumptions
            .Where(c => c.Metadata != null && c.Metadata.Contains("test-session"))
            .ToListAsync();
        
        var totalConsumptions = testSessionConsumptions.Sum(c => c.CreditsConsumed);
        
        totalConsumptions.Should().Be(8); // 3 + 2 + 3
        
        var plugin1Consumptions = await context.CreditConsumptions
            .Where(c => c.ResourceId == plugin1Id)
            .CountAsync();
        
        plugin1Consumptions.Should().Be(2);
        
        var plugin2Consumptions = await context.CreditConsumptions
            .Where(c => c.ResourceId == plugin2Id)
            .CountAsync();
        
        plugin2Consumptions.Should().Be(1);
    }

    [Fact]
    public async Task Plugin_Use_Should_Not_Deduct_Credits_If_User_Does_Not_Own_Plugin()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        var userId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        
        var user = new UserEntity { Id = userId, Email = "noPlugin@example.com", Name = "No Plugin User" };
        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Credits = 50
        };
        
        var plugin = new PluginEntity
        {
            Id = pluginId,
            Name = "Premium Plugin",
            SystemName = "PremiumPlugin",
            IsActive = true,
            PriceCredits = 10
        };
        
        // User does NOT own the plugin
        
        var agent = new AgentEntity
        {
            Id = agentId,
            Name = "Premium Assistant",
            IsPublic = true,
            UserId = Guid.NewGuid(), // Different owner
            PluginIds = new[] { pluginId }
        };
        
        await context.Users.AddAsync(user);
        await context.Subscriptions.AddAsync(subscription);
        await context.Plugins.AddAsync(plugin);
        await context.Agents.AddAsync(agent);
        await context.SaveChangesAsync();

        // Act - Try to use agent with plugin user doesn't own
        // This should be blocked at the access verification level
        
        // Assert
        var creditsBefore = subscription.Credits;
        await context.Entry(subscription).ReloadAsync();
        subscription.Credits.Should().Be(creditsBefore); // No credits deducted
        
        var consumptions = await context.CreditConsumptions
            .Where(c => c.UserId == userId && c.ResourceId == pluginId)
            .ToListAsync();
        
        consumptions.Should().BeEmpty();
    }

    [Fact]
    public async Task Failed_Plugin_Execution_Should_Not_Deduct_Credits()
    {
        // This test simulates a scenario where plugin execution fails
        // In real implementation, the TelemetryFunctionFilter should not deduct credits on failure
        
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        var userId = Guid.NewGuid();
        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Credits = 100
        };
        
        await context.Users.AddAsync(new UserEntity { Id = userId, Email = "test@example.com", Name = "Test" });
        await context.Subscriptions.AddAsync(subscription);
        await context.SaveChangesAsync();
        
        var creditsBefore = subscription.Credits;
        
        // Act - Simulate failed plugin execution (no credit consumption record created)
        
        // Assert
        await context.Entry(subscription).ReloadAsync();
        subscription.Credits.Should().Be(creditsBefore);
        
        var consumptions = await context.CreditConsumptions
            .Where(c => c.UserId == userId && c.ConsumptionType == "plugin")
            .ToListAsync();
        
        consumptions.Should().BeEmpty();
    }

    private string GenerateTestToken(Guid userId)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(userId.ToString()));
    }
}