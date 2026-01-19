using Application.Interfaces;
using Application.UseCases.Agent.GetAgentRatings;
using Domain.Entities;
using Moq;

namespace Application.Tests.UseCases.Agent;

public class GetAgentRatingsHandlerTests
{
    private readonly GetAgentRatingsHandler _handler;
    private readonly Mock<IAgentRepository> _agentRepositoryMock;
    private readonly Mock<IAgentRatingRepository> _ratingRepositoryMock;

    public GetAgentRatingsHandlerTests()
    {
        _agentRepositoryMock = new Mock<IAgentRepository>();
        _ratingRepositoryMock = new Mock<IAgentRatingRepository>();
        _handler = new GetAgentRatingsHandler(_agentRepositoryMock.Object, _ratingRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_Should_Return_All_Ratings_For_Agent()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var query = new GetAgentRatingsQuery(agentId);

        var agent = new AgentEntity { Id = agentId, Name = "Test Agent" };
        _agentRepositoryMock.Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        var ratings = new List<AgentRatingEntity>
        {
            new() { Id = Guid.NewGuid(), AgentId = agentId, UserId = Guid.NewGuid(), Rating = 5, Comment = "Excellent!", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), AgentId = agentId, UserId = Guid.NewGuid(), Rating = 4, Comment = "Very good", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), AgentId = agentId, UserId = Guid.NewGuid(), Rating = 3, Comment = null, CreatedAt = DateTime.UtcNow }
        };

        _ratingRepositoryMock.Setup(x => x.ListByAgentAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ratings);

        _ratingRepositoryMock.Setup(x => x.GetAverageRatingAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(4.0);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(agentId, result.AgentId);
        Assert.Equal(4.0, result.AverageRating);
        Assert.Equal(3, result.TotalRatings);
        Assert.Equal(3, result.Ratings.Count);
    }

    [Fact]
    public async Task Handle_Should_Return_Empty_List_When_No_Ratings_Exist()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var query = new GetAgentRatingsQuery(agentId);

        var agent = new AgentEntity { Id = agentId, Name = "Test Agent" };
        _agentRepositoryMock.Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        _ratingRepositoryMock.Setup(x => x.ListByAgentAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentRatingEntity>());

        _ratingRepositoryMock.Setup(x => x.GetAverageRatingAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((double?)null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(agentId, result.AgentId);
        Assert.Null(result.AverageRating);
        Assert.Equal(0, result.TotalRatings);
        Assert.Empty(result.Ratings);
    }

    [Fact]
    public async Task Handle_Should_Throw_When_Agent_Not_Found()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var query = new GetAgentRatingsQuery(agentId);

        _agentRepositoryMock.Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentEntity?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(query, CancellationToken.None));
        Assert.Contains(agentId.ToString(), exception.Message);
    }

    [Fact]
    public async Task Handle_Should_Throw_When_AgentId_Is_Empty()
    {
        // Arrange
        var query = new GetAgentRatingsQuery(Guid.Empty);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _handler.Handle(query, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_Should_Return_Correct_Average_Rating()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var query = new GetAgentRatingsQuery(agentId);

        var agent = new AgentEntity { Id = agentId, Name = "Test Agent" };
        _agentRepositoryMock.Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        var ratings = new List<AgentRatingEntity>
        {
            new() { Id = Guid.NewGuid(), AgentId = agentId, UserId = Guid.NewGuid(), Rating = 5 },
            new() { Id = Guid.NewGuid(), AgentId = agentId, UserId = Guid.NewGuid(), Rating = 4 },
            new() { Id = Guid.NewGuid(), AgentId = agentId, UserId = Guid.NewGuid(), Rating = 5 },
            new() { Id = Guid.NewGuid(), AgentId = agentId, UserId = Guid.NewGuid(), Rating = 5 },
            new() { Id = Guid.NewGuid(), AgentId = agentId, UserId = Guid.NewGuid(), Rating = 1 }
        };

        _ratingRepositoryMock.Setup(x => x.ListByAgentAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ratings);

        _ratingRepositoryMock.Setup(x => x.GetAverageRatingAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(4.0); // (5+4+5+5+1)/5 = 4.0

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(4.0, result.AverageRating);
        Assert.Equal(5, result.TotalRatings);
    }

    [Fact]
    public async Task Handle_Should_Include_All_Rating_Details()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var ratingId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow.AddDays(-1);
        var updatedAt = DateTime.UtcNow;
        var query = new GetAgentRatingsQuery(agentId);

        var agent = new AgentEntity { Id = agentId, Name = "Test Agent" };
        _agentRepositoryMock.Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        var rating = new AgentRatingEntity
        {
            Id = ratingId,
            AgentId = agentId,
            UserId = userId,
            Rating = 5,
            Comment = "Best agent ever!",
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };

        _ratingRepositoryMock.Setup(x => x.ListByAgentAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([rating]);

        _ratingRepositoryMock.Setup(x => x.GetAverageRatingAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5.0);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        var returnedRating = result.Ratings.Single();
        Assert.Equal(ratingId, returnedRating.Id);
        Assert.Equal(agentId, returnedRating.AgentId);
        Assert.Equal(userId, returnedRating.UserId);
        Assert.Equal(5, returnedRating.Rating);
        Assert.Equal("Best agent ever!", returnedRating.Comment);
        Assert.Equal(createdAt, returnedRating.CreatedAt);
        Assert.Equal(updatedAt, returnedRating.UpdatedAt);
    }
}
