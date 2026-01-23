using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Infrastructure.Persistence;
using Integration.Shared.Fixtures;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Subscription.IntegrationTests;

[Collection("Integration")]
public class UnlimitedSubscriptionTests
{
    private readonly CustomWebApplicationFactory _factory;

    public UnlimitedSubscriptionTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Unlimited_Subscription_Should_Not_Deduct_Credits_For_Messages()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid().ToString();

        var user = new UserEntity
        {
            Id = userId,
            Email = "unlimited@example.com",
            Name = "Unlimited User"
        };

        var unlimitedPlan = new SubscriptionPlanEntity
        {
            Id = Guid.NewGuid(),
            PlanEnum = SubscriptionPlanEnum.Unlimited,
            Price = 99.99m,
            Credits = 999999
        };

        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Credits = 100, // Starting credits
            SubscriptionPlanId = unlimitedPlan.Id,
            SubscriptionPlan = unlimitedPlan,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        };

        var agent = new AgentEntity
        {
            Id = Guid.NewGuid(),
            Name = "Test Agent",
            IsPublic = false,
            UserId = userId
        };

        var chatSession = new ChatSessionEntity
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            UserId = userId,
            AgentId = agent.Id,
            CreatedAt = DateTime.UtcNow
        };

        await context.Users.AddAsync(user);
        await context.SubscriptionPlans.AddAsync(unlimitedPlan);
        await context.Subscriptions.AddAsync(subscription);
        await context.Agents.AddAsync(agent);
        await context.ChatSessions.AddAsync(chatSession);
        await context.SaveChangesAsync();

        var creditsBefore = subscription.Credits;

        // Set up SignalR connection
        var client = _factory.CreateClient();
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(client.BaseAddress!, $"/chathub?sessionId={sessionId}"), options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult<string?>(GenerateTestToken(userId));
            })
            .Build();

        await connection.StartAsync();

        // Act - Send multiple messages (SendMessage signature: sessionId, message, byTokens, agentId, teamId)
        for (int i = 0; i < 5; i++)
        {
            await connection.InvokeAsync("SendMessage", sessionId, $"Test message {i}", false, (Guid?)null, (Guid?)null);
            await Task.Delay(500);
        }

        await Task.Delay(2000);

        // Assert
        await context.Entry(subscription).ReloadAsync();
        subscription.Credits.Should().Be(creditsBefore); // No credits deducted

        // Check that no credit consumptions were recorded
        var consumptions = await context.CreditConsumptions
            .Where(c => c.UserId == userId && c.ConsumptionType == "message")
            .ToListAsync();

        consumptions.Should().BeEmpty(); // No consumption records for unlimited plan

        await connection.DisposeAsync();
    }

    [Fact]
    public async Task Unlimited_Subscription_Should_Not_Deduct_Credits_For_Plugin_Usage()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var userId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();

        var user = new UserEntity
        {
            Id = userId,
            Email = "unlimited@example.com",
            Name = "Unlimited User"
        };

        var unlimitedPlan = new SubscriptionPlanEntity
        {
            Id = Guid.NewGuid(),
            PlanEnum = SubscriptionPlanEnum.Unlimited,
            Price = 99.99m,
            Credits = 999999
        };

        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Credits = 100,
            SubscriptionPlanId = unlimitedPlan.Id,
            SubscriptionPlan = unlimitedPlan,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        };

        var plugin = new PluginEntity
        {
            Id = pluginId,
            Name = "Expensive Plugin",
            SystemName = "ExpensivePlugin",
            IsActive = true,
            PriceCredits = 50 // Very expensive plugin
        };

        var userPlugin = new UserPluginEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PluginId = pluginId,
            PurchasedAt = DateTime.UtcNow,
            IsActive = true
        };

        await context.Users.AddAsync(user);
        await context.SubscriptionPlans.AddAsync(unlimitedPlan);
        await context.Subscriptions.AddAsync(subscription);
        await context.Plugins.AddAsync(plugin);
        await context.UserPlugins.AddAsync(userPlugin);
        await context.SaveChangesAsync();

        var creditsBefore = subscription.Credits;

        // Simulate plugin usage through credit tracking service
        // In real scenario, this would happen through the TelemetryFunctionFilter

        // Act - The system should check if subscription is unlimited before deducting
        subscription.ConsumeCredits(50); // This should not actually deduct for unlimited plan

        // Assert
        subscription.Credits.Should().Be(creditsBefore); // No credits deducted
        subscription.HasUnlimitedCredits().Should().BeTrue();
    }

    [Fact]
    public async Task Regular_Subscription_Should_Deduct_Credits_Normally()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var userId = Guid.NewGuid();

        var user = new UserEntity
        {
            Id = userId,
            Email = "regular@example.com",
            Name = "Regular User"
        };

        var regularPlan = new SubscriptionPlanEntity
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
            SubscriptionPlanId = regularPlan.Id,
            SubscriptionPlan = regularPlan,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        };

        await context.Users.AddAsync(user);
        await context.SubscriptionPlans.AddAsync(regularPlan);
        await context.Subscriptions.AddAsync(subscription);
        await context.SaveChangesAsync();

        // Act
        subscription.ConsumeCredits(10);

        // Assert
        subscription.Credits.Should().Be(40); // 50 - 10
        subscription.HasUnlimitedCredits().Should().BeFalse();
    }

    [Fact]
    public async Task Switching_To_Unlimited_Plan_Should_Enable_Unlimited_Credits()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var userId = Guid.NewGuid();

        var user = new UserEntity
        {
            Id = userId,
            Email = "upgrade@example.com",
            Name = "Upgrade User"
        };

        var regularPlan = new SubscriptionPlanEntity
        {
            Id = Guid.NewGuid(),
            PlanEnum = SubscriptionPlanEnum.Monthly100,
            Price = 29.99m,
            Credits = 100
        };

        var unlimitedPlan = new SubscriptionPlanEntity
        {
            Id = Guid.NewGuid(),
            PlanEnum = SubscriptionPlanEnum.Unlimited,
            Price = 99.99m,
            Credits = 999999
        };

        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Credits = 50,
            SubscriptionPlanId = regularPlan.Id,
            SubscriptionPlan = regularPlan,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        };

        await context.Users.AddAsync(user);
        await context.SubscriptionPlans.AddRangeAsync(regularPlan, unlimitedPlan);
        await context.Subscriptions.AddAsync(subscription);
        await context.SaveChangesAsync();

        // Act - Upgrade to unlimited plan
        subscription.SubscriptionPlanId = unlimitedPlan.Id;
        subscription.SubscriptionPlan = unlimitedPlan;
        subscription.Credits = 999999; // Reset credits to unlimited amount
        await context.SaveChangesAsync();

        var creditsBefore = subscription.Credits;
        subscription.ConsumeCredits(100); // Try to consume credits

        // Assert
        subscription.Credits.Should().Be(creditsBefore); // No deduction
        subscription.HasUnlimitedCredits().Should().BeTrue();
    }

    [Fact]
    public async Task Unlimited_Plan_Should_Allow_Unlimited_Plugin_Uses_Per_Month()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var userId = Guid.NewGuid();

        var unlimitedPlan = new SubscriptionPlanEntity
        {
            Id = Guid.NewGuid(),
            PlanEnum = SubscriptionPlanEnum.Unlimited,
            Price = 99.99m,
            Credits = 999999
        };

        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Credits = 999999,
            SubscriptionPlanId = unlimitedPlan.Id,
            SubscriptionPlan = unlimitedPlan,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        };

        var plugin = new PluginEntity
        {
            Id = Guid.NewGuid(),
            Name = "Heavy Plugin",
            SystemName = "HeavyPlugin",
            IsActive = true,
            PriceCredits = 20
        };

        await context.SubscriptionPlans.AddAsync(unlimitedPlan);
        await context.Subscriptions.AddAsync(subscription);
        await context.Plugins.AddAsync(plugin);
        await context.SaveChangesAsync();

        var creditsBefore = subscription.Credits;

        // Act - Simulate 100 plugin uses
        for (int i = 0; i < 100; i++)
        {
            // In real implementation, this would be done by the system
            // For unlimited plans, ConsumeCredits should not deduct
            subscription.ConsumeCredits(plugin.PriceCredits);
        }

        // Assert
        subscription.Credits.Should().Be(creditsBefore); // No change in credits
        subscription.HasUnlimitedCredits().Should().BeTrue();

        // Even after 100 uses, user should still have unlimited credits
        subscription.Credits.Should().BeGreaterThan(100000); // Still a very high number
    }

    private string GenerateTestToken(Guid userId)
    {
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(userId.ToString()));
    }
}
