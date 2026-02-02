using Application.Interfaces;
using Application.UseCases.Subscription.ConsumeCredits;
using Domain.Entities;
using Domain.Enums;
using Domain.Events;
using Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Tests.UseCases.Subscription;

public class ConsumeCreditsHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IEventPublisher> _eventPublisherMock;
    private readonly Mock<ICreditLimitService> _creditLimitServiceMock;
    private readonly Mock<IAuditService> _auditServiceMock;
    private readonly Mock<ILogger<ConsumeCreditsHandler>> _loggerMock;
    private readonly Mock<ISubscriptionRepository> _subscriptionRepositoryMock;
    private readonly Mock<IUserCreditsRepository> _userCreditsRepositoryMock;
    private readonly ConsumeCreditsHandler _handler;

    public ConsumeCreditsHandlerTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _eventPublisherMock = new Mock<IEventPublisher>();
        _creditLimitServiceMock = new Mock<ICreditLimitService>();
        _auditServiceMock = new Mock<IAuditService>();
        _loggerMock = new Mock<ILogger<ConsumeCreditsHandler>>();
        _subscriptionRepositoryMock = new Mock<ISubscriptionRepository>();
        _userCreditsRepositoryMock = new Mock<IUserCreditsRepository>();

        _unitOfWorkMock.Setup(x => x.Subscriptions).Returns(_subscriptionRepositoryMock.Object);
        _unitOfWorkMock.Setup(x => x.UserCredits).Returns(_userCreditsRepositoryMock.Object);

        _handler = new ConsumeCreditsHandler(
            _unitOfWorkMock.Object,
            _eventPublisherMock.Object,
            _creditLimitServiceMock.Object,
            _auditServiceMock.Object,
            _loggerMock.Object
        );
    }

    [Fact]
    public async Task Handle_ConsumesCredits_AndPublishesEvent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userCreditsId = Guid.NewGuid();
        var command = new ConsumeCreditsCommand(userId, 10);

        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SubscriptionPlan = new SubscriptionPlanEntity
            {
                PlanEnum = SubscriptionPlanEnum.Monthly100,
                Price = 29.99m,
                Credits = 100
            }
        };

        var userCredits = new UserCreditsEntity
        {
            Id = userCreditsId,
            UserId = userId,
            AvailableCredits = 50,
            TotalCreditsGranted = 50,
            TotalCreditsConsumed = 0,
            Version = 1
        };

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        _userCreditsRepositoryMock
            .Setup(x => x.GetOrCreateAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userCredits);

        _creditLimitServiceMock
            .Setup(x => x.CheckLimitsAsync(userId, 10, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreditLimitCheckResult(true, null));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(40, result.Credits);

        _userCreditsRepositoryMock.Verify(
            x => x.UpdateWithVersionCheckAsync(
                It.Is<UserCreditsEntity>(uc => uc.AvailableCredits == 40 && uc.Version == 2),
                1,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _eventPublisherMock.Verify(
            x => x.PublishAsync(
                It.Is<CreditsConsumedEvent>(e => e.Amount == 10)),
            Times.Once);

        _unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ThrowsInsufficientCreditsException_WhenNoUserCreditsExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new ConsumeCreditsCommand(userId, 10);

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionEntity?)null);

        // GetOrCreate returns a new entity with 0 credits
        var emptyUserCredits = new UserCreditsEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AvailableCredits = 0,
            TotalCreditsGranted = 0,
            TotalCreditsConsumed = 0,
            Version = 0
        };

        _userCreditsRepositoryMock
            .Setup(x => x.GetOrCreateAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyUserCredits);

        _creditLimitServiceMock
            .Setup(x => x.CheckLimitsAsync(userId, 10, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreditLimitCheckResult(true, null));

        // Act & Assert
        await Assert.ThrowsAsync<InsufficientCreditsException>(() =>
            _handler.Handle(command, CancellationToken.None));

        _unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ThrowsInsufficientCreditsException_WhenInsufficientCredits()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new ConsumeCreditsCommand(userId, 100);

        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SubscriptionPlan = new SubscriptionPlanEntity
            {
                PlanEnum = SubscriptionPlanEnum.Monthly100,
                Price = 29.99m,
                Credits = 100
            }
        };

        var userCredits = new UserCreditsEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AvailableCredits = 50,
            TotalCreditsGranted = 50,
            TotalCreditsConsumed = 0,
            Version = 1
        };

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        _userCreditsRepositoryMock
            .Setup(x => x.GetOrCreateAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userCredits);

        _creditLimitServiceMock
            .Setup(x => x.CheckLimitsAsync(userId, 100, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreditLimitCheckResult(true, null));

        // Act & Assert
        await Assert.ThrowsAsync<InsufficientCreditsException>(() =>
            _handler.Handle(command, CancellationToken.None));

        _unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _eventPublisherMock.Verify(x => x.PublishAsync(It.IsAny<CreditsConsumedEvent>()), Times.Never);
    }

    [Fact]
    public async Task Handle_SkipsLimitCheck_ForUnlimitedPlan()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new ConsumeCreditsCommand(userId, 100);

        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SubscriptionPlan = new SubscriptionPlanEntity
            {
                PlanEnum = SubscriptionPlanEnum.Unlimited,
                Price = 99.99m,
                Credits = 999999
            }
        };

        var userCredits = new UserCreditsEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AvailableCredits = 50,
            TotalCreditsGranted = 50,
            TotalCreditsConsumed = 0,
            Version = 1
        };

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        _userCreditsRepositoryMock
            .Setup(x => x.GetOrCreateAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userCredits);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(50, result.Credits); // Credits unchanged for unlimited

        _creditLimitServiceMock.Verify(
            x => x.CheckLimitsAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _creditLimitServiceMock.Verify(
            x => x.ConsumeLimitsAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_RetriesOnConcurrencyException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userCreditsId = Guid.NewGuid();
        var command = new ConsumeCreditsCommand(userId, 10);
        var attemptCount = 0;
        var getCallCount = 0;

        var subscriptionPlan = new SubscriptionPlanEntity
        {
            PlanEnum = SubscriptionPlanEnum.Monthly100,
            Price = 29.99m,
            Credits = 100
        };

        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SubscriptionPlan = subscriptionPlan
        };

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        // Return a fresh UserCreditsEntity instance on each call to avoid state mutation
        _userCreditsRepositoryMock
            .Setup(x => x.GetOrCreateAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                getCallCount++;
                return new UserCreditsEntity
                {
                    Id = userCreditsId,
                    UserId = userId,
                    AvailableCredits = 50,
                    TotalCreditsGranted = 50,
                    TotalCreditsConsumed = 0,
                    Version = 1
                };
            });

        _creditLimitServiceMock
            .Setup(x => x.CheckLimitsAsync(userId, 10, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreditLimitCheckResult(true, null));

        _userCreditsRepositoryMock
            .Setup(x => x.UpdateWithVersionCheckAsync(It.IsAny<UserCreditsEntity>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                attemptCount++;
                if (attemptCount < 2)
                {
                    throw new ConcurrencyException("UserCredits", userCreditsId, 1, 0);
                }
                return Task.CompletedTask;
            });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(40, result.Credits);
        Assert.Equal(2, attemptCount);
        Assert.Equal(2, getCallCount); // Should have fetched UserCredits twice
        _unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        _unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ThrowsCreditLimitExceededException_WhenLimitExceeded()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new ConsumeCreditsCommand(userId, 10);

        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SubscriptionPlan = new SubscriptionPlanEntity
            {
                PlanEnum = SubscriptionPlanEnum.Monthly100,
                Price = 29.99m,
                Credits = 100
            }
        };

        var userCredits = new UserCreditsEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AvailableCredits = 100,
            TotalCreditsGranted = 100,
            TotalCreditsConsumed = 0,
            Version = 1
        };

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        _userCreditsRepositoryMock
            .Setup(x => x.GetOrCreateAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userCredits);

        _creditLimitServiceMock
            .Setup(x => x.CheckLimitsAsync(userId, 10, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreditLimitCheckResult(
                false,
                new List<LimitViolation>
                {
                    new LimitViolation(
                        "daily",
                        50,
                        45,
                        5,
                        DateTime.UtcNow.AddHours(6),
                        null
                    )
                }
            ));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<CreditLimitExceededException>(() =>
            _handler.Handle(command, CancellationToken.None));

        Assert.Contains("daily limit", exception.Message);
        _unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ThrowsAfterMaxRetryAttempts()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userCreditsId = Guid.NewGuid();
        var command = new ConsumeCreditsCommand(userId, 10);

        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SubscriptionPlan = new SubscriptionPlanEntity
            {
                PlanEnum = SubscriptionPlanEnum.Monthly100,
                Price = 29.99m,
                Credits = 100
            }
        };

        var userCredits = new UserCreditsEntity
        {
            Id = userCreditsId,
            UserId = userId,
            AvailableCredits = 50,
            TotalCreditsGranted = 50,
            TotalCreditsConsumed = 0,
            Version = 1
        };

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        _userCreditsRepositoryMock
            .Setup(x => x.GetOrCreateAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userCredits);

        _creditLimitServiceMock
            .Setup(x => x.CheckLimitsAsync(userId, 10, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreditLimitCheckResult(true, null));

        _userCreditsRepositoryMock
            .Setup(x => x.UpdateWithVersionCheckAsync(It.IsAny<UserCreditsEntity>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyException("UserCredits", userCreditsId, 1, 0));

        // Act & Assert
        await Assert.ThrowsAsync<ConcurrencyException>(() =>
            _handler.Handle(command, CancellationToken.None));

        _unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Exactly(3));
        _unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task Handle_UpdatesCreditLimitsAfterSuccessfulConsumption()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new ConsumeCreditsCommand(userId, 10, "plugin_usage");

        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SubscriptionPlan = new SubscriptionPlanEntity
            {
                PlanEnum = SubscriptionPlanEnum.Monthly100,
                Price = 29.99m,
                Credits = 100
            }
        };

        var userCredits = new UserCreditsEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AvailableCredits = 50,
            TotalCreditsGranted = 50,
            TotalCreditsConsumed = 0,
            Version = 1
        };

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        _userCreditsRepositoryMock
            .Setup(x => x.GetOrCreateAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userCredits);

        _creditLimitServiceMock
            .Setup(x => x.CheckLimitsAsync(userId, 10, "plugin_usage", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreditLimitCheckResult(true, null));

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _creditLimitServiceMock.Verify(
            x => x.ConsumeLimitsAsync(userId, 10, "plugin_usage", It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
