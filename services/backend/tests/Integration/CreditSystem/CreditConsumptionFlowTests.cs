using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Application.Interfaces;
using CreditSystem.IntegrationTests.Mocks;
using Microsoft.AspNetCore.SignalR;

namespace CreditSystem.IntegrationTests;

public class CreditConsumptionFlowTests : CreditSystemTestBase
{
    public CreditConsumptionFlowTests(WebApplicationFactory<Program> factory) : base(factory)
    {
    }

    [Fact]
    public async Task ChatMessage_Should_ConsumeCredits_Successfully()
    {
        // Arrange
        await using var context = CreateTestContext();
        
        // Ensure database is created
        await context.Database.EnsureCreatedAsync();
        
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid().ToString();
        var agentId = Guid.NewGuid();
        
        await SeedTestData(context, userId, agentId, 100, sessionId);
        
        var connection = await CreateSignalRConnection(sessionId, userId);
        
        // Capture any error messages
        var errorMessages = new List<string>();
        connection.On<string, string>("ReceiveMessage", (user, message) =>
        {
            if (user == "bot")
                errorMessages.Add(message);
        });

        // Act
        try
        {
            await connection.InvokeAsync("SendMessage", sessionId, "Hello AI", false);
            await Task.Delay(1000); // Wait for processing
        }
        catch (HubException ex)
        {
            // Log the actual error for debugging
            throw new Exception($"Hub error: {ex.Message}. Errors received: {string.Join(", ", errorMessages)}", ex);
        }

        // Assert
        // Need to get a fresh context from the application to check the updated values
        using (var scope = Factory.Services.CreateScope())
        {
            var appContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            var subscription = await appContext.Subscriptions
                .FirstOrDefaultAsync(s => s.UserId == userId);
            
            subscription!.Credits.Should().Be(95); // 100 - 5 (default message cost)
            
            var consumption = await appContext.CreditConsumptions
                .FirstOrDefaultAsync(c => c.UserId == userId);
            
            consumption.Should().NotBeNull();
            consumption!.CreditsConsumed.Should().Be(5);
            consumption.ConsumptionType.Should().Be("message");
        }
        
        await connection.DisposeAsync();
    }

    [Fact]
    public async Task ChatMessage_Should_FailWithInsufficientCredits()
    {
        // Arrange
        await using var context = CreateTestContext();
        
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid().ToString();
        var agentId = Guid.NewGuid();
        
        await SeedTestData(context, userId, agentId, 2, sessionId); // Only 2 credits
        
        var connection = await CreateSignalRConnection(sessionId, userId);
        
        var messagesReceived = new List<string>();
        connection.On<string, string>("ReceiveMessage", (user, message) =>
        {
            messagesReceived.Add(message);
        });

        // Act
        try
        {
            await connection.InvokeAsync("SendMessage", sessionId, "Hello AI", false);
        }
        catch (HubException)
        {
            // Expected - insufficient credits should throw
        }
        await Task.Delay(1000);

        // Assert
        messagesReceived.Should().NotBeEmpty("Should have received an error message");
        messagesReceived.Should().Contain(m => m.Contains("Insufficient"));
        
        // Get fresh context to verify credits unchanged
        using (var scope = Factory.Services.CreateScope())
        {
            var appContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var subscription = await appContext.Subscriptions
                .FirstOrDefaultAsync(s => s.UserId == userId);
            
            subscription!.Credits.Should().Be(2); // Credits unchanged
        }
        
        await connection.DisposeAsync();
    }

    [Fact]
    public async Task TokenBasedMessage_Should_ConsumeCorrectCredits()
    {
        // Arrange
        await using var context = CreateTestContext();
        
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid().ToString();
        var agentId = Guid.NewGuid();
        
        await SeedTestData(context, userId, agentId, 100, sessionId);
        
        // Add token-based cost configuration
        await context.MessageCosts.AddAsync(new MessageCostEntity
        {
            Id = Guid.NewGuid(),
            MessageId = Guid.NewGuid(),
            UserId = userId,
            BillingMethod = "tokens",
            CostPerToken = 0.002m, // 2 per 1k tokens
            TotalCredits = 5
        });
        await context.SaveChangesAsync();
        
        var connection = await CreateSignalRConnection(sessionId, userId);

        // Act
        var longMessage = string.Join(" ", Enumerable.Repeat("Hello world", 200)); // ~400 tokens
        await connection.InvokeAsync("SendMessage", sessionId, longMessage, true); // byTokens = true
        await Task.Delay(1000);

        // Assert - Get fresh context to verify credits
        using (var scope = Factory.Services.CreateScope())
        {
            var appContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var subscription = await appContext.Subscriptions
                .FirstOrDefaultAsync(s => s.UserId == userId);
            
            // Should consume minimum 5 credits (assuming token count calculation)
            subscription!.Credits.Should().BeLessThan(100);
            subscription.Credits.Should().BeGreaterThanOrEqualTo(95);
        }
        
        await connection.DisposeAsync();
    }

    [Fact]
    public async Task UnlimitedPlan_Should_NotConsumeCredits()
    {
        // Arrange
        await using var context = CreateTestContext();
        
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid().ToString();
        var agentId = Guid.NewGuid();
        
        // Create user with unlimited plan
        var user = new UserEntity
        {
            Id = userId,
            Email = "unlimited@test.com",
            Name = "Unlimited User"
        };
        
        var unlimitedPlan = new SubscriptionPlanEntity
        {
            Id = Guid.NewGuid(),
            PlanEnum = SubscriptionPlanEnum.Unlimited,
            Price = 99.99m,
            Credits = 0
        };
        
        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Credits = 0,
            SubscriptionPlanId = unlimitedPlan.Id,
            SubscriptionPlan = unlimitedPlan
        };
        
        var agent = new AgentEntity
        {
            Id = agentId,
            Name = "Test Agent",
            IsPublic = false,
            UserId = userId
        };
        
        var chatSession = new ChatSessionEntity
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            UserId = userId,
            AgentId = agentId,
            CreatedAt = DateTime.UtcNow
        };
        
        await context.Users.AddAsync(user);
        await context.SubscriptionPlans.AddAsync(unlimitedPlan);
        await context.Subscriptions.AddAsync(subscription);
        await context.Agents.AddAsync(agent);
        await context.ChatSessions.AddAsync(chatSession);
        await context.SaveChangesAsync();
        
        var connection = await CreateSignalRConnection(sessionId, userId);

        // Act
        await connection.InvokeAsync("SendMessage", sessionId, "Hello AI", false);
        await Task.Delay(1000);

        // Assert - Need to get a fresh context to check the updated values
        using (var scope = Factory.Services.CreateScope())
        {
            var appContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            var updatedSubscription = await appContext.Subscriptions
                .FirstOrDefaultAsync(s => s.UserId == userId);
            
            updatedSubscription!.Credits.Should().Be(0); // Still 0, no deduction
            
            // But consumption should still be tracked for analytics
            var consumption = await appContext.CreditConsumptions
                .FirstOrDefaultAsync(c => c.UserId == userId);
            
            consumption.Should().NotBeNull();
            consumption!.CreditsConsumed.Should().Be(0); // 0 credits consumed for unlimited
        }
        
        await connection.DisposeAsync();
    }

    [Fact]
    public async Task CreditValidation_Should_PreventInvalidOperations()
    {
        // Arrange
        await using var context = CreateTestContext();
        
        var userId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        
        await SeedTestData(context, userId, Guid.NewGuid(), 10, null);
        
        // Add expensive plugin
        var plugin = new PluginEntity
        {
            Id = pluginId,
            Name = "Expensive Plugin",
            SystemName = "ExpensivePlugin",
            IsActive = true,
            PriceCredits = 50
        };
        await context.Plugins.AddAsync(plugin);
        await context.SaveChangesAsync();
        
        await SetAuthHeaderAsync(userId);

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/credit-validation/plugin/{pluginId}/validate", 
            new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<CreditValidationResponse>();
        result!.IsValid.Should().BeFalse();
        result.RequiredCredits.Should().Be(50);
        result.AvailableCredits.Should().Be(10);
        result.ErrorMessage.Should().Contain("Insufficient");
    }

    [Fact]
    public async Task MessageWithPlugins_Should_CalculateTotalCost()
    {
        // Arrange
        await using var context = CreateTestContext();
        
        var userId = Guid.NewGuid();
        var plugin1Id = Guid.NewGuid();
        var plugin2Id = Guid.NewGuid();
        
        await SeedTestData(context, userId, Guid.NewGuid(), 100, null);
        
        // Add action costs for plugins
        await context.ActionCosts.AddRangeAsync(
            new ActionCostEntity
            {
                Id = Guid.NewGuid(),
                ActionName = $"plugin_usage_{plugin1Id}",
                Credits = 10
            },
            new ActionCostEntity
            {
                Id = Guid.NewGuid(),
                ActionName = $"plugin_usage_{plugin2Id}",
                Credits = 15
            }
        );
        await context.SaveChangesAsync();
        
        await SetAuthHeaderAsync(userId);

        // Act
        var request = new
        {
            messageContent = "Test message",
            pluginIds = new[] { plugin1Id, plugin2Id }
        };
        
        var response = await Client.PostAsJsonAsync("/api/credit-validation/message/validate", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<CreditValidationResponse>();
        result!.IsValid.Should().BeTrue();
        result.RequiredCredits.Should().Be(30); // 5 (message) + 10 + 15
        result.CostBreakdown!.BaseCost.Should().Be(5);
        result.CostBreakdown.PluginCosts.Should().HaveCount(2);
        result.CostBreakdown.TotalCost.Should().Be(30);
    }

    private async Task SeedTestData(ApplicationDbContext context, Guid userId, Guid agentId, int credits, string? sessionId = null)
    {
        var user = new UserEntity
        {
            Id = userId,
            Email = "test@example.com",
            Name = "Test User"
        };
        
        var plan = new SubscriptionPlanEntity
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
            Credits = credits,
            SubscriptionPlanId = plan.Id
        };
        
        var agent = new AgentEntity
        {
            Id = agentId,
            Name = "Test Agent",
            IsPublic = false,
            UserId = userId
        };
        
        var chatSession = new ChatSessionEntity
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId ?? Guid.NewGuid().ToString(),
            UserId = userId,
            AgentId = agentId,
            CreatedAt = DateTime.UtcNow
        };
        
        await context.Users.AddAsync(user);
        await context.SubscriptionPlans.AddAsync(plan);
        await context.Subscriptions.AddAsync(subscription);
        await context.Agents.AddAsync(agent);
        
        if (sessionId != null)
        {
            await context.ChatSessions.AddAsync(chatSession);
        }
        
        await context.SaveChangesAsync();
        
        // Also add to mock repository if we have a session
        if (sessionId != null)
        {
            using var scope = Factory.Services.CreateScope();
            var mockRepo = scope.ServiceProvider.GetRequiredService<IChatSessionRepository>() as MockChatSessionRepository;
            mockRepo?.AddSession(chatSession);
        }
    }

    private async Task<HubConnection> CreateSignalRConnection(string sessionId, Guid userId)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl($"http://localhost/chathub?sessionId={sessionId}", options =>
            {
                options.HttpMessageHandlerFactory = _ => Factory.Server.CreateHandler();
                options.AccessTokenProvider = async () => await GenerateTestToken(userId);
            })
            .Build();
        
        await connection.StartAsync();
        return connection;
    }

    private async Task SetAuthHeaderAsync(Guid userId)
    {
        var token = await GenerateTestToken(userId);
        Client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    private class CreditValidationResponse
    {
        public bool IsValid { get; set; }
        public int RequiredCredits { get; set; }
        public int AvailableCredits { get; set; }
        public bool HasUnlimitedCredits { get; set; }
        public string? ErrorMessage { get; set; }
        public CostBreakdown? CostBreakdown { get; set; }
    }

    private class CostBreakdown
    {
        public int BaseCost { get; set; }
        public Dictionary<string, int> PluginCosts { get; set; } = new();
        public Dictionary<string, int> WorkflowCosts { get; set; } = new();
        public int TotalCost { get; set; }
    }
}