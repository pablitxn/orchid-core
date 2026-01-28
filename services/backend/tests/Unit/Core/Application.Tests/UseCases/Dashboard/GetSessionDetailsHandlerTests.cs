using Application.Interfaces;
using Application.UseCases.Dashboard.GetSessionDetails;
using Core.Application.Interfaces;
using Core.Application.UseCases.Dashboard.GetSessionDetails;
using Domain.Entities;
using Moq;
using Xunit;

namespace Application.Tests.UseCases.Dashboard;

public class GetSessionDetailsHandlerTests
{
    private readonly Mock<IChatSessionRepository> _chatSessionRepositoryMock;
    private readonly Mock<IMessageRepository> _messageRepositoryMock;
    private readonly Mock<ICreditConsumptionRepository> _creditConsumptionRepositoryMock;
    private readonly GetSessionDetailsHandler _handler;

    public GetSessionDetailsHandlerTests()
    {
        _chatSessionRepositoryMock = new Mock<IChatSessionRepository>();
        _messageRepositoryMock = new Mock<IMessageRepository>();
        _creditConsumptionRepositoryMock = new Mock<ICreditConsumptionRepository>();
        _handler = new GetSessionDetailsHandler(
            _chatSessionRepositoryMock.Object,
            _messageRepositoryMock.Object,
            _creditConsumptionRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_ReturnsSessionDetails_WhenSessionsExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var query = new GetSessionDetailsQuery(userId);

        var sessions = new List<ChatSessionEntity>
        {
            new()
            {
                Id = sessionId,
                SessionId = "session-123",
                UserId = userId,
                AgentId = agentId,
                Title = "Test Session",
                CreatedAt = DateTime.UtcNow.AddHours(-2),
                IsArchived = false,
                Agent = new AgentEntity
                {
                    Id = agentId,
                    Name = "Test Agent"
                }
            }
        };

        var messageTimestamps = new List<DateTime>
        {
            DateTime.UtcNow.AddHours(-2),
            DateTime.UtcNow.AddHours(-1),
            DateTime.UtcNow
        };

        _chatSessionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        _messageRepositoryMock
            .Setup(x => x.GetCountBySessionIdAsync("session-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);

        _messageRepositoryMock
            .Setup(x => x.GetMessageTimestampsBySessionIdAsync("session-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(messageTimestamps);

        _creditConsumptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CreditConsumptionEntity>());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("Test Session", result[0].Title);
        Assert.Equal(10, result[0].MessageCount);
        Assert.Equal("Test Agent", result[0].AgentName);
    }

    [Fact]
    public async Task Handle_FiltersArchivedSessions_WhenSpecified()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetSessionDetailsQuery(userId, IsArchived: false);

        var sessions = new List<ChatSessionEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                SessionId = "active-session",
                UserId = userId,
                Title = "Active Session",
                IsArchived = false,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                SessionId = "archived-session",
                UserId = userId,
                Title = "Archived Session",
                IsArchived = true,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            }
        };

        _chatSessionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        _messageRepositoryMock
            .Setup(x => x.GetCountBySessionIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _messageRepositoryMock
            .Setup(x => x.GetMessageTimestampsBySessionIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DateTime>());

        _creditConsumptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CreditConsumptionEntity>());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Single(result);
        Assert.Equal("Active Session", result[0].Title);
        Assert.False(result[0].IsArchived);
    }

    [Fact]
    public async Task Handle_FiltersByAgentId_WhenSpecified()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var targetAgentId = Guid.NewGuid();
        var otherAgentId = Guid.NewGuid();
        var query = new GetSessionDetailsQuery(userId, AgentId: targetAgentId);

        var sessions = new List<ChatSessionEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                SessionId = "target-session",
                UserId = userId,
                AgentId = targetAgentId,
                Title = "Target Agent Session",
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                SessionId = "other-session",
                UserId = userId,
                AgentId = otherAgentId,
                Title = "Other Agent Session",
                CreatedAt = DateTime.UtcNow
            }
        };

        _chatSessionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        _messageRepositoryMock
            .Setup(x => x.GetCountBySessionIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _messageRepositoryMock
            .Setup(x => x.GetMessageTimestampsBySessionIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DateTime>());

        _creditConsumptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CreditConsumptionEntity>());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Single(result);
        Assert.Equal("Target Agent Session", result[0].Title);
    }

    [Fact]
    public async Task Handle_ReturnsEmptyList_WhenNoSessionsExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetSessionDetailsQuery(userId);

        _chatSessionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatSessionEntity>());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_PaginatesResults_Correctly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetSessionDetailsQuery(userId, PageNumber: 2, PageSize: 2);

        var sessions = Enumerable.Range(1, 5).Select(i => new ChatSessionEntity
        {
            Id = Guid.NewGuid(),
            SessionId = $"session-{i}",
            UserId = userId,
            Title = $"Session {i}",
            CreatedAt = DateTime.UtcNow.AddHours(-i)
        }).ToList();

        _chatSessionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        _messageRepositoryMock
            .Setup(x => x.GetCountBySessionIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _messageRepositoryMock
            .Setup(x => x.GetMessageTimestampsBySessionIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DateTime>());

        _creditConsumptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CreditConsumptionEntity>());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        // Page 2 should have sessions 3 and 4 (0-indexed: skip 2, take 2)
        Assert.Equal("Session 3", result[0].Title);
        Assert.Equal("Session 4", result[1].Title);
    }
}
