using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Tests.Services;

public class CreditValidationServiceTests
{
    private readonly Mock<ISubscriptionRepository> _subscriptionRepositoryMock;
    private readonly Mock<IPluginRepository> _pluginRepositoryMock;
    private readonly Mock<IWorkflowRepository> _workflowRepositoryMock;
    private readonly Mock<ICostRegistry> _costRegistryMock;
    private readonly Mock<IUserBillingPreferenceRepository> _billingPreferenceRepositoryMock;
    private readonly Mock<ILogger<CreditValidationService>> _loggerMock;
    private readonly CreditValidationService _service;

    public CreditValidationServiceTests()
    {
        _subscriptionRepositoryMock = new Mock<ISubscriptionRepository>();
        _pluginRepositoryMock = new Mock<IPluginRepository>();
        _workflowRepositoryMock = new Mock<IWorkflowRepository>();
        _costRegistryMock = new Mock<ICostRegistry>();
        _billingPreferenceRepositoryMock = new Mock<IUserBillingPreferenceRepository>();
        _loggerMock = new Mock<ILogger<CreditValidationService>>();

        _service = new CreditValidationService(
            _subscriptionRepositoryMock.Object,
            _costRegistryMock.Object,
            _pluginRepositoryMock.Object,
            _workflowRepositoryMock.Object,
            _billingPreferenceRepositoryMock.Object,
            _loggerMock.Object
        );
    }

    [Fact]
    public async Task ValidateMessageCost_Should_ReturnValid_When_UserHasSufficientCredits()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var messageContent = "Test message";
        var pluginIds = new List<Guid> { Guid.NewGuid() };
        
        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Credits = 100,
            SubscriptionPlan = new SubscriptionPlanEntity { PlanEnum = SubscriptionPlanEnum.Monthly100, Price = 10, Credits = 100 }
        };

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        _costRegistryMock
            .Setup(x => x.GetMessageFixedCostAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        _costRegistryMock
            .Setup(x => x.GetPluginUsageCostsBatchAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int> { { pluginIds.First(), 10 } });
            
        _pluginRepositoryMock
            .Setup(x => x.GetByIdAsync(pluginIds.First(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PluginEntity { Id = pluginIds.First(), Name = "Test Plugin" });

        // Act
        var result = await _service.ValidateMessageCostAsync(
            userId, messageContent, pluginIds, null, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeTrue();
        result.RequiredCredits.Should().Be(15); // 5 for message + 10 for plugin
        result.AvailableCredits.Should().Be(100);
        result.HasUnlimitedCredits.Should().BeFalse();
        result.CostBreakdown.Should().NotBeNull();
        result.CostBreakdown!.BaseCost.Should().Be(5);
        result.CostBreakdown.PluginCosts.Should().HaveCount(1);
        result.CostBreakdown.TotalCost.Should().Be(15);
    }

    [Fact]
    public async Task ValidateMessageCost_Should_ReturnValid_When_UserHasUnlimitedPlan()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var messageContent = "Test message";
        
        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Credits = 0,
            SubscriptionPlan = new SubscriptionPlanEntity { PlanEnum = SubscriptionPlanEnum.Unlimited, Price = 25, Credits = -1 }
        };

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        // Act
        var result = await _service.ValidateMessageCostAsync(
            userId, messageContent, null, null, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeTrue();
        result.HasUnlimitedCredits.Should().BeTrue();
        result.RequiredCredits.Should().Be(0);
        result.AvailableCredits.Should().Be(int.MaxValue);
    }

    [Fact]
    public async Task ValidateMessageCost_Should_ReturnInvalid_When_InsufficientCredits()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var messageContent = "Test message";
        var pluginIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        
        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Credits = 10,
            SubscriptionPlan = new SubscriptionPlanEntity { PlanEnum = SubscriptionPlanEnum.Package10, Price = 5, Credits = 10 }
        };

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        _costRegistryMock
            .Setup(x => x.GetMessageFixedCostAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        var pluginCosts = new Dictionary<Guid, int>();
        foreach (var pluginId in pluginIds)
        {
            pluginCosts[pluginId] = 10;
            _pluginRepositoryMock
                .Setup(x => x.GetByIdAsync(pluginId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PluginEntity { Id = pluginId, Name = $"Plugin {pluginId}" });
        }
        
        _costRegistryMock
            .Setup(x => x.GetPluginUsageCostsBatchAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pluginCosts);

        // Act
        var result = await _service.ValidateMessageCostAsync(
            userId, messageContent, pluginIds, null, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeFalse();
        result.RequiredCredits.Should().Be(25); // 5 + 10 + 10
        result.AvailableCredits.Should().Be(10);
        result.ErrorMessage.Should().Contain("Insufficient credits");
    }

    [Fact]
    public async Task ValidateMessageCost_Should_CalculateTokenBasedCost_When_BillingMethodIsTokens()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var messageContent = "This is a longer message that will use token-based pricing";
        
        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Credits = 100,
            SubscriptionPlan = new SubscriptionPlanEntity { PlanEnum = SubscriptionPlanEnum.Monthly100, Price = 10, Credits = 100 }
        };

        var billingPreference = new UserBillingPreferenceEntity
        {
            UserId = userId,
            MessageBillingMethod = "tokens"
        };

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        _billingPreferenceRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(billingPreference);

        _costRegistryMock
            .Setup(x => x.GetMessageTokenCostPer1kAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(2.0m); // 2 credits per 1k tokens

        // Act
        var result = await _service.ValidateMessageCostAsync(
            userId, messageContent, null, null, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeTrue();
        result.RequiredCredits.Should().BeGreaterThan(0); // Estimated based on message length
        result.CostBreakdown!.BaseCost.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ValidatePluginPurchase_Should_ReturnValid_When_UserHasSufficientCredits()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        
        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Credits = 100,
            SubscriptionPlan = new SubscriptionPlanEntity { PlanEnum = SubscriptionPlanEnum.Monthly100, Price = 10, Credits = 100 }
        };

        var plugin = new PluginEntity
        {
            Id = pluginId,
            Name = "Test Plugin",
            PriceCredits = 50,
            IsActive = true
        };

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        _pluginRepositoryMock
            .Setup(x => x.GetByIdAsync(pluginId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin);

        _costRegistryMock
            .Setup(x => x.GetPluginPurchaseCostAsync(pluginId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(50);

        // Act
        var result = await _service.ValidatePluginPurchaseAsync(userId, pluginId, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeTrue();
        result.RequiredCredits.Should().Be(50);
        result.AvailableCredits.Should().Be(100);
    }

    [Fact]
    public async Task ValidatePluginPurchase_Should_ReturnInvalid_When_PluginNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        
        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Credits = 100,
            SubscriptionPlan = new SubscriptionPlanEntity { PlanEnum = SubscriptionPlanEnum.Monthly100, Price = 10, Credits = 100 }
        };

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        _pluginRepositoryMock
            .Setup(x => x.GetByIdAsync(pluginId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PluginEntity?)null);

        // Act
        var result = await _service.ValidatePluginPurchaseAsync(userId, pluginId, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Plugin not found");
    }

    [Fact]
    public async Task ValidateWorkflowPurchase_Should_ReturnValid_When_UserHasSufficientCredits()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        
        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Credits = 200,
            SubscriptionPlan = new SubscriptionPlanEntity { PlanEnum = SubscriptionPlanEnum.Monthly100, Price = 10, Credits = 100 }
        };

        var workflow = new WorkflowEntity
        {
            Id = workflowId,
            Name = "Test Workflow",
            PriceCredits = 100
        };

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        _workflowRepositoryMock
            .Setup(x => x.GetByIdAsync(workflowId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workflow);
            
        _costRegistryMock
            .Setup(x => x.GetWorkflowPurchaseCostAsync(workflowId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(100);

        // Act
        var result = await _service.ValidateWorkflowPurchaseAsync(userId, workflowId, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeTrue();
        result.RequiredCredits.Should().Be(100);
        result.AvailableCredits.Should().Be(200);
    }

    [Fact]
    public async Task ValidateOperation_Should_ReturnValid_When_UserHasSufficientCredits()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var requiredCredits = 25;
        var operationType = "custom_operation";
        
        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Credits = 50,
            SubscriptionPlan = new SubscriptionPlanEntity { PlanEnum = SubscriptionPlanEnum.Package25, Price = 8, Credits = 25 }
        };

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        // Act
        var result = await _service.ValidateOperationAsync(
            userId, requiredCredits, operationType, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeTrue();
        result.RequiredCredits.Should().Be(25);
        result.AvailableCredits.Should().Be(50);
    }

    [Fact]
    public async Task ValidateOperation_Should_ReturnInvalid_When_NoSubscription()
    {
        // Arrange
        var userId = Guid.NewGuid();
        
        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionEntity?)null);

        // Act
        var result = await _service.ValidateOperationAsync(
            userId, 10, "test", CancellationToken.None);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No active subscription");
        result.AvailableCredits.Should().Be(0);
    }

    [Fact]
    public async Task ValidateMessageCost_Should_IncludeWorkflowCosts()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var messageContent = "Test";
        var workflowIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        
        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Credits = 100,
            SubscriptionPlan = new SubscriptionPlanEntity { PlanEnum = SubscriptionPlanEnum.Monthly100, Price = 10, Credits = 100 }
        };

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        _costRegistryMock
            .Setup(x => x.GetMessageFixedCostAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        var workflowCosts = new Dictionary<Guid, int>();
        foreach (var workflowId in workflowIds)
        {
            workflowCosts[workflowId] = 20;
            _workflowRepositoryMock
                .Setup(x => x.GetByIdAsync(workflowId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new WorkflowEntity { Id = workflowId, Name = $"Workflow {workflowId}" });
        }
        
        _costRegistryMock
            .Setup(x => x.GetWorkflowUsageCostsBatchAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(workflowCosts);

        // Act
        var result = await _service.ValidateMessageCostAsync(
            userId, messageContent, null, workflowIds, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeTrue();
        result.RequiredCredits.Should().Be(45); // 5 + 20 + 20
        result.CostBreakdown!.WorkflowCosts.Should().HaveCount(2);
        result.CostBreakdown.TotalCost.Should().Be(45);
    }
}