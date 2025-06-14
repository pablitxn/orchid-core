// TODO: Enable this test class when UserCreditLimits DbSet is added to ApplicationDbContext
#if false

using System.Net;
using System.Net.Http.Json;
using System.Text;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CreditSystem.IntegrationTests;

public class CreditLimitTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public CreditLimitTests(WebApplicationFactory<Program> factory)
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
    public async Task DailyLimit_Should_PreventExcessiveConsumption()
    {
        // Arrange
        await using var context = CreateTestContext();
        
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid().ToString();
        var agentId = Guid.NewGuid();
        
        await SeedUserWithCreditsAndLimits(context, userId, agentId, 100, dailyLimit: 20);
        
        var connection = await CreateSignalRConnection(sessionId, userId);
        var messagesReceived = new List<string>();
        connection.On<string, string>("ReceiveMessage", (user, message) =>
        {
            messagesReceived.Add(message);
        });

        // Act - Send messages to exceed daily limit
        for (int i = 0; i < 5; i++) // 5 messages × 5 credits = 25 credits (exceeds 20 daily limit)
        {
            await connection.InvokeAsync("SendMessage", sessionId, $"Message {i}", false);
            await Task.Delay(500);
        }

        // Assert
        // First 4 messages should succeed (20 credits)
        var subscription = await context.Subscriptions.FirstOrDefaultAsync(s => s.UserId == userId);
        subscription!.Credits.Should().Be(80); // 100 - 20
        
        // Check credit limit was enforced
        var creditLimit = await context.UserCreditLimits
            .FirstOrDefaultAsync(l => l.UserId == userId && l.LimitType == "daily");
        creditLimit!.ConsumedCredits.Should().Be(20);
        
        // Last message should have failed
        messagesReceived.Should().Contain(m => m.Contains("limit exceeded"));
        
        await connection.DisposeAsync();
    }

    [Fact]
    public async Task WeeklyLimit_Should_TrackAcrossMultipleDays()
    {
        // Arrange
        await using var context = CreateTestContext();
        
        var userId = Guid.NewGuid();
        await SeedUserWithCredits(context, userId, 500);
        
        // Set weekly limit
        var weeklyLimit = new UserCreditLimitEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            LimitType = "weekly",
            MaxCredits = 100,
            ConsumedCredits = 85, // Already consumed 85 this week
            PeriodStartDate = DateTime.UtcNow.AddDays(-3),
            PeriodEndDate = DateTime.UtcNow.AddDays(4),
            IsActive = true
        };
        await context.UserCreditLimits.AddAsync(weeklyLimit);
        await context.SaveChangesAsync();
        
        SetAuthHeader(userId);

        // Act - Try to validate operation that would exceed weekly limit
        var response = await _client.PostAsJsonAsync("/api/credit-validation/operation/validate", new
        {
            requiredCredits = 20,
            operationType = "test_operation"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<CreditValidationResponse>();
        result!.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("weekly limit");
    }

    [Fact]
    public async Task MonthlyLimit_Should_ResetAfterPeriodExpires()
    {
        // Arrange
        await using var context = CreateTestContext();
        
        var userId = Guid.NewGuid();
        await SeedUserWithCredits(context, userId, 1000);
        
        // Set expired monthly limit
        var monthlyLimit = new UserCreditLimitEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            LimitType = "monthly",
            MaxCredits = 500,
            ConsumedCredits = 500, // Fully consumed
            PeriodStartDate = DateTime.UtcNow.AddDays(-35),
            PeriodEndDate = DateTime.UtcNow.AddDays(-5), // Expired 5 days ago
            IsActive = true
        };
        await context.UserCreditLimits.AddAsync(monthlyLimit);
        await context.SaveChangesAsync();
        
        // Trigger the reset function (normally done by scheduled job)
        await context.Database.ExecuteSqlRawAsync("SELECT reset_expired_credit_limits()");
        
        // Reload the limit
        await context.Entry(monthlyLimit).ReloadAsync();

        // Assert
        monthlyLimit.ConsumedCredits.Should().Be(0);
        monthlyLimit.PeriodStartDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        monthlyLimit.PeriodEndDate.Should().BeCloseTo(DateTime.UtcNow.AddMonths(1), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task ResourceSpecificLimits_Should_ApplyToCorrectOperations()
    {
        // Arrange
        await using var context = CreateTestContext();
        
        var userId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        
        await SeedUserWithCredits(context, userId, 200);
        
        // Set plugin-specific daily limit
        var pluginLimit = new UserCreditLimitEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            LimitType = "daily",
            MaxCredits = 30,
            ConsumedCredits = 0,
            PeriodStartDate = DateTime.UtcNow.Date,
            PeriodEndDate = DateTime.UtcNow.Date.AddDays(1),
            IsActive = true,
            ResourceType = "plugin"
        };
        
        // Set general daily limit
        var generalLimit = new UserCreditLimitEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            LimitType = "daily",
            MaxCredits = 100,
            ConsumedCredits = 0,
            PeriodStartDate = DateTime.UtcNow.Date,
            PeriodEndDate = DateTime.UtcNow.Date.AddDays(1),
            IsActive = true,
            ResourceType = null // Applies to all
        };
        
        await context.UserCreditLimits.AddRangeAsync(pluginLimit, generalLimit);
        
        // Add expensive plugin
        var plugin = new PluginEntity
        {
            Id = pluginId,
            Name = "Resource Limited Plugin",
            SystemName = "ResourceLimitedPlugin",
            IsActive = true,
            PriceCredits = 35
        };
        await context.Plugins.AddAsync(plugin);
        await context.SaveChangesAsync();
        
        SetAuthHeader(userId);

        // Act - Try to purchase plugin that exceeds plugin-specific limit
        var response = await _client.PostAsJsonAsync($"/api/credit-validation/plugin/{pluginId}/validate", new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<CreditValidationResponse>();
        result!.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("limit");
    }

    [Fact]
    public async Task MultipleLimits_Should_EnforceAllConstraints()
    {
        // Arrange
        await using var context = CreateTestContext();
        
        var userId = Guid.NewGuid();
        await SeedUserWithCredits(context, userId, 1000);
        
        // Set multiple overlapping limits
        var limits = new[]
        {
            new UserCreditLimitEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                LimitType = "daily",
                MaxCredits = 50,
                ConsumedCredits = 45,
                PeriodStartDate = DateTime.UtcNow.Date,
                PeriodEndDate = DateTime.UtcNow.Date.AddDays(1),
                IsActive = true
            },
            new UserCreditLimitEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                LimitType = "weekly",
                MaxCredits = 200,
                ConsumedCredits = 180,
                PeriodStartDate = DateTime.UtcNow.AddDays(-3),
                PeriodEndDate = DateTime.UtcNow.AddDays(4),
                IsActive = true
            },
            new UserCreditLimitEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                LimitType = "monthly",
                MaxCredits = 500,
                ConsumedCredits = 450,
                PeriodStartDate = DateTime.UtcNow.AddDays(-15),
                PeriodEndDate = DateTime.UtcNow.AddDays(15),
                IsActive = true
            }
        };
        
        await context.UserCreditLimits.AddRangeAsync(limits);
        await context.SaveChangesAsync();
        
        SetAuthHeader(userId);

        // Act - Try operation that violates daily limit but not others
        var response = await _client.PostAsJsonAsync("/api/credit-validation/operation/validate", new
        {
            requiredCredits = 10,
            operationType = "test_operation"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<CreditValidationResponse>();
        result!.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("daily limit"); // Should fail on daily, not weekly/monthly
    }

    [Fact]
    public async Task UnlimitedPlan_Should_BypassAllLimits()
    {
        // Arrange
        await using var context = CreateTestContext();
        
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid().ToString();
        var agentId = Guid.NewGuid();
        
        // Create user with unlimited plan
        await SeedUser(context, userId);
        
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
        
        await context.SubscriptionPlans.AddAsync(unlimitedPlan);
        await context.Subscriptions.AddAsync(subscription);
        
        // Add restrictive limits
        var limit = new UserCreditLimitEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            LimitType = "daily",
            MaxCredits = 10,
            ConsumedCredits = 0,
            PeriodStartDate = DateTime.UtcNow.Date,
            PeriodEndDate = DateTime.UtcNow.Date.AddDays(1),
            IsActive = true
        };
        await context.UserCreditLimits.AddAsync(limit);
        
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
        
        var connection = await CreateSignalRConnection(sessionId, userId);

        // Act - Send many messages (would exceed limit for normal users)
        for (int i = 0; i < 5; i++)
        {
            await connection.InvokeAsync("SendMessage", sessionId, $"Message {i}", false);
            await Task.Delay(200);
        }

        // Assert
        // All messages should succeed despite limits
        await context.Entry(limit).ReloadAsync();
        limit.ConsumedCredits.Should().Be(0); // Limits not enforced for unlimited plan
        
        await connection.DisposeAsync();
    }

    [Fact]
    public async Task CreditLimitHistory_Should_TrackAllChanges()
    {
        // Arrange
        await using var context = CreateTestContext();
        
        var userId = Guid.NewGuid();
        await SeedUserWithCredits(context, userId, 100);
        
        // Create limit
        var limit = new UserCreditLimitEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            LimitType = "daily",
            MaxCredits = 50,
            ConsumedCredits = 0,
            PeriodStartDate = DateTime.UtcNow.Date,
            PeriodEndDate = DateTime.UtcNow.Date.AddDays(1),
            IsActive = true,
            Version = 0
        };
        await context.UserCreditLimits.AddAsync(limit);
        await context.SaveChangesAsync();
        
        // Simulate multiple consumptions
        for (int i = 0; i < 3; i++)
        {
            limit.ConsumedCredits += 10;
            limit.Version++;
            await context.SaveChangesAsync();
        }

        // Assert
        limit.ConsumedCredits.Should().Be(30);
        limit.Version.Should().Be(3);
        limit.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    private async Task SeedUserWithCreditsAndLimits(
        ApplicationDbContext context, 
        Guid userId, 
        Guid agentId,
        int credits, 
        int? dailyLimit = null,
        int? weeklyLimit = null,
        int? monthlyLimit = null)
    {
        await SeedUserWithCredits(context, userId, credits);
        
        var limits = new List<UserCreditLimitEntity>();
        
        if (dailyLimit.HasValue)
        {
            limits.Add(new UserCreditLimitEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                LimitType = "daily",
                MaxCredits = dailyLimit.Value,
                ConsumedCredits = 0,
                PeriodStartDate = DateTime.UtcNow.Date,
                PeriodEndDate = DateTime.UtcNow.Date.AddDays(1),
                IsActive = true
            });
        }
        
        if (weeklyLimit.HasValue)
        {
            limits.Add(new UserCreditLimitEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                LimitType = "weekly",
                MaxCredits = weeklyLimit.Value,
                ConsumedCredits = 0,
                PeriodStartDate = DateTime.UtcNow.AddDays(-(int)DateTime.UtcNow.DayOfWeek),
                PeriodEndDate = DateTime.UtcNow.AddDays(7 - (int)DateTime.UtcNow.DayOfWeek),
                IsActive = true
            });
        }
        
        if (monthlyLimit.HasValue)
        {
            limits.Add(new UserCreditLimitEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                LimitType = "monthly",
                MaxCredits = monthlyLimit.Value,
                ConsumedCredits = 0,
                PeriodStartDate = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1),
                PeriodEndDate = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(1),
                IsActive = true
            });
        }
        
        if (limits.Any())
        {
            await context.UserCreditLimits.AddRangeAsync(limits);
        }
        
        // Add agent and chat session
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
            SessionId = Guid.NewGuid().ToString(),
            UserId = userId,
            AgentId = agentId,
            CreatedAt = DateTime.UtcNow
        };
        
        await context.Agents.AddAsync(agent);
        await context.ChatSessions.AddAsync(chatSession);
        await context.SaveChangesAsync();
    }

    private async Task SeedUserWithCredits(ApplicationDbContext context, Guid userId, int credits)
    {
        await SeedUser(context, userId);
        
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
        
        await context.SubscriptionPlans.AddAsync(plan);
        await context.Subscriptions.AddAsync(subscription);
        await context.SaveChangesAsync();
    }

    private async Task SeedUser(ApplicationDbContext context, Guid userId)
    {
        var user = new UserEntity
        {
            Id = userId,
            Email = $"user_{userId}@example.com",
            Name = $"User {userId}"
        };
        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();
    }

    private async Task<HubConnection> CreateSignalRConnection(string sessionId, Guid userId)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl($"http://localhost/chathub?sessionId={sessionId}", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult(GenerateTestToken(userId));
            })
            .Build();
        
        await connection.StartAsync();
        return connection;
    }

    private void SetAuthHeader(Guid userId)
    {
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", GenerateTestToken(userId));
    }

    private string GenerateTestToken(Guid userId)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(userId.ToString()));
    }

    private class CreditValidationResponse
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
#endif
