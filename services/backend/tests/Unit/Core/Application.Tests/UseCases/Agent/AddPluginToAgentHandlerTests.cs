using Application.Interfaces;
using Application.UseCases.Agent.AddPluginToAgent;
using Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;

namespace Application.Tests.UseCases.Agent;

public class AddPluginToAgentHandlerTests
{
    private readonly Mock<IAgentRepository> _agentRepositoryMock;
    private readonly Mock<IUserPluginRepository> _userPluginRepositoryMock;
    private readonly Mock<ILogger<AddPluginToAgentHandler>> _loggerMock;
    private readonly AddPluginToAgentHandler _handler;

    public AddPluginToAgentHandlerTests()
    {
        _agentRepositoryMock = new Mock<IAgentRepository>();
        _userPluginRepositoryMock = new Mock<IUserPluginRepository>();
        _loggerMock = new Mock<ILogger<AddPluginToAgentHandler>>();
        _handler = new AddPluginToAgentHandler(
            _agentRepositoryMock.Object,
            _userPluginRepositoryMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_Should_Add_Plugin_To_Agent_When_User_Owns_Plugin()
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

        _userPluginRepositoryMock.Setup(x => x.UserOwnsPluginAsync(userId, pluginId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _agentRepositoryMock.Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        _agentRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<AgentEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new AddPluginToAgentCommand(userId, agentId, pluginId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
        Assert.Contains(pluginId, agent.PluginIds);
        _agentRepositoryMock.Verify(x => x.UpdateAsync(agent, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_User_Does_Not_Own_Plugin()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();

        _userPluginRepositoryMock.Setup(x => x.UserOwnsPluginAsync(userId, pluginId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = new AddPluginToAgentCommand(userId, agentId, pluginId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("purchase", result.ErrorMessage!.ToLower());
        _agentRepositoryMock.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Agent_Not_Found()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();

        _userPluginRepositoryMock.Setup(x => x.UserOwnsPluginAsync(userId, pluginId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _agentRepositoryMock.Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentEntity?)null);

        var command = new AddPluginToAgentCommand(userId, agentId, pluginId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Agent not found", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Plugin_Already_Added()
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
            PluginIds = [pluginId] // Plugin already exists
        };

        _userPluginRepositoryMock.Setup(x => x.UserOwnsPluginAsync(userId, pluginId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _agentRepositoryMock.Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        var command = new AddPluginToAgentCommand(userId, agentId, pluginId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("already added", result.ErrorMessage!.ToLower());
        _agentRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<AgentEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Update_UpdatedAt_Timestamp()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        var originalUpdatedAt = DateTime.UtcNow.AddDays(-1);

        var agent = new AgentEntity
        {
            Id = agentId,
            Name = "Test Agent",
            UserId = userId,
            PluginIds = Array.Empty<Guid>(),
            UpdatedAt = originalUpdatedAt
        };

        _userPluginRepositoryMock.Setup(x => x.UserOwnsPluginAsync(userId, pluginId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _agentRepositoryMock.Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        _agentRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<AgentEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new AddPluginToAgentCommand(userId, agentId, pluginId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(agent.UpdatedAt > originalUpdatedAt);
    }

    [Fact]
    public async Task Handle_Should_Preserve_Existing_Plugins()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var existingPluginId = Guid.NewGuid();
        var newPluginId = Guid.NewGuid();

        var agent = new AgentEntity
        {
            Id = agentId,
            Name = "Test Agent",
            UserId = userId,
            PluginIds = [existingPluginId]
        };

        _userPluginRepositoryMock.Setup(x => x.UserOwnsPluginAsync(userId, newPluginId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _agentRepositoryMock.Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        _agentRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<AgentEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new AddPluginToAgentCommand(userId, agentId, newPluginId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, agent.PluginIds.Length);
        Assert.Contains(existingPluginId, agent.PluginIds);
        Assert.Contains(newPluginId, agent.PluginIds);
    }

    [Fact]
    public async Task Handle_Should_Return_Error_On_Exception()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();

        _userPluginRepositoryMock.Setup(x => x.UserOwnsPluginAsync(userId, pluginId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        var command = new AddPluginToAgentCommand(userId, agentId, pluginId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("error occurred", result.ErrorMessage!.ToLower());
    }
}
