using Application.Interfaces;
using Application.UseCases.Subscription.UpdateAutoRenew;
using Domain.Entities;
using Domain.Events;
using Domain.Exceptions;
using Moq;

namespace Application.Tests.UseCases.Subscription;

public class UpdateAutoRenewHandlerTests
{
    private readonly Mock<IEventPublisher> _events = new();
    private readonly UpdateAutoRenewHandler _handler;
    private readonly Mock<ISubscriptionRepository> _repo = new();
    private readonly Mock<IUserCreditsRepository> _userCreditsRepo = new();

    public UpdateAutoRenewHandlerTests()
    {
        _userCreditsRepo
            .Setup(r => r.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserCreditsEntity?)null);

        _handler = new UpdateAutoRenewHandler(_repo.Object, _userCreditsRepo.Object, _events.Object);
    }

    [Fact]
    public async Task Handle_UpdatesFlag_AndPublishesEvent()
    {
        var sub = new SubscriptionEntity { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), AutoRenew = true };
        var userCredits = new UserCreditsEntity
        {
            Id = Guid.NewGuid(),
            UserId = sub.UserId,
            AvailableCredits = 10,
            TotalCreditsGranted = 10,
            TotalCreditsConsumed = 0
        };
        _repo.Setup(r => r.GetByUserIdAsync(sub.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(sub);
        _userCreditsRepo.Setup(r => r.GetByUserIdAsync(sub.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userCredits);

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
