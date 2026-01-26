using Application.Interfaces;
using Application.UseCases.AdminBilling.AdjustCredits;
using Domain.Entities;
using Domain.Events;
using Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Application.Tests.UseCases.AdminBilling;

public class AdjustCreditsHandlerTests
{
    private readonly Mock<ISubscriptionRepository> _repo = new();
    private readonly Mock<IAuditService> _auditService = new();
    private readonly Mock<IEventPublisher> _eventPublisher = new();
    private readonly Mock<ILogger<AdjustCreditsHandler>> _logger = new();
    private readonly AdjustCreditsHandler _handler;

    public AdjustCreditsHandlerTests()
    {
        _handler = new AdjustCreditsHandler(
            _repo.Object,
            _auditService.Object,
            _eventPublisher.Object,
            _logger.Object);
    }

    [Fact]
    public async Task Handle_AddsCredits_WhenAmountPositive()
    {
        var userId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Credits = 100
        };

        _repo.Setup(r => r.GetByUserIdWithPlanAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        var cmd = new AdjustCreditsCommand(userId, 50, "Bonus credits", adminId);
        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.Equal(150, subscription.Credits);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<SubscriptionEntity>(), It.IsAny<CancellationToken>()), Times.Once);
        _eventPublisher.Verify(e => e.PublishAsync(It.IsAny<CreditsAddedEvent>()), Times.Once);
        _auditService.Verify(a => a.LogSuccessAsync(
            AuditActionTypes.AdminCreditAdjust,
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<object>(),
            It.IsAny<object>(),
            It.IsAny<Dictionary<string, object>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_RemovesCredits_WhenAmountNegative()
    {
        var userId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Credits = 100
        };

        _repo.Setup(r => r.GetByUserIdWithPlanAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        var cmd = new AdjustCreditsCommand(userId, -30, "Credit adjustment", adminId);
        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.Equal(70, subscription.Credits);
        _eventPublisher.Verify(e => e.PublishAsync(It.IsAny<CreditsConsumedEvent>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ThrowsValidationException_WhenReasonEmpty()
    {
        var cmd = new AdjustCreditsCommand(Guid.NewGuid(), 50, "", Guid.NewGuid());

        await Assert.ThrowsAsync<ValidationException>(
            () => _handler.Handle(cmd, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ThrowsValidationException_WhenAmountZero()
    {
        var cmd = new AdjustCreditsCommand(Guid.NewGuid(), 0, "Reason", Guid.NewGuid());

        await Assert.ThrowsAsync<ValidationException>(
            () => _handler.Handle(cmd, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ThrowsInsufficientCreditsException_WhenRemovingMoreThanAvailable()
    {
        var userId = Guid.NewGuid();
        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Credits = 50
        };

        _repo.Setup(r => r.GetByUserIdWithPlanAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        var cmd = new AdjustCreditsCommand(userId, -100, "Remove credits", Guid.NewGuid());

        await Assert.ThrowsAsync<InsufficientCreditsException>(
            () => _handler.Handle(cmd, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ThrowsSubscriptionNotFoundException_WhenNotFound()
    {
        var userId = Guid.NewGuid();
        _repo.Setup(r => r.GetByUserIdWithPlanAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionEntity?)null);

        var cmd = new AdjustCreditsCommand(userId, 50, "Add credits", Guid.NewGuid());

        await Assert.ThrowsAsync<SubscriptionNotFoundException>(
            () => _handler.Handle(cmd, CancellationToken.None));
    }
}
