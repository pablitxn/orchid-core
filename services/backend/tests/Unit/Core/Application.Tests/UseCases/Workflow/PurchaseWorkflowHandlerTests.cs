using Application.Interfaces;
using Application.UseCases.Workflow.PurchaseWorkflow;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Application.Tests.UseCases.Workflow;

public class PurchaseWorkflowHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ICreditTrackingService> _creditTrackingServiceMock;
    private readonly Mock<ICostRegistry> _costRegistryMock;
    private readonly Mock<ICreditLimitService> _creditLimitServiceMock;
    private readonly Mock<ILogger<PurchaseWorkflowHandler>> _loggerMock;
    private readonly Mock<IWorkflowRepository> _workflowRepositoryMock;
    private readonly Mock<ISubscriptionRepository> _subscriptionRepositoryMock;
    private readonly PurchaseWorkflowHandler _handler;

    public PurchaseWorkflowHandlerTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _creditTrackingServiceMock = new Mock<ICreditTrackingService>();
        _costRegistryMock = new Mock<ICostRegistry>();
        _creditLimitServiceMock = new Mock<ICreditLimitService>();
        _loggerMock = new Mock<ILogger<PurchaseWorkflowHandler>>();
        _workflowRepositoryMock = new Mock<IWorkflowRepository>();
        _subscriptionRepositoryMock = new Mock<ISubscriptionRepository>();

        _unitOfWorkMock.Setup(x => x.Workflows).Returns(_workflowRepositoryMock.Object);
        _unitOfWorkMock.Setup(x => x.Subscriptions).Returns(_subscriptionRepositoryMock.Object);

        _handler = new PurchaseWorkflowHandler(
            _unitOfWorkMock.Object,
            _creditTrackingServiceMock.Object,
            _costRegistryMock.Object,
            _creditLimitServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_PurchasesWorkflow_Successfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var command = new PurchaseWorkflowCommand(userId, workflowId);

        var workflow = new WorkflowEntity
        {
            Id = workflowId,
            Name = "Test Workflow",
            PriceCredits = 30
        };

        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Credits = 100,
            Version = 1,
            SubscriptionPlan = new SubscriptionPlanEntity
            {
                PlanEnum = SubscriptionPlanEnum.Monthly100,
                Price = 29.99m,
                Credits = 100
            }
        };

        _workflowRepositoryMock
            .Setup(x => x.GetByIdAsync(workflowId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workflow);

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        _workflowRepositoryMock
            .Setup(x => x.UserHasWorkflowAsync(userId, workflowId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _costRegistryMock
            .Setup(x => x.GetWorkflowPurchaseCostAsync(workflowId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(30);

        _creditLimitServiceMock
            .Setup(x => x.CheckLimitsAsync(userId, 30, "workflow_purchase", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreditLimitCheckResult(true, null));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(PurchaseOutcome.Success, result);
        _unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ThrowsWorkflowNotFoundException_WhenWorkflowNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var command = new PurchaseWorkflowCommand(userId, workflowId);

        _workflowRepositoryMock
            .Setup(x => x.GetByIdAsync(workflowId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkflowEntity?)null);

        // Act & Assert
        await Assert.ThrowsAsync<WorkflowNotFoundException>(() =>
            _handler.Handle(command, CancellationToken.None));

        _unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ThrowsActiveSubscriptionRequiredException_WhenNoSubscription()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var command = new PurchaseWorkflowCommand(userId, workflowId);

        var workflow = new WorkflowEntity
        {
            Id = workflowId,
            Name = "Test Workflow"
        };

        _workflowRepositoryMock
            .Setup(x => x.GetByIdAsync(workflowId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workflow);

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionEntity?)null);

        // Act & Assert
        await Assert.ThrowsAsync<ActiveSubscriptionRequiredException>(() =>
            _handler.Handle(command, CancellationToken.None));

        _unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ThrowsAlreadyPurchasedException_WhenUserAlreadyOwnsWorkflow()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var command = new PurchaseWorkflowCommand(userId, workflowId);

        var workflow = new WorkflowEntity { Id = workflowId };
        var subscription = new SubscriptionEntity
        {
            UserId = userId,
            Credits = 100,
            SubscriptionPlan = new SubscriptionPlanEntity { PlanEnum = SubscriptionPlanEnum.Monthly100, Price = 29.99m, Credits = 100 }
        };

        _workflowRepositoryMock
            .Setup(x => x.GetByIdAsync(workflowId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workflow);

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        _workflowRepositoryMock
            .Setup(x => x.UserHasWorkflowAsync(userId, workflowId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act & Assert
        await Assert.ThrowsAsync<AlreadyPurchasedException>(() =>
            _handler.Handle(command, CancellationToken.None));

        _unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ThrowsInsufficientCreditsException_WhenNotEnoughCredits()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var command = new PurchaseWorkflowCommand(userId, workflowId);

        var workflow = new WorkflowEntity { Id = workflowId, PriceCredits = 50 };
        var subscription = new SubscriptionEntity
        {
            UserId = userId,
            Credits = 20, // Not enough
            SubscriptionPlan = new SubscriptionPlanEntity { PlanEnum = SubscriptionPlanEnum.Monthly100, Price = 29.99m, Credits = 100 }
        };

        _workflowRepositoryMock
            .Setup(x => x.GetByIdAsync(workflowId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workflow);

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        _workflowRepositoryMock
            .Setup(x => x.UserHasWorkflowAsync(userId, workflowId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _costRegistryMock
            .Setup(x => x.GetWorkflowPurchaseCostAsync(workflowId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(50);

        _creditLimitServiceMock
            .Setup(x => x.CheckLimitsAsync(userId, 50, "workflow_purchase", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreditLimitCheckResult(true, null));

        // Act & Assert
        await Assert.ThrowsAsync<InsufficientCreditsException>(() =>
            _handler.Handle(command, CancellationToken.None));

        _unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ThrowsCreditLimitExceededException_WhenLimitExceeded()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var command = new PurchaseWorkflowCommand(userId, workflowId);

        var workflow = new WorkflowEntity { Id = workflowId, PriceCredits = 30 };
        var subscription = new SubscriptionEntity
        {
            UserId = userId,
            Credits = 100,
            SubscriptionPlan = new SubscriptionPlanEntity { PlanEnum = SubscriptionPlanEnum.Monthly100, Price = 29.99m, Credits = 100 }
        };

        _workflowRepositoryMock
            .Setup(x => x.GetByIdAsync(workflowId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workflow);

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        _workflowRepositoryMock
            .Setup(x => x.UserHasWorkflowAsync(userId, workflowId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _costRegistryMock
            .Setup(x => x.GetWorkflowPurchaseCostAsync(workflowId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(30);

        _creditLimitServiceMock
            .Setup(x => x.CheckLimitsAsync(userId, 30, "workflow_purchase", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreditLimitCheckResult(
                false,
                new List<LimitViolation>
                {
                    new LimitViolation("daily", 50, 45, 5, DateTime.UtcNow.AddHours(6), null)
                }));

        // Act & Assert
        await Assert.ThrowsAsync<CreditLimitExceededException>(() =>
            _handler.Handle(command, CancellationToken.None));

        _unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SkipsCreditDeduction_ForUnlimitedSubscription()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var command = new PurchaseWorkflowCommand(userId, workflowId);

        var workflow = new WorkflowEntity { Id = workflowId, Name = "Test", PriceCredits = 30 };
        var subscription = new SubscriptionEntity
        {
            UserId = userId,
            Credits = 0,
            Version = 1,
            SubscriptionPlan = new SubscriptionPlanEntity { PlanEnum = SubscriptionPlanEnum.Unlimited, Price = 99.99m, Credits = 0 }
        };

        _workflowRepositoryMock
            .Setup(x => x.GetByIdAsync(workflowId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workflow);

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        _workflowRepositoryMock
            .Setup(x => x.UserHasWorkflowAsync(userId, workflowId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _costRegistryMock
            .Setup(x => x.GetWorkflowPurchaseCostAsync(workflowId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(30);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(PurchaseOutcome.Success, result);
        _creditLimitServiceMock.Verify(
            x => x.CheckLimitsAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
