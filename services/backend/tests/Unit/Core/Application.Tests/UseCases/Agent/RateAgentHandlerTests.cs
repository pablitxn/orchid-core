using Application.Interfaces;
using Application.UseCases.Agent.RateAgent;
using Domain.Entities;
using Moq;
using Xunit;

namespace Application.Tests.UseCases.Agent;

public class RateAgentHandlerTests
{
    private readonly RateAgentHandler _handler;
    private readonly Mock<IAgentRepository> _agentRepositoryMock;
    private readonly Mock<IAgentRatingRepository> _ratingRepositoryMock;

    public RateAgentHandlerTests()
    {
        _agentRepositoryMock = new Mock<IAgentRepository>();
        _ratingRepositoryMock = new Mock<IAgentRatingRepository>();
        _handler = new RateAgentHandler(_agentRepositoryMock.Object, _ratingRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_Should_Create_New_Rating_When_User_Has_Not_Rated_Before()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var command = new RateAgentCommand(agentId, userId, 5, "Great agent!");

        var agent = new AgentEntity { Id = agentId, Name = "Test Agent" };
        _agentRepositoryMock.Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        _ratingRepositoryMock.Setup(x => x.GetByAgentAndUserAsync(agentId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentRatingEntity?)null);

        AgentRatingEntity? capturedRating = null;
        _ratingRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<AgentRatingEntity>(), It.IsAny<CancellationToken>()))
            .Callback<AgentRatingEntity, CancellationToken>((rating, _) => capturedRating = rating)
            .Returns(Task.CompletedTask);

        _ratingRepositoryMock.Setup(x => x.GetAverageRatingAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5.0);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedRating);
        Assert.Equal(agentId, capturedRating.AgentId);
        Assert.Equal(userId, capturedRating.UserId);
        Assert.Equal(5, capturedRating.Rating);
        Assert.Equal("Great agent!", capturedRating.Comment);

        Assert.Equal(5, result.Rating);
        Assert.Equal(5.0, result.NewAverageRating);
        _ratingRepositoryMock.Verify(x => x.CreateAsync(It.IsAny<AgentRatingEntity>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Update_Existing_Rating_When_User_Has_Already_Rated()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var ratingId = Guid.NewGuid();
        var command = new RateAgentCommand(agentId, userId, 4, "Updated review");

        var agent = new AgentEntity { Id = agentId, Name = "Test Agent" };
        _agentRepositoryMock.Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        var existingRating = new AgentRatingEntity
        {
            Id = ratingId,
            AgentId = agentId,
            UserId = userId,
            Rating = 3,
            Comment = "Original review",
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };

        _ratingRepositoryMock.Setup(x => x.GetByAgentAndUserAsync(agentId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingRating);

        _ratingRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<AgentRatingEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _ratingRepositoryMock.Setup(x => x.GetAverageRatingAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(4.0);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ratingId, result.Id);
        Assert.Equal(4, result.Rating);
        Assert.Equal("Updated review", result.Comment);
        Assert.Equal(4.0, result.NewAverageRating);
        _ratingRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<AgentRatingEntity>(), It.IsAny<CancellationToken>()), Times.Once);
        _ratingRepositoryMock.Verify(x => x.CreateAsync(It.IsAny<AgentRatingEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Throw_When_Agent_Not_Found()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var command = new RateAgentCommand(agentId, userId, 5, null);

        _agentRepositoryMock.Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentEntity?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, CancellationToken.None));
        Assert.Contains(agentId.ToString(), exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    [InlineData(100)]
    public async Task Handle_Should_Throw_When_Rating_Is_Invalid(int invalidRating)
    {
        // Arrange
        var command = new RateAgentCommand(Guid.NewGuid(), Guid.NewGuid(), invalidRating, null);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_Should_Throw_When_AgentId_Is_Empty()
    {
        // Arrange
        var command = new RateAgentCommand(Guid.Empty, Guid.NewGuid(), 5, null);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_Should_Throw_When_UserId_Is_Empty()
    {
        // Arrange
        var command = new RateAgentCommand(Guid.NewGuid(), Guid.Empty, 5, null);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_Should_Throw_When_Comment_Exceeds_Max_Length()
    {
        // Arrange
        var longComment = new string('a', 1001);
        var command = new RateAgentCommand(Guid.NewGuid(), Guid.NewGuid(), 5, longComment);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_Should_Allow_Rating_Without_Comment()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var command = new RateAgentCommand(agentId, userId, 4, null);

        var agent = new AgentEntity { Id = agentId, Name = "Test Agent" };
        _agentRepositoryMock.Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        _ratingRepositoryMock.Setup(x => x.GetByAgentAndUserAsync(agentId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentRatingEntity?)null);

        AgentRatingEntity? capturedRating = null;
        _ratingRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<AgentRatingEntity>(), It.IsAny<CancellationToken>()))
            .Callback<AgentRatingEntity, CancellationToken>((rating, _) => capturedRating = rating)
            .Returns(Task.CompletedTask);

        _ratingRepositoryMock.Setup(x => x.GetAverageRatingAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(4.0);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedRating);
        Assert.Null(capturedRating.Comment);
        Assert.Equal(4, result.Rating);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task Handle_Should_Accept_Valid_Rating_Values(int validRating)
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var command = new RateAgentCommand(agentId, userId, validRating, null);

        var agent = new AgentEntity { Id = agentId, Name = "Test Agent" };
        _agentRepositoryMock.Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        _ratingRepositoryMock.Setup(x => x.GetByAgentAndUserAsync(agentId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentRatingEntity?)null);

        _ratingRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<AgentRatingEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _ratingRepositoryMock.Setup(x => x.GetAverageRatingAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((double)validRating);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(validRating, result.Rating);
    }
}
