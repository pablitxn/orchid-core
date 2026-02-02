using Application.Interfaces;
using Application.UseCases.Subscription.GetSubscription;
using Domain.Entities;
using Moq;

namespace Application.Tests.UseCases.Subscription;

public class GetSubscriptionHandlerTests
{
    private readonly GetSubscriptionHandler _handler;
    private readonly Mock<ISubscriptionRepository> _subscriptionRepo = new();
    private readonly Mock<IUserCreditsRepository> _userCreditsRepo = new();

    public GetSubscriptionHandlerTests()
    {
        _handler = new GetSubscriptionHandler(_subscriptionRepo.Object, _userCreditsRepo.Object);
    }

    [Fact]
    public async Task Handle_ReturnsSubscriptionWithCredits()
    {
        var userId = Guid.NewGuid();
        var sub = new SubscriptionEntity { Id = Guid.NewGuid(), UserId = userId };
        var userCredits = new UserCreditsEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AvailableCredits = 20,
            TotalCreditsGranted = 20,
            TotalCreditsConsumed = 0
        };

        _subscriptionRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(sub);
        _userCreditsRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(userCredits);

        var result = await _handler.Handle(new GetSubscriptionQuery(userId), CancellationToken.None);

        _subscriptionRepo.Verify(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
        _userCreditsRepo.Verify(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(result);
        Assert.Equal(sub.Id, result.Id);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(20, result.Credits);
        Assert.Null(result.ExpiresAt);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenNoSubscriptionAndNoCredits()
    {
        var userId = Guid.NewGuid();
        _subscriptionRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionEntity?)null);
        _userCreditsRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserCreditsEntity?)null);

        var result = await _handler.Handle(new GetSubscriptionQuery(userId), CancellationToken.None);

        Assert.Null(result);
        _subscriptionRepo.Verify(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
        _userCreditsRepo.Verify(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ReturnsCreditsOnly_WhenNoSubscriptionButHasCredits()
    {
        var userId = Guid.NewGuid();
        var userCredits = new UserCreditsEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AvailableCredits = 50,
            TotalCreditsGranted = 50,
            TotalCreditsConsumed = 0
        };

        _subscriptionRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionEntity?)null);
        _userCreditsRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userCredits);

        var result = await _handler.Handle(new GetSubscriptionQuery(userId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(Guid.Empty, result.Id); // No subscription ID
        Assert.Equal(userId, result.UserId);
        Assert.Equal(50, result.Credits);
    }

    [Fact]
    public async Task Handle_ReturnsZeroCredits_WhenSubscriptionExistsButNoCreditsEntity()
    {
        var userId = Guid.NewGuid();
        var sub = new SubscriptionEntity { Id = Guid.NewGuid(), UserId = userId };

        _subscriptionRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(sub);
        _userCreditsRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserCreditsEntity?)null);

        var result = await _handler.Handle(new GetSubscriptionQuery(userId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(sub.Id, result.Id);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(0, result.Credits); // Falls back to 0 when no UserCreditsEntity
    }
}
