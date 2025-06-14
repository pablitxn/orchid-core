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

public class MarketplacePurchaseTests : CreditSystemTestBase
{
    public MarketplacePurchaseTests(WebApplicationFactory<Program> factory) : base(factory)
    {
    }

    [Fact]
    public async Task PurchasePlugin_Should_DeductCreditsAndGrantAccess()
    {
        // Arrange
        await using var context = CreateTestContext();
        
        var userId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        
        await SeedUserWithCredits(context, userId, 100);
        
        var plugin = new PluginEntity
        {
            Id = pluginId,
            Name = "Analytics Plugin",
            SystemName = "AnalyticsPlugin",
            Description = "Advanced analytics features",
            IsActive = true,
            PriceCredits = 50,
            IsSubscriptionBased = false
        };
        await context.Plugins.AddAsync(plugin);
        
        // Add cost configuration
        await context.ActionCosts.AddAsync(new ActionCostEntity
        {
            Id = Guid.NewGuid(),
            ActionName = $"plugin_purchase_{pluginId}",
            Credits = 50
        });
        
        await context.SaveChangesAsync();
        
        await SetAuthHeader(userId);

        // Act
        var response = await Client.PostAsync($"/api/marketplace/plugins/{pluginId}/purchase", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Check credits were deducted
        var subscription = await context.Subscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId);
        subscription!.Credits.Should().Be(50); // 100 - 50
        
        // Check user owns the plugin
        var userPlugin = await context.UserPlugins
            .FirstOrDefaultAsync(up => up.UserId == userId && up.PluginId == pluginId);
        userPlugin.Should().NotBeNull();
        userPlugin!.IsActive.Should().BeTrue();
        userPlugin.PurchasedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        
        // Check credit consumption was tracked
        var consumption = await context.CreditConsumptions
            .FirstOrDefaultAsync(c => c.UserId == userId && c.ResourceId == pluginId);
        consumption.Should().NotBeNull();
        consumption!.ConsumptionType.Should().Be("plugin");
        consumption.CreditsConsumed.Should().Be(50);
        consumption.BalanceAfter.Should().Be(50);
    }

    [Fact]
    public async Task PurchasePlugin_Should_Fail_WithInsufficientCredits()
    {
        // Arrange
        await using var context = CreateTestContext();
        
        var userId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        
        await SeedUserWithCredits(context, userId, 20); // Only 20 credits
        
        var plugin = new PluginEntity
        {
            Id = pluginId,
            Name = "Premium Plugin",
            SystemName = "PremiumPlugin",
            IsActive = true,
            PriceCredits = 50
        };
        await context.Plugins.AddAsync(plugin);
        await context.SaveChangesAsync();
        
        await SetAuthHeader(userId);

        // Act
        var response = await Client.PostAsync($"/api/marketplace/plugins/{pluginId}/purchase", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var error = await response.Content.ReadAsStringAsync();
        error.Should().Contain("Insufficient credits");
        
        // Verify no plugin ownership was created
        var userPlugin = await context.UserPlugins
            .FirstOrDefaultAsync(up => up.UserId == userId && up.PluginId == pluginId);
        userPlugin.Should().BeNull();
        
        // Verify credits weren't deducted
        var subscription = await context.Subscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId);
        subscription!.Credits.Should().Be(20);
    }

    [Fact]
    public async Task PurchasePlugin_Should_ReactivateExistingOwnership()
    {
        // Arrange
        await using var context = CreateTestContext();
        
        var userId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        
        await SeedUserWithCredits(context, userId, 100);
        
        var plugin = new PluginEntity
        {
            Id = pluginId,
            Name = "Reactivatable Plugin",
            SystemName = "ReactivatablePlugin",
            IsActive = true,
            PriceCredits = 30
        };
        
        // User previously owned but deactivated
        var existingOwnership = new UserPluginEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PluginId = pluginId,
            PurchasedAt = DateTime.UtcNow.AddDays(-30),
            IsActive = false
        };
        
        await context.Plugins.AddAsync(plugin);
        await context.UserPlugins.AddAsync(existingOwnership);
        await context.SaveChangesAsync();
        
        await SetAuthHeader(userId);

        // Act
        var response = await Client.PostAsync($"/api/marketplace/plugins/{pluginId}/purchase", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        await context.Entry(existingOwnership).ReloadAsync();
        existingOwnership.IsActive.Should().BeTrue();
        existingOwnership.PurchasedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task PurchaseWorkflow_Should_DeductCreditsAndGrantAccess()
    {
        // Arrange
        await using var context = CreateTestContext();
        
        var userId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        
        await SeedUserWithCredits(context, userId, 200);
        await SeedUser(context, authorId); // Workflow author
        
        var workflow = new WorkflowEntity
        {
            Id = workflowId,
            Name = "Data Processing Workflow",
            Description = "Automated data processing pipeline",
            PriceCredits = 100,
            CreatedAt = DateTime.UtcNow
        };
        await context.Workflows.AddAsync(workflow);
        await context.SaveChangesAsync();
        
        await SetAuthHeader(userId);

        // Act
        var response = await Client.PostAsync($"/api/marketplace/workflows/{workflowId}/purchase", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Check credits were deducted
        var subscription = await context.Subscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId);
        subscription!.Credits.Should().Be(100); // 200 - 100
        
        // Check user owns the workflow
        var userWorkflow = await context.UserWorkflows
            .FirstOrDefaultAsync(uw => uw.UserId == userId && uw.WorkflowId == workflowId);
        userWorkflow.Should().NotBeNull();
        userWorkflow!.PurchasedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        
        // Check credit consumption was tracked
        var consumption = await context.CreditConsumptions
            .FirstOrDefaultAsync(c => c.UserId == userId && c.ResourceId == workflowId);
        consumption.Should().NotBeNull();
        consumption!.ConsumptionType.Should().Be("workflow");
        consumption.CreditsConsumed.Should().Be(100);
    }

    [Fact]
    public async Task PurchaseWorkflow_Should_Fail_IfAlreadyOwned()
    {
        // Arrange
        await using var context = CreateTestContext();
        
        var userId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        
        await SeedUserWithCredits(context, userId, 100);
        
        var workflow = new WorkflowEntity
        {
            Id = workflowId,
            Name = "Already Owned Workflow",
            PriceCredits = 50
        };
        
        var existingOwnership = new UserWorkflowEntity
        {
            UserId = userId,
            WorkflowId = workflowId,
            PurchasedAt = DateTime.UtcNow.AddDays(-5)
        };
        
        await context.Workflows.AddAsync(workflow);
        await context.UserWorkflows.AddAsync(existingOwnership);
        await context.SaveChangesAsync();
        
        await SetAuthHeader(userId);

        // Act
        var response = await Client.PostAsync($"/api/marketplace/workflows/{workflowId}/purchase", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var error = await response.Content.ReadAsStringAsync();
        error.Should().Contain("already purchased");
        
        // Verify credits weren't deducted
        var subscription = await context.Subscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId);
        subscription!.Credits.Should().Be(100);
    }

    [Fact]
    public async Task GetUserMarketplaceItems_Should_ReturnOwnedItems()
    {
        // Arrange
        await using var context = CreateTestContext();
        
        var userId = Guid.NewGuid();
        await SeedUserWithCredits(context, userId, 0);
        
        // Add owned plugins
        var plugin1 = new PluginEntity
        {
            Id = Guid.NewGuid(),
            Name = "Plugin 1",
            SystemName = "Plugin1",
            IsActive = true
        };
        
        var plugin2 = new PluginEntity
        {
            Id = Guid.NewGuid(),
            Name = "Plugin 2",
            SystemName = "Plugin2",
            IsActive = true
        };
        
        await context.Plugins.AddRangeAsync(plugin1, plugin2);
        
        await context.UserPlugins.AddRangeAsync(
            new UserPluginEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PluginId = plugin1.Id,
                PurchasedAt = DateTime.UtcNow.AddDays(-10),
                IsActive = true
            },
            new UserPluginEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PluginId = plugin2.Id,
                PurchasedAt = DateTime.UtcNow.AddDays(-5),
                IsActive = true
            }
        );
        
        // Add owned workflow
        var workflow = new WorkflowEntity
        {
            Id = Guid.NewGuid(),
            Name = "My Workflow"
        };
        
        await context.Workflows.AddAsync(workflow);
        
        await context.UserWorkflows.AddAsync(new UserWorkflowEntity
        {
            UserId = userId,
            WorkflowId = workflow.Id,
            PurchasedAt = DateTime.UtcNow.AddDays(-3)
        });
        
        await context.SaveChangesAsync();
        
        await SetAuthHeader(userId);

        // Act
        var response = await Client.GetAsync("/api/marketplace/my-items");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<UserMarketplaceItems>();
        result.Should().NotBeNull();
        result!.Plugins.Should().HaveCount(2);
        result.Workflows.Should().HaveCount(1);
        
        result.Plugins.Should().Contain(p => p.Name == "Plugin 1");
        result.Plugins.Should().Contain(p => p.Name == "Plugin 2");
        result.Workflows.Should().Contain(w => w.Name == "My Workflow");
    }

    [Fact]
    public async Task ValidatePluginPurchase_Should_CheckCreditsBeforePurchase()
    {
        // Arrange
        await using var context = CreateTestContext();
        
        var userId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        
        await SeedUserWithCredits(context, userId, 30);
        
        var plugin = new PluginEntity
        {
            Id = pluginId,
            Name = "Validation Test Plugin",
            SystemName = "ValidationPlugin",
            IsActive = true,
            PriceCredits = 50
        };
        
        await context.Plugins.AddAsync(plugin);
        await context.ActionCosts.AddAsync(new ActionCostEntity
        {
            Id = Guid.NewGuid(),
            ActionName = $"plugin_purchase_{pluginId}",
            Credits = 50
        });
        await context.SaveChangesAsync();
        
        await SetAuthHeader(userId);

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/credit-validation/plugin/{pluginId}/validate", 
            new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<CreditValidationResponse>();
        result!.IsValid.Should().BeFalse();
        result.RequiredCredits.Should().Be(50);
        result.AvailableCredits.Should().Be(30);
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SubscriptionBasedPlugin_Should_RequireActiveSubscription()
    {
        // Arrange
        await using var context = CreateTestContext();
        
        var userId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        
        // Create user with expired subscription
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
            Credits = 100,
            SubscriptionPlanId = plan.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(-1) // Expired
        };
        
        await context.SubscriptionPlans.AddAsync(plan);
        await context.Subscriptions.AddAsync(subscription);
        
        var plugin = new PluginEntity
        {
            Id = pluginId,
            Name = "Subscription Plugin",
            SystemName = "SubscriptionPlugin",
            IsActive = true,
            IsSubscriptionBased = true,
            PriceCredits = 0 // Free with subscription
        };
        
        await context.Plugins.AddAsync(plugin);
        await context.SaveChangesAsync();
        
        await SetAuthHeader(userId);

        // Act
        var response = await Client.PostAsync($"/api/marketplace/plugins/{pluginId}/purchase", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var error = await response.Content.ReadAsStringAsync();
        error.Should().Contain("subscription has expired");
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

    private async Task SetAuthHeader(Guid userId)
    {
        Client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await GenerateTestToken(userId));
    }

    private class UserMarketplaceItems
    {
        public List<PluginDto> Plugins { get; set; } = new();
        public List<WorkflowDto> Workflows { get; set; } = new();
    }

    private class PluginDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime PurchasedAt { get; set; }
    }

    private class WorkflowDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime PurchasedAt { get; set; }
    }

    private class CreditValidationResponse
    {
        public bool IsValid { get; set; }
        public int RequiredCredits { get; set; }
        public int AvailableCredits { get; set; }
        public string? ErrorMessage { get; set; }
    }
}