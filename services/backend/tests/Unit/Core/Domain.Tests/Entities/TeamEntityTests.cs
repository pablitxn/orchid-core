using Domain.Entities;
using Domain.Enums;

namespace Domain.Tests.Entities;

public class TeamEntityTests
{
    [Fact]
    public void Properties_AreSetCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();

        var team = new TeamEntity
        {
            Id = id,
            Name = "Test Team",
            Description = "A test team",
            Policy = TeamInteractionPolicy.LeaderDriven
        };

        // Assert
        Assert.Equal(id, team.Id);
        Assert.Equal("Test Team", team.Name);
        Assert.Equal("A test team", team.Description);
        Assert.Equal(TeamInteractionPolicy.LeaderDriven, team.Policy);
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Arrange
        var team = new TeamEntity();

        // Assert
        Assert.Equal(string.Empty, team.Name);
        Assert.Null(team.Description);
        Assert.Equal(TeamInteractionPolicy.Open, team.Policy);
        Assert.Empty(team.TeamAgents);
    }

    [Fact]
    public void TeamAgents_CanBeModified()
    {
        // Arrange
        var team = new TeamEntity { Id = Guid.NewGuid(), Name = "Test Team" };
        var teamAgent = new TeamAgentEntity
        {
            TeamId = team.Id,
            AgentId = Guid.NewGuid(),
            Role = "Leader",
            Order = 1
        };

        // Act
        team.TeamAgents.Add(teamAgent);

        // Assert
        Assert.Single(team.TeamAgents);
        Assert.Equal(teamAgent, team.TeamAgents[0]);
    }
}
