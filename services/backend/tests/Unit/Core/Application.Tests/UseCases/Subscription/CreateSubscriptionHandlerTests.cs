using Application.Interfaces;
using Application.UseCases.Subscription.CreateSubscription;
using Domain.Entities;
using Domain.Events;
using Domain.Exceptions;
using Moq;

namespace Application.Tests.UseCases.Subscription;

public class CreateSubscriptionHandlerTests
{
    private readonly Mock<IEventPublisher> _events = new();
    private readonly CreateSubscriptionHandler _handler;
    private readonly Mock<ISubscriptionRepository> _repo = new();
    private readonly Mock<IUserCreditsRepository> _userCreditsRepo = new();
    private readonly Mock<IUserRepository> _users = new();

    public CreateSubscriptionHandlerTests()
    {
        _handler = new CreateSubscriptionHandler(
            _repo.Object,
            _userCreditsRepo.Object,
            _users.Object,
            _events.Object);
    }

    [Fact]
    public async Task Handle_CreatesSubscription_AndPublishesEvent()
    {
        var userId = Guid.NewGuid();
        _users.Setup(r => r.GetByIdAsync(userId, CancellationToken.None))
            .ReturnsAsync(new UserEntity { Id = userId, Email = "test@example.com" });
        _repo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionEntity?)null);
        _repo.Setup(r => r.CreateAsync(It.IsAny<SubscriptionEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionEntity s, CancellationToken _) => s);
        _userCreditsRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserCreditsEntity?)null);
        _userCreditsRepo.Setup(r => r.CreateAsync(It.IsAny<UserCreditsEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserCreditsEntity uc, CancellationToken _) => uc);

        var cmd = new CreateSubscriptionCommand(userId, 100, null);
        var result = await _handler.Handle(cmd, CancellationToken.None);

        _repo.Verify(r => r.CreateAsync(It.IsAny<SubscriptionEntity>(), It.IsAny<CancellationToken>()), Times.Once);
        _userCreditsRepo.Verify(r => r.CreateAsync(It.IsAny<UserCreditsEntity>(), It.IsAny<CancellationToken>()), Times.Once);
        _events.Verify(e => e.PublishAsync(It.IsAny<SubscriptionCreatedEvent>()), Times.Once);
        Assert.Equal(100, result.Credits);
    }

    [Fact]
    public async Task Handle_ThrowsArgumentException_WhenUserNotFound()
    {
        var userId = Guid.NewGuid();
        _users.Setup(r => r.GetByIdAsync(userId, CancellationToken.None)).ReturnsAsync((UserEntity?)null);

        var cmd = new CreateSubscriptionCommand(userId, 10, null);
        await Assert.ThrowsAsync<UserNotFoundException>(() => _handler.Handle(cmd, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_SetsExpirationDate_WhenProvided()
    {
        var userId = Guid.NewGuid();
        var expirationDate = DateTime.UtcNow.AddDays(30);
        _users.Setup(r => r.GetByIdAsync(userId, CancellationToken.None))
            .ReturnsAsync(new UserEntity { Id = userId, Email = "test@example.com" });
        _repo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionEntity?)null);
        _repo.Setup(r => r.CreateAsync(It.IsAny<SubscriptionEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionEntity s, CancellationToken _) => s);
        _userCreditsRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserCreditsEntity?)null);
        _userCreditsRepo.Setup(r => r.CreateAsync(It.IsAny<UserCreditsEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserCreditsEntity uc, CancellationToken _) => uc);

        var cmd = new CreateSubscriptionCommand(userId, 50, expirationDate);
        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.Equal(expirationDate, result.ExpiresAt);
    }

    [Fact]
    public async Task Handle_AddsCreditsToExistingUserCredits_WhenUserCreditsExist()
    {
        var userId = Guid.NewGuid();
        var existingCredits = new UserCreditsEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AvailableCredits = 50,
            TotalCreditsGranted = 50,
            TotalCreditsConsumed = 0
        };
        _users.Setup(r => r.GetByIdAsync(userId, CancellationToken.None))
            .ReturnsAsync(new UserEntity { Id = userId, Email = "test@example.com" });
        _repo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionEntity?)null);
        _repo.Setup(r => r.CreateAsync(It.IsAny<SubscriptionEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionEntity s, CancellationToken _) => s);
        _userCreditsRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingCredits);
        _userCreditsRepo.Setup(r => r.UpdateAsync(It.IsAny<UserCreditsEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var cmd = new CreateSubscriptionCommand(userId, 100, null);
        var result = await _handler.Handle(cmd, CancellationToken.None);

        _userCreditsRepo.Verify(r => r.UpdateAsync(It.Is<UserCreditsEntity>(uc => uc.AvailableCredits == 150), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(150, result.Credits);
    }

    [Fact]
    public async Task Handle_ThrowsValidationException_WhenCreditsNegative()
    {
        var userId = Guid.NewGuid();
        _users.Setup(r => r.GetByIdAsync(userId, CancellationToken.None))
            .ReturnsAsync(new UserEntity { Id = userId, Email = "test@example.com" });

        var cmd = new CreateSubscriptionCommand(userId, -10, null);
        await Assert.ThrowsAsync<ValidationException>(() => _handler.Handle(cmd, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ThrowsValidationException_WhenExpirationInPast()
    {
        var userId = Guid.NewGuid();
        _users.Setup(r => r.GetByIdAsync(userId, CancellationToken.None))
            .ReturnsAsync(new UserEntity { Id = userId, Email = "test@example.com" });

        var cmd = new CreateSubscriptionCommand(userId, 10, DateTime.UtcNow.AddDays(-1));
        await Assert.ThrowsAsync<ValidationException>(() => _handler.Handle(cmd, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ThrowsAlreadySubscribedException_WhenActiveSubscriptionExists()
    {
        var userId = Guid.NewGuid();
        var existingSub = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        };
        _users.Setup(r => r.GetByIdAsync(userId, CancellationToken.None))
            .ReturnsAsync(new UserEntity { Id = userId, Email = "test@example.com" });
        _repo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSub);

        var cmd = new CreateSubscriptionCommand(userId, 10, null);
        await Assert.ThrowsAsync<AlreadySubscribedException>(() => _handler.Handle(cmd, CancellationToken.None));
    }
}
