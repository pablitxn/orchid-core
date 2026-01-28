using Application.Interfaces;
using Application.UseCases.ChatSession.GetChatSessionById;
using Domain.Entities;
using Domain.Enums;
using Moq;
using Xunit;

namespace Application.Tests.UseCases.ChatSession;

public class GetChatSessionByIdHandlerTests
{
    private readonly Mock<IChatSessionRepository> _chatSessionRepositoryMock;
    private readonly Mock<IPluginRepository> _pluginRepositoryMock;
    private readonly GetChatSessionByIdHandler _handler;

    public GetChatSessionByIdHandlerTests()
    {
        _chatSessionRepositoryMock = new Mock<IChatSessionRepository>();
        _pluginRepositoryMock = new Mock<IPluginRepository>();
        _handler = new GetChatSessionByIdHandler(
            _chatSessionRepositoryMock.Object,
            _pluginRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenSessionNotFound()
    {
        // Arrange
        var sessionId = "test-session-123";
        var query = new GetChatSessionByIdQuery(sessionId);

        _chatSessionRepositoryMock
            .Setup(x => x.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChatSessionEntity?)null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_ReturnsSessionDto_WhenSessionExists()
    {
        // Arrange
        var sessionId = "test-session-123";
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var query = new GetChatSessionByIdQuery(sessionId);

        var session = new ChatSessionEntity
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            UserId = userId,
            AgentId = agentId,
            Title = "Test Session",
            InteractionType = InteractionType.Text,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            UpdatedAt = DateTime.UtcNow,
            Agent = new AgentEntity
            {
                Id = agentId,
                Name = "Test Agent",
                Description = "Test Description",
                Model = "gpt-4",
                IsPublic = true,
                UserId = userId
            }
        };

        var plugins = new List<PluginEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Test Plugin",
                Description = "Plugin Description",
                PriceCredits = 10,
                IsActive = true
            }
        };

        _chatSessionRepositoryMock
            .Setup(x => x.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _pluginRepositoryMock
            .Setup(x => x.ListByAgentAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugins);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(sessionId, result.SessionId);
        Assert.Equal(userId, result.UserId);
        Assert.Equal("Test Session", result.Title);
        Assert.NotNull(result.Agent);
        Assert.Equal("Test Agent", result.Agent.Name);
        Assert.Single(result.Agent.Plugins);
    }

    [Fact]
    public async Task Handle_ReturnsSessionWithTeam_WhenTeamExists()
    {
        // Arrange
        var sessionId = "test-session-456";
        var userId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var query = new GetChatSessionByIdQuery(sessionId);

        var session = new ChatSessionEntity
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            UserId = userId,
            TeamId = teamId,
            Title = "Team Session",
            InteractionType = InteractionType.Text,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Team = new TeamEntity
            {
                Id = teamId,
                Name = "Test Team",
                Description = "Team Description"
            }
        };

        _chatSessionRepositoryMock
            .Setup(x => x.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Team);
        Assert.Equal("Test Team", result.Team.Name);
        Assert.Null(result.Agent);
    }

    [Fact]
    public async Task Handle_ReturnsSessionWithoutAgentOrTeam_WhenBothNull()
    {
        // Arrange
        var sessionId = "test-session-789";
        var userId = Guid.NewGuid();
        var query = new GetChatSessionByIdQuery(sessionId);

        var session = new ChatSessionEntity
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            UserId = userId,
            Title = "Solo Session",
            InteractionType = InteractionType.Text,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _chatSessionRepositoryMock
            .Setup(x => x.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.Agent);
        Assert.Null(result.Team);
    }
}
