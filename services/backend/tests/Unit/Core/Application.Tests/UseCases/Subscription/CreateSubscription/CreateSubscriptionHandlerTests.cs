using Application.Interfaces;
using Application.UseCases.Subscription.CreateSubscription;
using Domain.Entities;
using Domain.Events;
using Moq;

namespace Application.Tests.UseCases.Subscription.CreateSubscription;

public class CreateSubscriptionHandlerTests
{
    private readonly Mock<IEventPublisher> _events = new();
    private readonly CreateSubscriptionHandler _handler;
    private readonly Mock<ISubscriptionRepository> _repo = new();
    private readonly Mock<IUserRepository> _users = new();

    public CreateSubscriptionHandlerTests()
    {
        _handler = new CreateSubscriptionHandler(_repo.Object, _users.Object, _events.Object);
    }

    [Fact]
    public async Task Handle_CreatesSubscription_AndPublishesEvent()
    {
        var userId = Guid.NewGuid();
        _users.Setup(r => r.GetByIdAsync(userId, CancellationToken.None))
            .ReturnsAsync(new UserEntity { Id = userId, Email = "test@example.com" });
        _repo.Setup(r => r.CreateAsync(It.IsAny<SubscriptionEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionEntity s, CancellationToken _) => s);

        var cmd = new CreateSubscriptionCommand(userId, 100, null);
        var result = await _handler.Handle(cmd, CancellationToken.None);

        _repo.Verify(r => r.CreateAsync(It.IsAny<SubscriptionEntity>(), It.IsAny<CancellationToken>()), Times.Once);
        _events.Verify(e => e.PublishAsync(It.IsAny<SubscriptionCreatedEvent>()), Times.Once);
        Assert.Equal(100, result.Credits);
    }

    [Fact]
    public async Task Handle_ThrowsArgumentException_WhenUserNotFound()
    {
        var userId = Guid.NewGuid();
        _users.Setup(r => r.GetByIdAsync(userId, CancellationToken.None)).ReturnsAsync((UserEntity?)null);

        var cmd = new CreateSubscriptionCommand(userId, 10, null);
        await Assert.ThrowsAsync<ArgumentException>(() => _handler.Handle(cmd, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_SetsExpirationDate_WhenProvided()
    {
        var userId = Guid.NewGuid();
        var expirationDate = DateTime.UtcNow.AddDays(30);
        _users.Setup(r => r.GetByIdAsync(userId, CancellationToken.None))
            .ReturnsAsync(new UserEntity { Id = userId, Email = "test@example.com" });
        _repo.Setup(r => r.CreateAsync(It.IsAny<SubscriptionEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionEntity s, CancellationToken _) => s);

        var cmd = new CreateSubscriptionCommand(userId, 50, expirationDate);
        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.Equal(expirationDate, result.ExpiresAt);
    }
}