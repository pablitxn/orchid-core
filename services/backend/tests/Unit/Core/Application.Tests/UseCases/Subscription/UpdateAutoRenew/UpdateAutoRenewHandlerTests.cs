using Application.Interfaces;
using Application.UseCases.Subscription.ConsumeCredits;
using Application.UseCases.Subscription.UpdateAutoRenew;
using Domain.Entities;
using Domain.Events;
using Moq;

namespace Application.Tests.UseCases.Subscription.UpdateAutoRenew;

public class UpdateAutoRenewHandlerTests
{
    private readonly Mock<IEventPublisher> _events = new();
    private readonly UpdateAutoRenewHandler _handler;
    private readonly Mock<ISubscriptionRepository> _repo = new();

    public UpdateAutoRenewHandlerTests()
    {
        _handler = new UpdateAutoRenewHandler(_repo.Object, _events.Object);
    }

    [Fact]
    public async Task Handle_UpdatesFlag_AndPublishesEvent()
    {
        var sub = new SubscriptionEntity { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), AutoRenew = true };
        _repo.Setup(r => r.GetByUserIdAsync(sub.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(sub);

        var cmd = new UpdateAutoRenewCommand(sub.UserId, false);
        var result = await _handler.Handle(cmd, CancellationToken.None);

        _repo.Verify(r => r.UpdateAsync(It.IsAny<SubscriptionEntity>(), It.IsAny<CancellationToken>()), Times.Once);
        _events.Verify(e => e.PublishAsync(It.IsAny<SubscriptionUpdatedEvent>()), Times.Once);
        Assert.False(result.AutoRenew);
    }

    [Fact]
    public async Task Handle_ThrowsSubscriptionNotFoundException_WhenNotFound()
    {
        var userId = Guid.NewGuid();
        _repo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionEntity?)null);

        var cmd = new UpdateAutoRenewCommand(userId, false);
        await Assert.ThrowsAsync<SubscriptionNotFoundException>(() => _handler.Handle(cmd, CancellationToken.None));
    }
}