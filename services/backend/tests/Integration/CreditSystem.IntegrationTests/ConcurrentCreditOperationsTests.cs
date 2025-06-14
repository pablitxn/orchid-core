using System.Net;
using System.Net.Http.Json;
using System.Text;
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

namespace CreditSystem.IntegrationTests;

public class ConcurrentCreditOperationsTests : CreditSystemTestBase
{
    public ConcurrentCreditOperationsTests(WebApplicationFactory<Program> factory) : base(factory)
    {
    }

    [Fact]
    public async Task ConcurrentMessages_Should_DeductCreditsCorrectly()
    {
        // Arrange
        await using var context = CreateTestContext();
        
        var userId = Guid.NewGuid();
        var initialCredits = 100;
        await SeedUserWithCredits(context, userId, initialCredits);
        
        // Get the mock repository from the service provider
        using var scope = Factory.Services.CreateScope();
        var mockRepo = scope.ServiceProvider.GetRequiredService<IChatSessionRepository>() as MockChatSessionRepository;
        
        // Create multiple sessions for concurrent messages
        var sessions = new List<(string sessionId, Guid agentId)>();
        for (int i = 0; i < 3; i++)
        {
            var agentId = Guid.NewGuid();
            var sessionId = Guid.NewGuid().ToString();
            
            var agent = new AgentEntity
            {
                Id = agentId,
                Name = $"Agent {i}",
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
            
            await context.Agents.AddAsync(agent);
            await context.ChatSessions.AddAsync(chatSession);
            
            // Also add to mock repository so ChatHub can find it
            mockRepo?.AddSession(chatSession);
            
            sessions.Add((sessionId, agentId));
        }
        await context.SaveChangesAsync();

        // Act - Send concurrent messages
        var tasks = new List<Task>();
        var connections = new List<HubConnection>();
        
        foreach (var (sessionId, _) in sessions)
        {
            var connection = await CreateSignalRConnection(sessionId, userId);
            connections.Add(connection);
            
            // Send 3 messages per connection sequentially to avoid race conditions
            for (int i = 0; i < 3; i++)
            {
                var messageIndex = i;
                var capturedSessionId = sessionId; // Capture for closure
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await connection.InvokeAsync("SendMessage", capturedSessionId, $"Concurrent message {messageIndex}", false);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error sending message: {ex.Message}");
                    }
                }));
            }
        }
        
        await Task.WhenAll(tasks);
        await Task.Delay(3000); // Wait longer for all processing

        // Assert - Get fresh context to verify credits
        using (var assertScope = Factory.Services.CreateScope())
        {
            var appContext = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            var subscription = await appContext.Subscriptions
                .FirstOrDefaultAsync(s => s.UserId == userId);
            
            // 9 messages × 5 credits = 45 credits consumed
            subscription!.Credits.Should().Be(55); // 100 - 45
            
            // Verify all consumptions were tracked
            var consumptions = await appContext.CreditConsumptions
                .Where(c => c.UserId == userId)
                .ToListAsync();
            
            consumptions.Should().HaveCount(9);
            consumptions.Sum(c => c.CreditsConsumed).Should().Be(45);
        }
        
        // Cleanup connections
        foreach (var connection in connections)
        {
            await connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task ConcurrentPurchases_Should_PreventDoublePurchase()
    {
        // Arrange
        await using var context = CreateTestContext();
        
        var userId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        
        await SeedUserWithCredits(context, userId, 100);
        
        var plugin = new PluginEntity
        {
            Id = pluginId,
            Name = "Concurrent Test Plugin",
            SystemName = "ConcurrentPlugin",
            IsActive = true,
            PriceCredits = 50
        };
        await context.Plugins.AddAsync(plugin);
        await context.SaveChangesAsync();

        // Act - Try to purchase the same plugin concurrently
        var clients = new List<HttpClient>();
        for (int i = 0; i < 5; i++)
        {
            clients.Add(await CreateAuthenticatedClient(userId));
        }
        
        var tasks = clients.Select(client => 
            client.PostAsync($"/api/marketplace/plugins/{pluginId}/purchase", null)).ToList();
        
        var responses = await Task.WhenAll(tasks);

        // Assert
        // Only one purchase should succeed
        var successCount = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        successCount.Should().Be(1);
        
        // Others should fail with "already owned" error
        var failureCount = responses.Count(r => r.StatusCode == HttpStatusCode.BadRequest);
        failureCount.Should().Be(4);
        
        // Verify only one ownership record exists
        var ownerships = await context.UserPlugins
            .Where(up => up.UserId == userId && up.PluginId == pluginId)
            .ToListAsync();
        ownerships.Should().HaveCount(1);
        
        // Verify credits were deducted only once
        var subscription = await context.Subscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId);
        subscription!.Credits.Should().Be(50); // 100 - 50
    }

    [Fact]
    public async Task ConcurrentConsumption_Should_HandleVersionConflicts()
    {
        // Arrange
        await using var context = CreateTestContext();
        
        var userId = Guid.NewGuid();
        await SeedUserWithCredits(context, userId, 50);
        
        await SetAuthHeader(userId);

        // Act - Send multiple credit-consuming operations concurrently
        var tasks = new List<Task<HttpResponseMessage>>();
        
        // Mix of different operations
        tasks.Add(Client.PostAsJsonAsync("/api/credit-validation/operation/validate", new
        {
            requiredCredits = 10,
            operationType = "operation1"
        }));
        
        tasks.Add(Client.PostAsJsonAsync("/api/credit-validation/operation/validate", new
        {
            requiredCredits = 15,
            operationType = "operation2"
        }));
        
        tasks.Add(Client.PostAsJsonAsync("/api/credit-validation/operation/validate", new
        {
            requiredCredits = 20,
            operationType = "operation3"
        }));
        
        var validationResults = await Task.WhenAll(tasks);

        // All validations should succeed (total 45 credits needed, have 50)
        foreach (var response in validationResults)
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<CreditValidationResponse>();
            result!.IsValid.Should().BeTrue();
        }

        // Now try to consume credits concurrently
        // This tests the retry mechanism in ConsumeCreditsHandler
        var consumeTasks = new List<Task>();
        for (int i = 0; i < 3; i++)
        {
            var sessionId = Guid.NewGuid().ToString();
            var agentId = Guid.NewGuid();
            
            await SeedAgentAndSession(context, userId, agentId, sessionId);
            
            var connection = await CreateSignalRConnection(sessionId, userId);
            consumeTasks.Add(connection.InvokeAsync("SendMessage", sessionId, $"Message {i}", false));
        }
        
        await Task.WhenAll(consumeTasks);
        await Task.Delay(1000);

        // Assert - All operations should have succeeded with retry logic
        using (var assertScope = Factory.Services.CreateScope())
        {
            var appContext = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var subscription = await appContext.Subscriptions
                .FirstOrDefaultAsync(s => s.UserId == userId);
            
            subscription!.Credits.Should().Be(35); // 50 - (3 × 5)
            subscription.Version.Should().BeGreaterThan(0); // Version should have incremented
        }
    }

    // TODO: Enable this test when UserCreditLimits DbSet is added to ApplicationDbContext
    // [Fact]
    // public async Task ConcurrentLimitUpdates_Should_MaintainConsistency()
    // {
    //     // Arrange
    //     await using var context = CreateTestContext();
    //     
    //     var userId = Guid.NewGuid();
    //     await SeedUserWithCredits(context, userId, 200);
    //     
    //     // Create credit limit
    //     var creditLimit = new UserCreditLimitEntity
    //     {
    //         Id = Guid.NewGuid(),
    //         UserId = userId,
    //         LimitType = "daily",
    //         MaxCredits = 100,
    //         ConsumedCredits = 0,
    //         PeriodStartDate = DateTime.UtcNow.Date,
    //         PeriodEndDate = DateTime.UtcNow.Date.AddDays(1),
    //         IsActive = true,
    //         // Version = 0
    //     };
    //     await context.UserCreditLimits.AddAsync(creditLimit);
    //     await context.SaveChangesAsync();
    // 
    //     // Act - Consume credits concurrently
    //     var tasks = new List<Task>();
    //     for (int i = 0; i < 10; i++)
    //     {
    //         var sessionId = Guid.NewGuid().ToString();
    //         var agentId = Guid.NewGuid();
    //         
    //         await SeedAgentAndSession(context, userId, agentId, sessionId);
    //         
    //         tasks.Add(Task.Run(async () =>
    //         {
    //             var connection = await CreateSignalRConnection(sessionId, userId);
    //             await connection.InvokeAsync("SendMessage", sessionId, "Test message", false);
    //             await connection.DisposeAsync();
    //         }));
    //     }
    //     
    //     await Task.WhenAll(tasks);
    //     await Task.Delay(2000);
    // 
    //     // Assert
    //     await context.Entry(creditLimit).ReloadAsync();
    //     
    //     // Should have consumed 50 credits (10 messages × 5 credits)
    //     creditLimit.ConsumedCredits.Should().Be(50);
    //     creditLimit.Version.Should().BeGreaterThan(0);
    //     
    //     // Verify subscription credits
    //     var subscription = await context.Subscriptions
    //         .FirstOrDefaultAsync(s => s.UserId == userId);
    //     subscription!.Credits.Should().Be(150); // 200 - 50
    // }

    [Fact]
    public async Task RaceCondition_Should_NotAllowNegativeCredits()
    {
        // Arrange
        await using var context = CreateTestContext();
        
        var userId = Guid.NewGuid();
        await SeedUserWithCredits(context, userId, 20); // Limited credits

        // Act - Try to consume more than available concurrently
        var tasks = new List<Task>();
        var successCount = 0;
        var failureCount = 0;
        
        for (int i = 0; i < 10; i++)
        {
            var sessionId = Guid.NewGuid().ToString();
            var agentId = Guid.NewGuid();
            
            await SeedAgentAndSession(context, userId, agentId, sessionId);
            
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    var connection = await CreateSignalRConnection(sessionId, userId);
                    var messages = new List<string>();
                    connection.On<string, string>("ReceiveMessage", (user, message) =>
                    {
                        messages.Add(message);
                    });
                    
                    await connection.InvokeAsync("SendMessage", sessionId, "Concurrent test", false);
                    await Task.Delay(500);
                    
                    if (messages.Any(m => m.Contains("Insufficient")))
                    {
                        Interlocked.Increment(ref failureCount);
                    }
                    else
                    {
                        Interlocked.Increment(ref successCount);
                    }
                    
                    await connection.DisposeAsync();
                }
                catch
                {
                    Interlocked.Increment(ref failureCount);
                }
            }));
        }
        
        await Task.WhenAll(tasks);

        // Assert
        // Only 4 messages should succeed (20 credits / 5 per message)
        successCount.Should().Be(4);
        failureCount.Should().Be(6);
        
        // Credits should never go negative
        using (var assertScope = Factory.Services.CreateScope())
        {
            var appContext = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var subscription = await appContext.Subscriptions
                .FirstOrDefaultAsync(s => s.UserId == userId);
            subscription!.Credits.Should().Be(0);
            subscription.Credits.Should().BeGreaterThanOrEqualTo(0);
        }
    }

    // TODO: Enable this test when CostConfigurationEntity and its DbSet are added to ApplicationDbContext
    // [Fact]
    // public async Task ConcurrentCostUpdates_Should_UseLatestValues()
    // {
    //     // Arrange
    //     await using var context = CreateTestContext();
    //     
    //     var userId = Guid.NewGuid();
    //     await SeedUserWithCredits(context, userId, 500);
    //     
    //     // Create initial cost configuration
    //     var costConfig = new CostConfigurationEntity
    //     {
    //         Id = Guid.NewGuid(),
    //         CostType = "message_fixed",
    //         CreditCost = 5,
    //         IsActive = true
    //     };
    //     await context.CostConfigurations.AddAsync(costConfig);
    //     await context.SaveChangesAsync();
    // 
    //     // Act - Update cost while messages are being sent
    //     var messageTasks = new List<Task>();
    //     var updateTask = Task.Run(async () =>
    //     {
    //         await Task.Delay(500); // Wait for some messages to start
    //         
    //         // Update cost to 10 credits
    //         costConfig.IsActive = false;
    //         await context.SaveChangesAsync();
    //         
    //         await context.CostConfigurations.AddAsync(new CostConfigurationEntity
    //         {
    //             Id = Guid.NewGuid(),
    //             CostType = "message_fixed",
    //             CreditCost = 10,
    //             IsActive = true,
    //             Priority = 1
    //         });
    //         await context.SaveChangesAsync();
    //     });
    //     
    //     // Send messages over 2 seconds
    //     for (int i = 0; i < 10; i++)
    //     {
    //         var sessionId = Guid.NewGuid().ToString();
    //         var agentId = Guid.NewGuid();
    //         
    //         await SeedAgentAndSession(context, userId, agentId, sessionId);
    //         
    //         messageTasks.Add(Task.Run(async () =>
    //         {
    //             await Task.Delay(i * 200); // Stagger messages
    //             var connection = await CreateSignalRConnection(sessionId, userId);
    //             await connection.InvokeAsync("SendMessage", sessionId, $"Message {i}", false);
    //             await connection.DisposeAsync();
    //         }));
    //     }
    //     
    //     messageTasks.Add(updateTask);
    //     await Task.WhenAll(messageTasks);
    //     await Task.Delay(1000);
    // 
    //     // Assert
    //     var consumptions = await context.CreditConsumptions
    //         .Where(c => c.UserId == userId)
    //         .OrderBy(c => c.ConsumedAt)
    //         .ToListAsync();
    //     
    //     // Some messages should have used 5 credits, later ones 10 credits
    //     var lowCostMessages = consumptions.Where(c => c.CreditsConsumed == 5).Count();
    //     var highCostMessages = consumptions.Where(c => c.CreditsConsumed == 10).Count();
    //     
    //     lowCostMessages.Should().BeGreaterThan(0);
    //     highCostMessages.Should().BeGreaterThan(0);
    //     (lowCostMessages + highCostMessages).Should().Be(10);
    // }

    private async Task SeedUserWithCredits(ApplicationDbContext context, Guid userId, int credits)
    {
        var user = new UserEntity
        {
            Id = userId,
            Email = $"user_{userId}@example.com",
            Name = $"User {userId}"
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
            SubscriptionPlanId = plan.Id,
            Version = 0
        };
        
        await context.Users.AddAsync(user);
        await context.SubscriptionPlans.AddAsync(plan);
        await context.Subscriptions.AddAsync(subscription);
        await context.SaveChangesAsync();
    }

    private async Task SeedAgentAndSession(ApplicationDbContext context, Guid userId, Guid agentId, string sessionId)
    {
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
        
        await context.Agents.AddAsync(agent);
        await context.ChatSessions.AddAsync(chatSession);
        await context.SaveChangesAsync();
        
        // Also add to mock repository
        using var scope = Factory.Services.CreateScope();
        var mockRepo = scope.ServiceProvider.GetRequiredService<IChatSessionRepository>() as MockChatSessionRepository;
        mockRepo?.AddSession(chatSession);
    }

    private async Task<HttpClient> CreateAuthenticatedClient(Guid userId)
    {
        // Use the same Factory instance to share the database
        var client = Factory.CreateClient();
        
        client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await GenerateTestToken(userId));
        
        return client;
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

    private async Task SetAuthHeader(Guid userId)
    {
        Client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await GenerateTestToken(userId));
    }

    private class CreditValidationResponse
    {
        public bool IsValid { get; set; }
        public int RequiredCredits { get; set; }
        public int AvailableCredits { get; set; }
        public string? ErrorMessage { get; set; }
    }
}