using Application.Interfaces;
using Application.UseCases.Subscription.PurchasePlan;
using Domain.Entities;
using Domain.Enums;
using Domain.Events;
using Domain.Exceptions;
using Moq;
using Xunit;

namespace Application.Tests.UseCases.Subscription;

public class PurchasePlanHandlerTests
{
    private readonly Mock<ISubscriptionRepository> _subscriptionRepositoryMock;
    private readonly Mock<ISubscriptionPlanRepository> _subscriptionPlanRepositoryMock;
    private readonly Mock<IEventPublisher> _eventPublisherMock;
    private readonly PurchasePlanHandler _handler;

    public PurchasePlanHandlerTests()
    {
        _subscriptionRepositoryMock = new Mock<ISubscriptionRepository>();
        _subscriptionPlanRepositoryMock = new Mock<ISubscriptionPlanRepository>();
        _eventPublisherMock = new Mock<IEventPublisher>();
        _handler = new PurchasePlanHandler(
            _subscriptionRepositoryMock.Object,
            _subscriptionPlanRepositoryMock.Object,
            _eventPublisherMock.Object);
    }

    [Fact]
    public async Task Handle_CreatesNewSubscription_WhenUserHasNoSubscription()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var command = new PurchasePlanCommand(userId, SubscriptionPlanEnum.Monthly100);

        var plan = new SubscriptionPlanEntity
        {
            Id = planId,
            PlanEnum = SubscriptionPlanEnum.Monthly100,
            Price = 29.99m,
            Credits = 100
        };

        SubscriptionEntity? createdSubscription = null;

        _subscriptionRepositoryMock
            .SetupSequence(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionEntity?)null) // First call returns null (no subscription)
            .ReturnsAsync(() => createdSubscription!); // Second call returns created subscription

        _subscriptionPlanRepositoryMock
            .Setup(x => x.GetByPlanAsync(SubscriptionPlanEnum.Monthly100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        _subscriptionRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<SubscriptionEntity>(), It.IsAny<CancellationToken>()))
            .Callback<SubscriptionEntity, CancellationToken>((s, _) => createdSubscription = s)
            .ReturnsAsync((SubscriptionEntity s, CancellationToken _) => s);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        _subscriptionRepositoryMock.Verify(
            x => x.CreateAsync(It.IsAny<SubscriptionEntity>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _eventPublisherMock.Verify(
            x => x.PublishAsync(It.IsAny<SubscriptionCreatedEvent>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_AddsCreditsToExistingSubscription()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var command = new PurchasePlanCommand(userId, SubscriptionPlanEnum.Package25);

        var plan = new SubscriptionPlanEntity
        {
            Id = planId,
            PlanEnum = SubscriptionPlanEnum.Package25,
            Price = 9.99m,
            Credits = 25
        };

        var existingSubscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Credits = 50,
            SubscriptionPlanId = planId
        };

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSubscription);

        _subscriptionPlanRepositoryMock
            .Setup(x => x.GetByPlanAsync(SubscriptionPlanEnum.Package25, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        _subscriptionRepositoryMock.Verify(
            x => x.UpdateAsync(It.Is<SubscriptionEntity>(s => s.Credits == 75), It.IsAny<CancellationToken>()),
            Times.Once);
        _eventPublisherMock.Verify(
            x => x.PublishAsync(It.Is<CreditsAddedEvent>(e => e.Amount == 25)),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ThrowsSubscriptionPlanNotFoundException_WhenPlanNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new PurchasePlanCommand(userId, SubscriptionPlanEnum.Monthly100);

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionEntity?)null);

        _subscriptionPlanRepositoryMock
            .Setup(x => x.GetByPlanAsync(SubscriptionPlanEnum.Monthly100, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionPlanEntity?)null);

        // Act & Assert
        await Assert.ThrowsAsync<SubscriptionPlanNotFoundException>(() =>
            _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_SetsExpirationDate_ForMonthlyPlan()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var command = new PurchasePlanCommand(userId, SubscriptionPlanEnum.Monthly100);

        var plan = new SubscriptionPlanEntity
        {
            Id = planId,
            PlanEnum = SubscriptionPlanEnum.Monthly100,
            Price = 29.99m,
            Credits = 100
        };

        var beforeCreate = DateTime.UtcNow;

        SubscriptionEntity? createdSubscription = null;

        _subscriptionRepositoryMock
            .SetupSequence(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionEntity?)null) // First call returns null (no subscription)
            .ReturnsAsync(() => createdSubscription!); // Second call returns created subscription

        _subscriptionPlanRepositoryMock
            .Setup(x => x.GetByPlanAsync(SubscriptionPlanEnum.Monthly100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        _subscriptionRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<SubscriptionEntity>(), It.IsAny<CancellationToken>()))
            .Callback<SubscriptionEntity, CancellationToken>((s, _) => createdSubscription = s)
            .ReturnsAsync((SubscriptionEntity s, CancellationToken _) => s);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(createdSubscription);
        Assert.NotNull(createdSubscription.ExpiresAt);
        Assert.True(createdSubscription.ExpiresAt >= beforeCreate.AddMonths(1).AddMinutes(-1));
    }

    [Fact]
    public async Task Handle_DoesNotAddCredits_ForUnlimitedPlan()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var command = new PurchasePlanCommand(userId, SubscriptionPlanEnum.Unlimited);

        var plan = new SubscriptionPlanEntity
        {
            Id = planId,
            PlanEnum = SubscriptionPlanEnum.Unlimited,
            Price = 99.99m,
            Credits = 0
        };

        var existingSubscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Credits = 50,
            SubscriptionPlanId = planId
        };

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSubscription);

        _subscriptionPlanRepositoryMock
            .Setup(x => x.GetByPlanAsync(SubscriptionPlanEnum.Unlimited, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _subscriptionRepositoryMock.Verify(
            x => x.UpdateAsync(It.Is<SubscriptionEntity>(s => s.Credits == 50), It.IsAny<CancellationToken>()),
            Times.Once);
        _eventPublisherMock.Verify(
            x => x.PublishAsync(It.IsAny<CreditsAddedEvent>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_PublishesCreditsAddedEvent_ForCreditPackage()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var command = new PurchasePlanCommand(userId, SubscriptionPlanEnum.Package10);

        var plan = new SubscriptionPlanEntity
        {
            Id = planId,
            PlanEnum = SubscriptionPlanEnum.Package10,
            Price = 4.99m,
            Credits = 10
        };

        var existingSubscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Credits = 5,
            SubscriptionPlanId = planId
        };

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSubscription);

        _subscriptionPlanRepositoryMock
            .Setup(x => x.GetByPlanAsync(SubscriptionPlanEnum.Package10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _eventPublisherMock.Verify(
            x => x.PublishAsync(It.Is<CreditsAddedEvent>(e => e.Amount == 10)),
            Times.Once);
    }
}
