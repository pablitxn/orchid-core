using Application.Interfaces;
using Application.UseCases.AdminBilling.GetAllSubscriptions;
using Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;

namespace Application.Tests.UseCases.AdminBilling;

public class GetAllSubscriptionsHandlerTests
{
    private readonly Mock<ISubscriptionRepository> _repo = new();
    private readonly Mock<IUserCreditsRepository> _userCreditsRepo = new();
    private readonly Mock<ILogger<GetAllSubscriptionsHandler>> _logger = new();
    private readonly GetAllSubscriptionsHandler _handler;

    public GetAllSubscriptionsHandlerTests()
    {
        _userCreditsRepo
            .Setup(r => r.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserCreditsEntity?)null);

        _handler = new GetAllSubscriptionsHandler(_repo.Object, _userCreditsRepo.Object, _logger.Object);
    }

    [Fact]
    public async Task Handle_ReturnsSubscriptionsWithPagination()
    {
        var subscriptions = new List<SubscriptionEntity>
        {
            new() { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Credits = 100 },
            new() { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Credits = 200 }
        };

        _repo.Setup(r => r.GetAllAsync(1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync((subscriptions.AsEnumerable(), 2));

        var query = new GetAllSubscriptionsQuery(1, 50);
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(1, result.Page);
        Assert.Equal(50, result.PageSize);
    }

    [Fact]
    public async Task Handle_ReturnsEmptyList_WhenNoSubscriptions()
    {
        _repo.Setup(r => r.GetAllAsync(1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Enumerable.Empty<SubscriptionEntity>(), 0));

        var query = new GetAllSubscriptionsQuery(1, 50);
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task Handle_CalculatesTotalPages_Correctly()
    {
        var subscriptions = new List<SubscriptionEntity>
        {
            new() { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Credits = 100 }
        };

        _repo.Setup(r => r.GetAllAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((subscriptions.AsEnumerable(), 25));

        var query = new GetAllSubscriptionsQuery(1, 10);
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.Equal(3, result.TotalPages);
    }
}
