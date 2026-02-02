using Application.Interfaces;
using Application.UseCases.Subscription.AddCredits;
using Domain.Entities;
using Domain.Events;
using Moq;

namespace Application.Tests.UseCases.Subscription;

public class AddCreditsHandlerTests
{
    private readonly Mock<IEventPublisher> _events = new();
    private readonly AddCreditsHandler _handler;
    private readonly Mock<IUserCreditsRepository> _userCreditsRepo = new();
    private readonly Mock<ISubscriptionRepository> _subscriptionRepo = new();

    public AddCreditsHandlerTests()
    {
        _handler = new AddCreditsHandler(_userCreditsRepo.Object, _subscriptionRepo.Object, _events.Object);
    }

    [Fact]
    public async Task Handle_AddsCredits_AndPublishesEvent()
    {
        var userId = Guid.NewGuid();
        var userCredits = new UserCreditsEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AvailableCredits = 5,
            TotalCreditsGranted = 5,
            TotalCreditsConsumed = 0
        };

        _userCreditsRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(userCredits);

        var cmd = new AddCreditsCommand(userId, 5);
        var result = await _handler.Handle(cmd, CancellationToken.None);

        _userCreditsRepo.Verify(r => r.UpdateAsync(It.IsAny<UserCreditsEntity>(), It.IsAny<CancellationToken>()), Times.Once);
        _events.Verify(e => e.PublishAsync(It.IsAny<CreditsAddedEvent>()), Times.Once);
        Assert.Equal(10, result.Credits);
    }

    [Fact]
    public async Task Handle_CreatesUserCredits_WhenNotFound()
    {
        var userId = Guid.NewGuid();
        _userCreditsRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserCreditsEntity?)null);

        UserCreditsEntity? createdCredits = null;
        _userCreditsRepo.Setup(r => r.CreateAsync(It.IsAny<UserCreditsEntity>(), It.IsAny<CancellationToken>()))
            .Callback<UserCreditsEntity, CancellationToken>((uc, _) => createdCredits = uc)
            .ReturnsAsync((UserCreditsEntity uc, CancellationToken _) => uc);

        var cmd = new AddCreditsCommand(userId, 5);
        var result = await _handler.Handle(cmd, CancellationToken.None);

        _userCreditsRepo.Verify(r => r.CreateAsync(It.IsAny<UserCreditsEntity>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(createdCredits);
        Assert.Equal(userId, createdCredits.UserId);
        Assert.Equal(5, result.Credits);
    }
}
