using Application.Interfaces;
using Application.UseCases.Agent.Common;
using Application.UseCases.Agent.UpdateAgent;
using Domain.Entities;
using Domain.Exceptions;
using Moq;
using Xunit;

namespace Application.Tests.UseCases.Agent;

public class UpdateAgentHandlerTests
{
    private readonly Mock<IAgentRepository> _agentRepositoryMock;
    private readonly Mock<IPluginRepository> _pluginRepositoryMock;
    private readonly Mock<IChatSessionRepository> _chatSessionRepositoryMock;
    private readonly Mock<IAgentRatingRepository> _agentRatingRepositoryMock;
    private readonly UpdateAgentHandler _handler;

    public UpdateAgentHandlerTests()
    {
        _agentRepositoryMock = new Mock<IAgentRepository>();
        _pluginRepositoryMock = new Mock<IPluginRepository>();
        _chatSessionRepositoryMock = new Mock<IChatSessionRepository>();
        _agentRatingRepositoryMock = new Mock<IAgentRatingRepository>();
        _handler = new UpdateAgentHandler(
            _agentRepositoryMock.Object,
            _pluginRepositoryMock.Object,
            _chatSessionRepositoryMock.Object,
            _agentRatingRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_Should_Update_Agent_When_User_Is_Owner()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var agent = new AgentEntity
        {
            Id = agentId,
            Name = "Original Name",
            Description = "Original Description",
            UserId = userId,
            PluginIds = Array.Empty<Guid>()
        };

        _agentRepositoryMock.Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        _agentRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<AgentEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _pluginRepositoryMock.Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PluginEntity>());

        _chatSessionRepositoryMock.Setup(x => x.CountByAgentIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        _chatSessionRepositoryMock.Setup(x => x.GetLastUsedByAgentIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DateTime.UtcNow);

        _agentRatingRepositoryMock.Setup(x => x.GetAverageRatingAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((double?)null);

        var command = new UpdateAgentCommand(
            agentId,
            userId,
            "Updated Name",
            "Updated Description",
            null,
            null,
            null,
            null,
            null, // model
            null,
            null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal("Updated Name", agent.Name);
        Assert.Equal("Updated Description", agent.Description);
        Assert.Equal("Updated Name", result.Name);
        Assert.Equal("Updated Description", result.Description);
        _agentRepositoryMock.Verify(x => x.UpdateAsync(agent, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Throw_KeyNotFoundException_When_Agent_Not_Found()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        _agentRepositoryMock.Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentEntity?)null);

        var command = new UpdateAgentCommand(
            agentId,
            userId,
            "New Name",
            null,
            null,
            null,
            null,
            null,
            null, // model
            null,
            null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<AgentNotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));

        Assert.Contains(agentId.ToString(), exception.Message);
    }

    [Fact]
    public async Task Handle_Should_Throw_UnauthorizedAccessException_When_User_Is_Not_Owner()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var attemptingUserId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        var agent = new AgentEntity
        {
            Id = agentId,
            Name = "Test Agent",
            UserId = ownerId,
            PluginIds = Array.Empty<Guid>()
        };

        _agentRepositoryMock.Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        var command = new UpdateAgentCommand(
            agentId,
            attemptingUserId,
            "New Name",
            null,
            null,
            null,
            null,
            null,
            null, // model
            null,
            null);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedDomainAccessException>(
            () => _handler.Handle(command, CancellationToken.None));

        _agentRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<AgentEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Throw_InvalidOperationException_When_Agent_Is_Deleted()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        var agent = new AgentEntity
        {
            Id = agentId,
            Name = "Deleted Agent",
            UserId = userId,
            IsDeleted = true,
            PluginIds = Array.Empty<Guid>()
        };

        _agentRepositoryMock.Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        var command = new UpdateAgentCommand(
            agentId,
            userId,
            "New Name",
            null,
            null,
            null,
            null,
            null,
            null, // model
            null,
            null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<AgentDeletedException>(
            () => _handler.Handle(command, CancellationToken.None));

        Assert.Contains("deleted", exception.Message.ToLower());
    }

    [Fact]
    public async Task Handle_Should_Only_Update_Provided_Fields()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var agent = new AgentEntity
        {
            Id = agentId,
            Name = "Original Name",
            Description = "Original Description",
            AvatarUrl = "https://original.url",
            Personality = "Original Personality",
            Language = "en",
            UserId = userId,
            IsPublic = false,
            PluginIds = Array.Empty<Guid>()
        };

        _agentRepositoryMock.Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        _agentRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<AgentEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _pluginRepositoryMock.Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PluginEntity>());

        _chatSessionRepositoryMock.Setup(x => x.CountByAgentIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _chatSessionRepositoryMock.Setup(x => x.GetLastUsedByAgentIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTime?)null);

        _agentRatingRepositoryMock.Setup(x => x.GetAverageRatingAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((double?)null);

        // Only update Name
        var command = new UpdateAgentCommand(
            agentId,
            userId,
            "Updated Name",
            null, // Keep original description
            null,
            null,
            null,
            null,
            null, // model
            null,
            null);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal("Updated Name", agent.Name);
        Assert.Equal("Original Description", agent.Description); // Unchanged
        Assert.Equal("https://original.url", agent.AvatarUrl); // Unchanged
        Assert.Equal("Original Personality", agent.Personality); // Unchanged
        Assert.Equal("en", agent.Language); // Unchanged
        Assert.False(agent.IsPublic); // Unchanged
    }

    [Fact]
    public async Task Handle_Should_Update_IsPublic_Field()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var agent = new AgentEntity
        {
            Id = agentId,
            Name = "Test Agent",
            UserId = userId,
            IsPublic = false,
            PluginIds = Array.Empty<Guid>()
        };

        _agentRepositoryMock.Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        _agentRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<AgentEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _pluginRepositoryMock.Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PluginEntity>());

        _chatSessionRepositoryMock.Setup(x => x.CountByAgentIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _chatSessionRepositoryMock.Setup(x => x.GetLastUsedByAgentIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTime?)null);

        _agentRatingRepositoryMock.Setup(x => x.GetAverageRatingAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((double?)null);

        var command = new UpdateAgentCommand(
            agentId,
            userId,
            null,
            null,
            null,
            null,
            null,
            null,
            null, // model
            null,
            true); // Make public

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(agent.IsPublic);
        Assert.True(result.IsPublic);
    }

    [Fact]
    public async Task Handle_Should_Update_PluginIds()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();

        var agent = new AgentEntity
        {
            Id = agentId,
            Name = "Test Agent",
            UserId = userId,
            PluginIds = Array.Empty<Guid>()
        };

        var plugin = new PluginEntity
        {
            Id = pluginId,
            Name = "Test Plugin",
            Description = "Test Description"
        };

        _agentRepositoryMock.Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        _agentRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<AgentEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _pluginRepositoryMock.Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PluginEntity> { plugin });

        _chatSessionRepositoryMock.Setup(x => x.CountByAgentIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _chatSessionRepositoryMock.Setup(x => x.GetLastUsedByAgentIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTime?)null);

        _agentRatingRepositoryMock.Setup(x => x.GetAverageRatingAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((double?)null);

        var command = new UpdateAgentCommand(
            agentId,
            userId,
            null,
            null,
            null,
            null,
            null,
            null,
            null, // model
            new[] { pluginId },
            null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Single(agent.PluginIds);
        Assert.Equal(pluginId, agent.PluginIds[0]);
        Assert.Single(result.Plugins);
        Assert.Equal(pluginId, result.Plugins[0].Id);
    }

    [Fact]
    public async Task Handle_Should_Update_UpdatedAt_Timestamp()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var originalUpdatedAt = DateTime.UtcNow.AddDays(-1);

        var agent = new AgentEntity
        {
            Id = agentId,
            Name = "Test Agent",
            UserId = userId,
            UpdatedAt = originalUpdatedAt,
            PluginIds = Array.Empty<Guid>()
        };

        _agentRepositoryMock.Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        _agentRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<AgentEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _pluginRepositoryMock.Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PluginEntity>());

        _chatSessionRepositoryMock.Setup(x => x.CountByAgentIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _chatSessionRepositoryMock.Setup(x => x.GetLastUsedByAgentIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTime?)null);

        _agentRatingRepositoryMock.Setup(x => x.GetAverageRatingAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((double?)null);

        var command = new UpdateAgentCommand(
            agentId,
            userId,
            "New Name",
            null,
            null,
            null,
            null,
            null,
            null, // model
            null,
            null);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(agent.UpdatedAt > originalUpdatedAt);
    }

    [Fact]
    public async Task Handle_Should_Return_Real_Stats()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var lastUsed = DateTime.UtcNow.AddHours(-1);

        var agent = new AgentEntity
        {
            Id = agentId,
            Name = "Test Agent",
            UserId = userId,
            PluginIds = Array.Empty<Guid>()
        };

        _agentRepositoryMock.Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        _agentRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<AgentEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _pluginRepositoryMock.Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PluginEntity>());

        _chatSessionRepositoryMock.Setup(x => x.CountByAgentIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);

        _chatSessionRepositoryMock.Setup(x => x.GetLastUsedByAgentIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lastUsed);

        _agentRatingRepositoryMock.Setup(x => x.GetAverageRatingAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((double?)null);

        var command = new UpdateAgentCommand(
            agentId,
            userId,
            "New Name",
            null,
            null,
            null,
            null,
            null,
            null, // model
            null,
            null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result.Stats);
        Assert.Equal(10, result.Stats.TotalConversations);
        Assert.Equal(lastUsed, result.Stats.LastUsed);
    }

    [Fact]
    public async Task Handle_Should_Return_Real_Rating()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        var agent = new AgentEntity
        {
            Id = agentId,
            Name = "Test Agent",
            UserId = userId,
            PluginIds = Array.Empty<Guid>()
        };

        _agentRepositoryMock.Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        _agentRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<AgentEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _pluginRepositoryMock.Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PluginEntity>());

        _chatSessionRepositoryMock.Setup(x => x.CountByAgentIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _chatSessionRepositoryMock.Setup(x => x.GetLastUsedByAgentIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTime?)null);

        _agentRatingRepositoryMock.Setup(x => x.GetAverageRatingAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(4.5);

        var command = new UpdateAgentCommand(
            agentId,
            userId,
            "New Name",
            null,
            null,
            null,
            null,
            null,
            null, // model
            null,
            null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result.Stats);
        Assert.Equal(4.5, result.Stats.AvgRating);
    }
}
