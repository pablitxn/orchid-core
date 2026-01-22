using Domain.Entities;

namespace Domain.Tests.Entities;

public class TeamAgentEntityTests
{
    [Fact]
    public void Properties_AreSetCorrectly()
    {
        // Arrange
        var teamId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        var teamAgent = new TeamAgentEntity
        {
            TeamId = teamId,
            AgentId = agentId,
            Role = "Leader",
            Order = 1
        };

        // Assert
        Assert.Equal(teamId, teamAgent.TeamId);
        Assert.Equal(agentId, teamAgent.AgentId);
        Assert.Equal("Leader", teamAgent.Role);
        Assert.Equal(1, teamAgent.Order);
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Arrange
        var teamAgent = new TeamAgentEntity();

        // Assert
        Assert.Equal(string.Empty, teamAgent.Role);
        Assert.Equal(0, teamAgent.Order);
    }

    [Fact]
    public void NavigationProperties_CanBeSet()
    {
        // Arrange
        var team = new TeamEntity { Id = Guid.NewGuid(), Name = "Test Team" };
        var agent = new AgentEntity { Id = Guid.NewGuid(), Name = "Test Agent" };

        var teamAgent = new TeamAgentEntity
        {
            TeamId = team.Id,
            Team = team,
            AgentId = agent.Id,
            Agent = agent,
            Role = "Member",
            Order = 2
        };

        // Assert
        Assert.Equal(team, teamAgent.Team);
        Assert.Equal(agent, teamAgent.Agent);
    }
}
