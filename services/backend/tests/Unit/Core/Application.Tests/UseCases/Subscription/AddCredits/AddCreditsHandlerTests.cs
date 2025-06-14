using Application.Interfaces;
using Application.UseCases.Subscription.AddCredits;
using Application.UseCases.Subscription.ConsumeCredits;
using Domain.Entities;
using Domain.Events;
using Moq;

namespace Application.Tests.UseCases.Subscription.AddCredits;

public class AddCreditsHandlerTests
{
    private readonly Mock<IEventPublisher> _events = new();
    private readonly AddCreditsHandler _handler;
    private readonly Mock<ISubscriptionRepository> _repo = new();

    public AddCreditsHandlerTests()
    {
        _handler = new AddCreditsHandler(_repo.Object, _events.Object);
    }

    [Fact]
    public async Task Handle_AddsCredits_AndPublishesEvent()
    {
        var sub = new SubscriptionEntity { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Credits = 5 };
        _repo.Setup(r => r.GetByUserIdAsync(sub.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(sub);

        var cmd = new AddCreditsCommand(sub.UserId, 5);
        var result = await _handler.Handle(cmd, CancellationToken.None);

        _repo.Verify(r => r.UpdateAsync(It.IsAny<SubscriptionEntity>(), It.IsAny<CancellationToken>()), Times.Once);
        _events.Verify(e => e.PublishAsync(It.IsAny<CreditsAddedEvent>()), Times.Once);
        Assert.Equal(10, result.Credits);
    }

    [Fact]
    public async Task Handle_ThrowsSubscriptionNotFoundException_WhenSubscriptionNotFound()
    {
        var userId = Guid.NewGuid();
        _repo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionEntity?)null);

        var cmd = new AddCreditsCommand(userId, 5);
        await Assert.ThrowsAsync<SubscriptionNotFoundException>(() => _handler.Handle(cmd, CancellationToken.None));
    }
}