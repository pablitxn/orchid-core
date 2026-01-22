using Domain.Entities;
using Domain.Enums;

namespace Domain.Tests.Entities;

public class ChatSessionEntityTests
{
    [Fact]
    public void Properties_AreSetCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var teamId = Guid.NewGuid();

        var session = new ChatSessionEntity
        {
            Id = id,
            UserId = userId,
            AgentId = agentId,
            TeamId = teamId,
            InteractionType = InteractionType.Voice,
            SessionId = "session-123",
            Title = "Test Session",
            IsArchived = true
        };

        // Assert
        Assert.Equal(id, session.Id);
        Assert.Equal(userId, session.UserId);
        Assert.Equal(agentId, session.AgentId);
        Assert.Equal(teamId, session.TeamId);
        Assert.Equal(InteractionType.Voice, session.InteractionType);
        Assert.Equal("session-123", session.SessionId);
        Assert.Equal("Test Session", session.Title);
        Assert.True(session.IsArchived);
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Arrange
        var session = new ChatSessionEntity();

        // Assert
        Assert.Equal(InteractionType.Text, session.InteractionType);
        Assert.Equal(string.Empty, session.SessionId);
        Assert.False(session.IsArchived);
        Assert.Null(session.AgentId);
        Assert.Null(session.TeamId);
    }

    [Fact]
    public void NavigationProperties_CanBeSet()
    {
        // Arrange
        var agent = new AgentEntity { Id = Guid.NewGuid(), Name = "Test Agent" };
        var team = new TeamEntity { Id = Guid.NewGuid(), Name = "Test Team" };

        var session = new ChatSessionEntity
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            AgentId = agent.Id,
            Agent = agent,
            TeamId = team.Id,
            Team = team
        };

        // Assert
        Assert.Equal(agent, session.Agent);
        Assert.Equal(team, session.Team);
    }
}
