using Application.Interfaces;
using Core.Application.Interfaces;
using Core.Application.UseCases.Dashboard.GetCreditHistory;
using Domain.Entities;
using Moq;
using Xunit;

namespace Application.Tests.UseCases.Dashboard;

public class GetCreditHistoryHandlerTests
{
    private readonly Mock<ICreditConsumptionRepository> _creditConsumptionRepositoryMock;
    private readonly GetCreditHistoryHandler _handler;

    public GetCreditHistoryHandlerTests()
    {
        _creditConsumptionRepositoryMock = new Mock<ICreditConsumptionRepository>();
        _handler = new GetCreditHistoryHandler(_creditConsumptionRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_ReturnsCreditHistory_WhenRecordsExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetCreditHistoryQuery(userId);

        var creditHistory = new List<CreditConsumptionEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ConsumptionType = "plugin",
                ResourceName = "Test Plugin",
                CreditsConsumed = 10,
                BalanceAfter = 90,
                ConsumedAt = DateTime.UtcNow.AddHours(-1)
            },
            new()
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ConsumptionType = "message",
                ResourceName = "Chat Message",
                CreditsConsumed = 5,
                BalanceAfter = 85,
                ConsumedAt = DateTime.UtcNow
            }
        };

        _creditConsumptionRepositoryMock
            .Setup(x => x.GetByDateRangeAsync(
                userId,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(creditHistory);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(1, result.CurrentPage);
    }

    [Fact]
    public async Task Handle_ReturnsEmptyList_WhenNoRecordsExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetCreditHistoryQuery(userId);

        _creditConsumptionRepositoryMock
            .Setup(x => x.GetByDateRangeAsync(
                userId,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CreditConsumptionEntity>());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task Handle_FiltersByConsumptionType_WhenSpecified()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetCreditHistoryQuery(userId, ConsumptionType: "plugin");

        var creditHistory = new List<CreditConsumptionEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ConsumptionType = "plugin",
                ResourceName = "Test Plugin",
                CreditsConsumed = 10,
                BalanceAfter = 90,
                ConsumedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ConsumptionType = "message",
                ResourceName = "Chat Message",
                CreditsConsumed = 5,
                BalanceAfter = 85,
                ConsumedAt = DateTime.UtcNow
            }
        };

        _creditConsumptionRepositoryMock
            .Setup(x => x.GetByDateRangeAsync(
                userId,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(creditHistory);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Items);
        Assert.Equal("plugin", result.Items[0].ConsumptionType);
    }

    [Fact]
    public async Task Handle_EnforcesPaginationLimits()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetCreditHistoryQuery(userId, PageSize: 1000); // Exceeds max

        var creditHistory = Enumerable.Range(1, 150).Select(i => new CreditConsumptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ConsumptionType = "message",
            ResourceName = $"Message {i}",
            CreditsConsumed = 1,
            BalanceAfter = 100 - i,
            ConsumedAt = DateTime.UtcNow.AddMinutes(-i)
        }).ToList();

        _creditConsumptionRepositoryMock
            .Setup(x => x.GetByDateRangeAsync(
                userId,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(creditHistory);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(100, result.Items.Count); // Max page size enforced
    }

    [Fact]
    public async Task Handle_OrdersByDateDescending()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetCreditHistoryQuery(userId);

        var oldDate = DateTime.UtcNow.AddDays(-5);
        var newDate = DateTime.UtcNow;

        var creditHistory = new List<CreditConsumptionEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ConsumptionType = "plugin",
                ResourceName = "Old Record",
                CreditsConsumed = 10,
                BalanceAfter = 90,
                ConsumedAt = oldDate
            },
            new()
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ConsumptionType = "message",
                ResourceName = "New Record",
                CreditsConsumed = 5,
                BalanceAfter = 85,
                ConsumedAt = newDate
            }
        };

        _creditConsumptionRepositoryMock
            .Setup(x => x.GetByDateRangeAsync(
                userId,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(creditHistory);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("New Record", result.Items[0].ResourceName);
        Assert.Equal("Old Record", result.Items[1].ResourceName);
    }
}
