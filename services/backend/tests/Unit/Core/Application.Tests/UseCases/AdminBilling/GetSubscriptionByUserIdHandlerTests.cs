using Application.Interfaces;
using Application.UseCases.AdminBilling.GetSubscriptionByUserId;
using Domain.Entities;
using Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Application.Tests.UseCases.AdminBilling;

public class GetSubscriptionByUserIdHandlerTests
{
    private readonly Mock<ISubscriptionRepository> _repo = new();
    private readonly Mock<IUserCreditsRepository> _userCreditsRepo = new();
    private readonly Mock<ILogger<GetSubscriptionByUserIdHandler>> _logger = new();
    private readonly GetSubscriptionByUserIdHandler _handler;

    public GetSubscriptionByUserIdHandlerTests()
    {
        _userCreditsRepo
            .Setup(r => r.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserCreditsEntity?)null);

        _handler = new GetSubscriptionByUserIdHandler(_repo.Object, _userCreditsRepo.Object, _logger.Object);
    }

    [Fact]
    public async Task Handle_ReturnsSubscription_WhenFound()
    {
        var userId = Guid.NewGuid();
        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
        };

        var userCredits = new UserCreditsEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AvailableCredits = 100,
            TotalCreditsGranted = 100,
            TotalCreditsConsumed = 0
        };

        _repo.Setup(r => r.GetByUserIdWithPlanAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);
        _userCreditsRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userCredits);

        var query = new GetSubscriptionByUserIdQuery(userId);
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.Equal(userId, result.UserId);
        Assert.Equal(100, result.Credits);
    }

    [Fact]
    public async Task Handle_ThrowsSubscriptionNotFoundException_WhenNotFound()
    {
        var userId = Guid.NewGuid();
        _repo.Setup(r => r.GetByUserIdWithPlanAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionEntity?)null);

        var query = new GetSubscriptionByUserIdQuery(userId);
        await Assert.ThrowsAsync<SubscriptionNotFoundException>(
            () => _handler.Handle(query, CancellationToken.None));
    }
}
