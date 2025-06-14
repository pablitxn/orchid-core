using System.Net;
using System.Net.Http.Json;
using System.Text;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CreditSystem.IntegrationTests;

public class SubscriptionPlanTests : CreditSystemTestBase
{
    public SubscriptionPlanTests(WebApplicationFactory<Program> factory) : base(factory)
    {
    }

    [Fact]
    public async Task PurchaseMonthlyPlan_Should_AddCreditsAndSetExpiration()
    {
        // Arrange
        await using var context = CreateTestContext();
        
        var userId = Guid.NewGuid();
        await SeedUser(context, userId);
        await SeedSubscriptionPlans(context);
        
        await SetAuthHeader(userId);

        // Act
        var response = await Client.PostAsJsonAsync("/api/subscription/purchase", new
        {
            userId = userId,
            plan = "Monthly100"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var subscription = await context.Subscriptions
            .Include(s => s.SubscriptionPlan)
            .FirstOrDefaultAsync(s => s.UserId == userId);
        
        subscription.Should().NotBeNull();
        subscription!.Credits.Should().Be(100);
        subscription.ExpiresAt.Should().NotBeNull();
        subscription.ExpiresAt!.Value.Should().BeCloseTo(DateTime.UtcNow.AddMonths(1), TimeSpan.FromMinutes(1));
        subscription.SubscriptionPlan.Should().NotBeNull();
        subscription.SubscriptionPlan!.PlanEnum.Should().Be(SubscriptionPlanEnum.Monthly100);
    }

    [Theory]
    [InlineData(SubscriptionPlanEnum.Package5, 5)]
    [InlineData(SubscriptionPlanEnum.Package10, 10)]
    [InlineData(SubscriptionPlanEnum.Package25, 25)]
    public async Task PurchaseCreditPackage_Should_AddCreditsWithoutExpiration(SubscriptionPlanEnum planType, int expectedCredits)
    {
        // Arrange
        await using var context = CreateTestContext();
        
        var userId = Guid.NewGuid();
        await SeedUser(context, userId);
        await SeedSubscriptionPlans(context);
        
        await SetAuthHeader(userId);

        // Act
        var response = await Client.PostAsJsonAsync("/api/subscription/purchase", new
        {
            userId = userId,
            plan = planType.ToString()
        });

        // Assert
        if (response.StatusCode != HttpStatusCode.OK)
        {
            var content = await response.Content.ReadAsStringAsync();
            throw new Exception($"Unexpected response: {response.StatusCode}, Content: {content}");
        }
        
        var subscription = await context.Subscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId);
        
        subscription.Should().NotBeNull();
        subscription!.Credits.Should().Be(expectedCredits);
        subscription.ExpiresAt.Should().BeNull(); // Credit packages don't expire
    }

    [Fact]
    public async Task PurchaseAdditionalCredits_Should_AddToExisting()
    {
        // Arrange
        await using var context = CreateTestContext();
        
        var userId = Guid.NewGuid();
        await SeedUser(context, userId);
        await SeedSubscriptionPlans(context);
        
        // Create existing subscription with some credits
        var existingSubscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Credits = 30,
            SubscriptionPlanId = (await context.SubscriptionPlans.FirstAsync(p => p.PlanEnum == SubscriptionPlanEnum.Package10)).Id
        };
        await context.Subscriptions.AddAsync(existingSubscription);
        await context.SaveChangesAsync();
        
        await SetAuthHeader(userId);

        // Act
        var response = await Client.PostAsJsonAsync("/api/subscription/purchase", new
        {
            userId = userId,
            plan = "Package25"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        await context.Entry(existingSubscription).ReloadAsync();
        existingSubscription.Credits.Should().Be(55); // 30 + 25
    }

    [Fact]
    public async Task UpgradeToUnlimitedPlan_Should_SetUnlimitedCredits()
    {
        // Arrange
        await using var context = CreateTestContext();
        
        var userId = Guid.NewGuid();
        await SeedUser(context, userId);
        await SeedSubscriptionPlans(context);
        
        // Create existing limited subscription
        var limitedPlan = await context.SubscriptionPlans.FirstAsync(p => p.PlanEnum == SubscriptionPlanEnum.Monthly100);
        var existingSubscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Credits = 50,
            SubscriptionPlanId = limitedPlan.Id,
            SubscriptionPlan = limitedPlan
        };
        await context.Subscriptions.AddAsync(existingSubscription);
        await context.SaveChangesAsync();
        
        await SetAuthHeader(userId);

        // Act
        var response = await Client.PostAsJsonAsync("/api/subscription/purchase", new
        {
            userId = userId,
            plan = "Unlimited"
        });

        // Assert
        if (response.StatusCode != HttpStatusCode.OK)
        {
            var content = await response.Content.ReadAsStringAsync();
            throw new Exception($"Unexpected response: {response.StatusCode}, Content: {content}");
        }
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        await context.Entry(existingSubscription).ReloadAsync();
        await context.Entry(existingSubscription).Reference(s => s.SubscriptionPlan).LoadAsync();
        
        existingSubscription.SubscriptionPlan!.PlanEnum.Should().Be(SubscriptionPlanEnum.Unlimited);
        existingSubscription.HasUnlimitedCredits().Should().BeTrue();
    }

    [Fact]
    public async Task GetSubscriptionStatus_Should_ReturnCorrectInfo()
    {
        // Arrange
        await using var context = CreateTestContext();
        
        var userId = Guid.NewGuid();
        await SeedUser(context, userId);
        await SeedSubscriptionPlans(context);
        
        var plan = await context.SubscriptionPlans.FirstAsync(p => p.PlanEnum == SubscriptionPlanEnum.Monthly100);
        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Credits = 75,
            SubscriptionPlanId = plan.Id,
            SubscriptionPlan = plan,
            ExpiresAt = DateTime.UtcNow.AddDays(15),
            AutoRenew = true
        };
        await context.Subscriptions.AddAsync(subscription);
        await context.SaveChangesAsync();
        
        await SetAuthHeader(userId);

        // Act
        var response = await Client.GetAsync("/api/subscription/status");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<dynamic>();
        result.Should().NotBeNull();
        ((int)result!.credits).Should().Be(75);
        ((DateTime?)result.expiresAt).Should().BeCloseTo(DateTime.UtcNow.AddDays(15), TimeSpan.FromMinutes(1));
        ((bool)result.autoRenew).Should().BeTrue();
        ((bool)result.isActive).Should().BeTrue();
    }

    [Fact]
    public async Task SubscriptionAutoRenewal_Should_BeConfigurable()
    {
        // Arrange
        await using var context = CreateTestContext();
        
        var userId = Guid.NewGuid();
        await SeedUser(context, userId);
        
        var plan = await SeedSubscriptionPlans(context);
        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Credits = 100,
            SubscriptionPlanId = plan.Id,
            AutoRenew = true
        };
        await context.Subscriptions.AddAsync(subscription);
        await context.SaveChangesAsync();
        
        await SetAuthHeader(userId);

        // Act - Disable auto-renewal
        var response = await Client.PutAsJsonAsync("/api/subscription/auto-renew", new
        {
            userId = userId,
            autoRenew = false
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        await context.Entry(subscription).ReloadAsync();
        subscription.AutoRenew.Should().BeFalse();
    }

    [Fact]
    public async Task ExpiredSubscription_Should_BlockCreditConsumption()
    {
        // Arrange
        await using var context = CreateTestContext();
        
        var userId = Guid.NewGuid();
        await SeedUser(context, userId);
        
        var plan = await SeedSubscriptionPlans(context);
        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Credits = 50,
            SubscriptionPlanId = plan.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(-1) // Expired yesterday
        };
        await context.Subscriptions.AddAsync(subscription);
        await context.SaveChangesAsync();
        
        await SetAuthHeader(userId);

        // Act
        var response = await Client.PostAsJsonAsync("/api/credit-validation/operation/validate", new
        {
            requiredCredits = 10,
            operationType = "test_operation"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<CreditValidationResponse>();
        result.Should().NotBeNull();
        result!.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetSubscriptionHistory_Should_ReturnAllTransactions()
    {
        // Arrange
        await using var context = CreateTestContext();
        
        var userId = Guid.NewGuid();
        await SeedUser(context, userId);
        
        var plan = await SeedSubscriptionPlans(context);
        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Credits = 100,
            SubscriptionPlanId = plan.Id
        };
        await context.Subscriptions.AddAsync(subscription);
        
        // Add credit consumption history
        await context.CreditConsumptions.AddRangeAsync(
            new CreditConsumptionEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ConsumptionType = "message",
                ResourceId = Guid.NewGuid(),
                ResourceName = "Chat Message",
                CreditsConsumed = 5,
                BalanceAfter = 95,
                ConsumedAt = DateTime.UtcNow.AddHours(-2)
            },
            new CreditConsumptionEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ConsumptionType = "plugin",
                ResourceId = Guid.NewGuid(),
                ResourceName = "Test Plugin",
                CreditsConsumed = 50,
                BalanceAfter = 45,
                ConsumedAt = DateTime.UtcNow.AddHours(-1)
            }
        );
        await context.SaveChangesAsync();
        
        await SetAuthHeader(userId);

        // Act
        var response = await Client.GetAsync("/api/credithistory/consumption");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<dynamic>();
        result.Should().NotBeNull();
        var consumptions = result!.consumptions as IEnumerable<dynamic>;
        consumptions.Should().NotBeNull();
        
        var consumptionList = consumptions!.ToList();
        consumptionList.Should().HaveCount(2);
        consumptionList[0].consumptionType.ToString().Should().Be("plugin");
        ((int)consumptionList[0].creditsConsumed).Should().Be(50);
    }

    private async Task SeedUser(ApplicationDbContext context, Guid userId)
    {
        var user = new UserEntity
        {
            Id = userId,
            Email = "test@example.com",
            Name = "Test User"
        };
        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();
    }

    private async Task<SubscriptionPlanEntity> SeedSubscriptionPlans(ApplicationDbContext context)
    {
        var plans = new[]
        {
            new SubscriptionPlanEntity
            {
                Id = Guid.NewGuid(),
                PlanEnum = SubscriptionPlanEnum.Monthly100,
                Price = 29.99m,
                Credits = 100
            },
            new SubscriptionPlanEntity
            {
                Id = Guid.NewGuid(),
                PlanEnum = SubscriptionPlanEnum.Package5,
                Price = 4.99m,
                Credits = 5
            },
            new SubscriptionPlanEntity
            {
                Id = Guid.NewGuid(),
                PlanEnum = SubscriptionPlanEnum.Package10,
                Price = 9.99m,
                Credits = 10
            },
            new SubscriptionPlanEntity
            {
                Id = Guid.NewGuid(),
                PlanEnum = SubscriptionPlanEnum.Package25,
                Price = 19.99m,
                Credits = 25
            },
            new SubscriptionPlanEntity
            {
                Id = Guid.NewGuid(),
                PlanEnum = SubscriptionPlanEnum.Unlimited,
                Price = 99.99m,
                Credits = 0
            }
        };
        
        await context.SubscriptionPlans.AddRangeAsync(plans);
        await context.SaveChangesAsync();
        
        return plans[0]; // Return Monthly100 as default
    }

    private async Task SetAuthHeader(Guid userId)
    {
        Client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await GenerateTestToken(userId));
    }

    private class SubscriptionStatusResponse
    {
        public Guid UserId { get; set; }
        public int Credits { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool AutoRenew { get; set; }
        public Guid? SubscriptionPlanId { get; set; }
        public bool IsActive { get; set; }
    }

    private class CreditValidationResponse
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
    }

    private class CreditHistoryItem
    {
        public string ConsumptionType { get; set; } = string.Empty;
        public int CreditsConsumed { get; set; }
        public DateTime ConsumedAt { get; set; }
    }
}