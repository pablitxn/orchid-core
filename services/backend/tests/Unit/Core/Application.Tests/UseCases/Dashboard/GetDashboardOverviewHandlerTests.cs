using Application.Interfaces;
using Core.Application.Interfaces;
using Core.Application.UseCases.Dashboard.GetDashboardOverview;
using Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Application.Tests.UseCases.Dashboard;

public class GetDashboardOverviewHandlerTests
{
    private readonly Mock<ISubscriptionRepository> _subscriptionRepositoryMock;
    private readonly Mock<IUserCreditsRepository> _userCreditsRepositoryMock;
    private readonly Mock<ICreditConsumptionRepository> _creditConsumptionRepositoryMock;
    private readonly Mock<ILogger<GetDashboardOverviewHandler>> _loggerMock;
    private readonly GetDashboardOverviewHandler _handler;

    public GetDashboardOverviewHandlerTests()
    {
        _subscriptionRepositoryMock = new Mock<ISubscriptionRepository>();
        _userCreditsRepositoryMock = new Mock<IUserCreditsRepository>();
        _creditConsumptionRepositoryMock = new Mock<ICreditConsumptionRepository>();
        _loggerMock = new Mock<ILogger<GetDashboardOverviewHandler>>();
        _userCreditsRepositoryMock
            .Setup(x => x.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserCreditsEntity?)null);
        _handler = new GetDashboardOverviewHandler(
            _subscriptionRepositoryMock.Object,
            _userCreditsRepositoryMock.Object,
            _creditConsumptionRepositoryMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_ReturnsDashboardOverview_WithSubscription()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetDashboardOverviewQuery(userId);

        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AutoRenew = true
        };

        var userCredits = new UserCreditsEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AvailableCredits = 500,
            TotalCreditsGranted = 500,
            TotalCreditsConsumed = 0
        };

        var creditHistory = new List<CreditConsumptionEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ConsumptionType = "plugin",
                ResourceName = "Test Plugin",
                CreditsConsumed = 10,
                ConsumedAt = DateTime.UtcNow.AddHours(-1)
            }
        };

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);
        _userCreditsRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userCredits);

        _creditConsumptionRepositoryMock
            .Setup(x => x.GetRecentByUserIdAsync(userId, 30, It.IsAny<CancellationToken>()))
            .ReturnsAsync(creditHistory);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Credits);
        Assert.Equal(500, result.Credits.CurrentBalance);
        Assert.NotNull(result.Subscription);
    }

    [Fact]
    public async Task Handle_ReturnsDashboardOverview_WithoutSubscription()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetDashboardOverviewQuery(userId);

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionEntity?)null);
        _userCreditsRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserCreditsEntity?)null);

        _creditConsumptionRepositoryMock
            .Setup(x => x.GetRecentByUserIdAsync(userId, 30, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CreditConsumptionEntity>());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Credits);
        Assert.Equal(0, result.Credits.CurrentBalance);
        Assert.Equal("Free", result.Subscription.PlanName);
    }

    [Fact]
    public async Task Handle_CalculatesCreditMetrics_Correctly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetDashboardOverviewQuery(userId);
        var now = DateTime.UtcNow;

        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId
        };

        var userCredits = new UserCreditsEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AvailableCredits = 100,
            TotalCreditsGranted = 100,
            TotalCreditsConsumed = 0
        };

        var creditHistory = new List<CreditConsumptionEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ConsumptionType = "plugin",
                ResourceName = "Plugin A",
                CreditsConsumed = 10,
                ConsumedAt = now.Date // Today
            },
            new()
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ConsumptionType = "message",
                ResourceName = "Message",
                CreditsConsumed = 5,
                ConsumedAt = now.AddDays(-3) // This week
            },
            new()
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ConsumptionType = "plugin",
                ResourceName = "Plugin B",
                CreditsConsumed = 15,
                ConsumedAt = now.AddDays(-20) // This month (assuming same month)
            }
        };

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);
        _userCreditsRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userCredits);

        _creditConsumptionRepositoryMock
            .Setup(x => x.GetRecentByUserIdAsync(userId, 30, It.IsAny<CancellationToken>()))
            .ReturnsAsync(creditHistory);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(10, result.Credits.ConsumedToday);
        Assert.True(result.Credits.ConsumedThisWeek >= 10); // At least today's consumption
    }

    [Fact]
    public async Task Handle_CalculatesLowCreditAlert_WhenBelowThreshold()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetDashboardOverviewQuery(userId);

        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId
        };

        var userCredits = new UserCreditsEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AvailableCredits = 50, // Below threshold of 100
            TotalCreditsGranted = 50,
            TotalCreditsConsumed = 0
        };

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);
        _userCreditsRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userCredits);

        _creditConsumptionRepositoryMock
            .Setup(x => x.GetRecentByUserIdAsync(userId, 30, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CreditConsumptionEntity>());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Credits.LowCreditAlert);
    }

    [Fact]
    public async Task Handle_ThrowsException_OnError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetDashboardOverviewQuery(userId);

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => _handler.Handle(query, CancellationToken.None));
    }
}
