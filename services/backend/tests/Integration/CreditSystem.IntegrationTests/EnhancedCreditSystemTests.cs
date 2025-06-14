using System;
using System.Linq;
using System.Threading.Tasks;
using Application.Interfaces;
using Application.UseCases.Plugin.ExecutePlugin;
using Application.UseCases.Subscription.ConsumeCredits;
using Application.UseCases.Workflow.PurchaseWorkflow;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CreditSystem.IntegrationTests;

public class EnhancedCreditSystemTests : CreditSystemTestBase
{
    public EnhancedCreditSystemTests(WebApplicationFactory<Program> factory) : base(factory)
    {
    }
    
    [Fact]
    public async Task ExecutePlugin_Should_ConsumeCredits_BasedOnUsageCost()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        
        // Create user with subscription
        await CreateUserWithSubscription(userId, 1000);
        
        using var scope = Factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        
        // Create and configure plugin
        var plugin = new PluginEntity
        {
            Id = pluginId,
            Name = "Test Plugin",
            Description = "Test",
            IsActive = true,
            IsSubscriptionBased = false,
            CreatedAt = DateTime.UtcNow
        };
        await unitOfWork.Plugins.CreateAsync(plugin, CancellationToken.None);
        
        // User owns the plugin
        var userPlugin = new UserPluginEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PluginId = pluginId,
            PurchasedAt = DateTime.UtcNow,
            IsActive = true
        };
        await unitOfWork.UserPlugins.CreateAsync(userPlugin, CancellationToken.None);
        
        // Configure plugin usage cost
        var costConfig = new CostConfigurationEntity
        {
            Id = Guid.NewGuid(),
            CostType = "plugin_usage",
            ResourceId = pluginId,
            CreditCost = 5, // 5 credits per use
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        await unitOfWork.CostConfigurations.CreateAsync(costConfig, CancellationToken.None);
        await unitOfWork.SaveChangesAsync();
        
        // Act
        var command = new ExecutePluginCommand(userId, pluginId, "{\"test\": \"data\"}");
        var result = await mediator.Send(command);
        
        // Assert
        result.Success.Should().BeTrue();
        result.CreditsUsed.Should().Be(5);
        
        var subscription = await unitOfWork.Subscriptions.GetByUserIdAsync(userId);
        subscription!.Credits.Should().Be(995); // 1000 - 5
    }

    [Fact]
    public async Task ExecutePlugin_WithHighQualityParameter_Should_ConsumeMoreCredits()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        
        await CreateUserWithSubscription(userId, 1000);
        
        using var scope = Factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        
        // Setup plugin and ownership
        var plugin = new PluginEntity
        {
            Id = pluginId,
            Name = "Test Plugin",
            Description = "Test",
            IsActive = true,
            IsSubscriptionBased = false,
            CreatedAt = DateTime.UtcNow
        };
        await unitOfWork.Plugins.CreateAsync(plugin, CancellationToken.None);
        
        var userPlugin = new UserPluginEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PluginId = pluginId,
            PurchasedAt = DateTime.UtcNow,
            IsActive = true
        };
        await unitOfWork.UserPlugins.CreateAsync(userPlugin, CancellationToken.None);
        
        // Base cost is 10
        var costConfig = new CostConfigurationEntity
        {
            Id = Guid.NewGuid(),
            CostType = "plugin_usage",
            ResourceId = pluginId,
            CreditCost = 10,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        await unitOfWork.CostConfigurations.CreateAsync(costConfig, CancellationToken.None);
        await unitOfWork.SaveChangesAsync();
        
        // Act - Execute with high quality parameter (should cost 50% more)
        var command = new ExecutePluginCommand(userId, pluginId, "{\"quality\": \"high\"}");
        var result = await mediator.Send(command);
        
        // Assert
        result.Success.Should().BeTrue();
        result.CreditsUsed.Should().Be(15); // 10 * 1.5
        
        var subscription = await unitOfWork.Subscriptions.GetByUserIdAsync(userId);
        subscription!.Credits.Should().Be(985); // 1000 - 15
    }

    [Fact]
    public async Task PurchaseWorkflow_WithCreditLimits_Should_ValidateAndConsume()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        
        await CreateUserWithSubscription(userId, 500);
        
        using var scope = Factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        
        // Create workflow
        var workflow = new WorkflowEntity
        {
            Id = workflowId,
            Name = "Test Workflow",
            Description = "Test",
            PriceCredits = 100,
            CreatedAt = DateTime.UtcNow
        };
        await unitOfWork.Workflows.CreateAsync(workflow, CancellationToken.None);
        
        // Create credit limit
        var creditLimit = new UserCreditLimitEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            LimitType = "daily",
            MaxCredits = 200,
            ConsumedCredits = 50,
            PeriodStartDate = DateTime.UtcNow.Date,
            PeriodEndDate = DateTime.UtcNow.Date.AddDays(1),
            IsActive = true,
            ResourceType = "workflow_purchase"
        };
        await unitOfWork.UserCreditLimits.CreateAsync(creditLimit, CancellationToken.None);
        
        // Configure workflow purchase cost
        var costConfig = new CostConfigurationEntity
        {
            Id = Guid.NewGuid(),
            CostType = "workflow_purchase",
            ResourceId = workflowId,
            CreditCost = 100,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        await unitOfWork.CostConfigurations.CreateAsync(costConfig, CancellationToken.None);
        await unitOfWork.SaveChangesAsync();
        
        // Act
        var command = new PurchaseWorkflowCommand(userId, workflowId);
        var result = await mediator.Send(command);
        
        // Assert
        result.Should().Be(PurchaseResult.Success);
        
        var subscription = await unitOfWork.Subscriptions.GetByUserIdAsync(userId);
        subscription!.Credits.Should().Be(400); // 500 - 100
        
        var updatedLimit = await unitOfWork.UserCreditLimits.GetByIdAsync(creditLimit.Id);
        updatedLimit!.ConsumedCredits.Should().Be(150); // 50 + 100
    }

    [Fact]
    public async Task PurchaseWorkflow_ExceedingCreditLimit_Should_Throw()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        
        await CreateUserWithSubscription(userId, 500);
        
        using var scope = Factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        
        // Create workflow
        var workflow = new WorkflowEntity
        {
            Id = workflowId,
            Name = "Test Workflow",
            Description = "Test",
            PriceCredits = 150,
            CreatedAt = DateTime.UtcNow
        };
        await unitOfWork.Workflows.CreateAsync(workflow, CancellationToken.None);
        
        // Create credit limit that would be exceeded
        var creditLimit = new UserCreditLimitEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            LimitType = "daily",
            MaxCredits = 200,
            ConsumedCredits = 100, // Only 100 credits left in limit
            PeriodStartDate = DateTime.UtcNow.Date,
            PeriodEndDate = DateTime.UtcNow.Date.AddDays(1),
            IsActive = true,
            ResourceType = "workflow_purchase"
        };
        await unitOfWork.UserCreditLimits.CreateAsync(creditLimit, CancellationToken.None);
        
        // Configure workflow purchase cost
        var costConfig = new CostConfigurationEntity
        {
            Id = Guid.NewGuid(),
            CostType = "workflow_purchase",
            ResourceId = workflowId,
            CreditCost = 150,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        await unitOfWork.CostConfigurations.CreateAsync(costConfig, CancellationToken.None);
        await unitOfWork.SaveChangesAsync();
        
        // Act & Assert
        var command = new PurchaseWorkflowCommand(userId, workflowId);
        await Assert.ThrowsAsync<CreditLimitExceededException>(() => mediator.Send(command));
        
        // Verify credits were not consumed
        var subscription = await unitOfWork.Subscriptions.GetByUserIdAsync(userId);
        subscription!.Credits.Should().Be(500);
    }

    [Fact]
    public async Task ConsumeCredits_WithConcurrentRequests_Should_HandleCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        
        await CreateUserWithSubscription(userId, 1000);
        
        using var scope = Factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        
        // Act - Simulate concurrent consumption
        var tasks = new Task[10];
        for (int i = 0; i < 10; i++)
        {
            tasks[i] = Task.Run(async () =>
            {
                using var taskScope = Factory.Services.CreateScope();
                var taskMediator = taskScope.ServiceProvider.GetRequiredService<IMediator>();
                var command = new ConsumeCreditsCommand(userId, 50, "concurrent_test");
                await taskMediator.Send(command);
            });
        }
        
        await Task.WhenAll(tasks);
        
        // Assert
        using var verifyScope = Factory.Services.CreateScope();
        var unitOfWork = verifyScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var subscription = await unitOfWork.Subscriptions.GetByUserIdAsync(userId);
        subscription!.Credits.Should().Be(500); // 1000 - (10 * 50)
    }

    [Fact]
    public async Task UnlimitedSubscription_Should_NotConsumeCredits()
    {
        // Arrange
        var userId = Guid.NewGuid();
        
        // Create user with unlimited subscription
        await CreateUserWithSubscription(userId, 0, SubscriptionPlanEnum.Unlimited);
        
        using var scope = Factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        
        // Act
        var command = new ConsumeCreditsCommand(userId, 1000, "test");
        var result = await mediator.Send(command);
        
        // Assert
        result.Credits.Should().Be(0); // Unlimited plans show 0 credits
        result.HasUnlimitedCredits().Should().BeTrue();
    }

    [Fact]
    public async Task CreditLimit_Should_ResetAfterPeriodExpires()
    {
        // Arrange
        var userId = Guid.NewGuid();
        
        await CreateUserWithSubscription(userId, 1000);
        
        using var scope = Factory.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        
        // Create expired credit limit
        var creditLimit = new UserCreditLimitEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            LimitType = "daily",
            MaxCredits = 100,
            ConsumedCredits = 90,
            PeriodStartDate = DateTime.UtcNow.AddDays(-2),
            PeriodEndDate = DateTime.UtcNow.AddDays(-1), // Expired yesterday
            IsActive = true
        };
        await unitOfWork.UserCreditLimits.CreateAsync(creditLimit, CancellationToken.None);
        await unitOfWork.SaveChangesAsync();
        
        // Act - Check if within limit (should reset)
        var isWithinLimit = creditLimit.IsWithinLimit(50);
        
        // Assert
        isWithinLimit.Should().BeTrue();
        creditLimit.ConsumedCredits.Should().Be(0); // Reset
        creditLimit.PeriodStartDate.Date.Should().Be(DateTime.UtcNow.Date);
        creditLimit.PeriodEndDate.Should().BeAfter(DateTime.UtcNow);
    }
    
    private async Task CreateUserWithSubscription(Guid userId, int credits, SubscriptionPlanEnum plan = SubscriptionPlanEnum.Monthly100)
    {
        await using var context = CreateTestContext();
        
        var user = new UserEntity
        {
            Id = userId,
            Email = $"user_{userId}@example.com",
            Name = $"User {userId}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await context.Users.AddAsync(user);
        
        var subscriptionPlan = new SubscriptionPlanEntity
        {
            Id = Guid.NewGuid(),
            PlanEnum = plan,
            Price = 29.99m,
            Credits = plan == SubscriptionPlanEnum.Unlimited ? 0 : 100
        };
        
        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Credits = credits,
            SubscriptionPlanId = subscriptionPlan.Id,
            SubscriptionPlan = subscriptionPlan,
            ExpiresAt = plan == SubscriptionPlanEnum.Unlimited ? null : DateTime.UtcNow.AddDays(30)
        };
        
        await context.SubscriptionPlans.AddAsync(subscriptionPlan);
        await context.Subscriptions.AddAsync(subscription);
        await context.SaveChangesAsync();
    }
}