using Application.Interfaces;
using Application.UseCases.Agent.RemovePluginFromAgent;
using Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Application.Tests.UseCases.Agent;

public class RemovePluginFromAgentHandlerTests
{
    private readonly Mock<IAgentRepository> _agentRepositoryMock;
    private readonly Mock<ILogger<RemovePluginFromAgentHandler>> _loggerMock;
    private readonly RemovePluginFromAgentHandler _handler;

    public RemovePluginFromAgentHandlerTests()
    {
        _agentRepositoryMock = new Mock<IAgentRepository>();
        _loggerMock = new Mock<ILogger<RemovePluginFromAgentHandler>>();
        _handler = new RemovePluginFromAgentHandler(
            _agentRepositoryMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_Should_Remove_Plugin_From_Agent()
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
            PluginIds = new[] { pluginId }
        };

        _agentRepositoryMock.Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        _agentRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<AgentEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new RemovePluginFromAgentCommand(userId, agentId, pluginId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
        Assert.DoesNotContain(pluginId, agent.PluginIds);
        _agentRepositoryMock.Verify(x => x.UpdateAsync(agent, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Agent_Not_Found()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();

        _agentRepositoryMock.Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentEntity?)null);

        var command = new RemovePluginFromAgentCommand(userId, agentId, pluginId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Agent not found", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Plugin_Not_Assigned()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        var otherPluginId = Guid.NewGuid();

        var agent = new AgentEntity
        {
            Id = agentId,
            Name = "Test Agent",
            UserId = userId,
            PluginIds = new[] { otherPluginId } // Different plugin
        };

        _agentRepositoryMock.Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        var command = new RemovePluginFromAgentCommand(userId, agentId, pluginId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not assigned", result.ErrorMessage!.ToLower());
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
            PluginIds = new[] { pluginId },
            UpdatedAt = originalUpdatedAt
        };

        _agentRepositoryMock.Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        _agentRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<AgentEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new RemovePluginFromAgentCommand(userId, agentId, pluginId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(agent.UpdatedAt > originalUpdatedAt);
    }

    [Fact]
    public async Task Handle_Should_Preserve_Other_Plugins()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var pluginToRemove = Guid.NewGuid();
        var pluginToKeep1 = Guid.NewGuid();
        var pluginToKeep2 = Guid.NewGuid();

        var agent = new AgentEntity
        {
            Id = agentId,
            Name = "Test Agent",
            UserId = userId,
            PluginIds = new[] { pluginToKeep1, pluginToRemove, pluginToKeep2 }
        };

        _agentRepositoryMock.Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        _agentRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<AgentEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new RemovePluginFromAgentCommand(userId, agentId, pluginToRemove);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, agent.PluginIds.Length);
        Assert.Contains(pluginToKeep1, agent.PluginIds);
        Assert.Contains(pluginToKeep2, agent.PluginIds);
        Assert.DoesNotContain(pluginToRemove, agent.PluginIds);
    }

    [Fact]
    public async Task Handle_Should_Result_In_Empty_PluginIds_When_Last_Plugin_Removed()
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
            PluginIds = new[] { pluginId } // Only one plugin
        };

        _agentRepositoryMock.Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        _agentRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<AgentEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new RemovePluginFromAgentCommand(userId, agentId, pluginId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Empty(agent.PluginIds);
    }

    [Fact]
    public async Task Handle_Should_Return_Error_On_Exception()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();

        _agentRepositoryMock.Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        var command = new RemovePluginFromAgentCommand(userId, agentId, pluginId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("error occurred", result.ErrorMessage!.ToLower());
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Agent_Has_No_Plugins()
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
            PluginIds = Array.Empty<Guid>() // No plugins
        };

        _agentRepositoryMock.Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        var command = new RemovePluginFromAgentCommand(userId, agentId, pluginId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not assigned", result.ErrorMessage!.ToLower());
    }
}
