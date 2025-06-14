using Application.Interfaces;
using Application.UseCases.Subscription.GetSubscription;
using Domain.Entities;
using Moq;

namespace Application.Tests.UseCases.Subscription.GetSubscription;

public class GetSubscriptionHandlerTests
{
    private readonly GetSubscriptionHandler _handler;
    private readonly Mock<ISubscriptionRepository> _repo = new();

    public GetSubscriptionHandlerTests()
    {
        _handler = new GetSubscriptionHandler(_repo.Object);
    }

    [Fact]
    public async Task Handle_ReturnsSubscription()
    {
        var sub = new SubscriptionEntity { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Credits = 20 };
        _repo.Setup(r => r.GetByUserIdAsync(sub.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(sub);

        var result = await _handler.Handle(new GetSubscriptionQuery(sub.UserId), CancellationToken.None);
        // Verify repository was called with expected user ID
        _repo.Verify(r => r.GetByUserIdAsync(sub.UserId, It.IsAny<CancellationToken>()), Times.Once);
        // Assert all relevant properties are returned correctly
        Assert.NotNull(result);
        Assert.Equal(sub.Id, result.Id);
        Assert.Equal(sub.UserId, result.UserId);
        Assert.Equal(sub.Credits, result.Credits);
        Assert.Null(result.ExpiresAt);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenSubscriptionNotFound()
    {
        var userId = Guid.NewGuid();
        _repo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionEntity?)null);

        var result = await _handler.Handle(new GetSubscriptionQuery(userId), CancellationToken.None);

        Assert.Null(result);
        _repo.Verify(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }
}