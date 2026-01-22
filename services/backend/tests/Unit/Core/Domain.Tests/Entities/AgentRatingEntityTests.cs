using Domain.Entities;

namespace Domain.Tests.Entities;

public class AgentRatingEntityTests
{
    [Fact]
    public void Properties_AreSetCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var rating = new AgentRatingEntity
        {
            Id = id,
            AgentId = agentId,
            UserId = userId,
            Rating = 5,
            Comment = "Excellent agent!"
        };

        // Assert
        Assert.Equal(id, rating.Id);
        Assert.Equal(agentId, rating.AgentId);
        Assert.Equal(userId, rating.UserId);
        Assert.Equal(5, rating.Rating);
        Assert.Equal("Excellent agent!", rating.Comment);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void AllRatingValues_CanBeSet(int ratingValue)
    {
        // Arrange & Act
        var rating = new AgentRatingEntity
        {
            Id = Guid.NewGuid(),
            AgentId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Rating = ratingValue
        };

        // Assert
        Assert.Equal(ratingValue, rating.Rating);
    }

    [Fact]
    public void Comment_IsOptional()
    {
        // Arrange
        var rating = new AgentRatingEntity
        {
            Id = Guid.NewGuid(),
            AgentId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Rating = 4,
            Comment = null
        };

        // Assert
        Assert.Null(rating.Comment);
    }

    [Fact]
    public void NavigationProperties_CanBeSet()
    {
        // Arrange
        var agent = new AgentEntity { Id = Guid.NewGuid(), Name = "Test Agent" };
        var user = new UserEntity { Id = Guid.NewGuid(), Email = "test@example.com" };

        var rating = new AgentRatingEntity
        {
            Id = Guid.NewGuid(),
            AgentId = agent.Id,
            Agent = agent,
            UserId = user.Id,
            User = user,
            Rating = 5
        };

        // Assert
        Assert.Equal(agent, rating.Agent);
        Assert.Equal(user, rating.User);
    }
}
