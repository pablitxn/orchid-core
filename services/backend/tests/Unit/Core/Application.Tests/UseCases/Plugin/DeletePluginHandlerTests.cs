using Application.Interfaces;
using Application.UseCases.Plugin.DeletePlugin;
using Domain.Entities;
using Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Application.Tests.UseCases.Plugin;

public class DeletePluginHandlerTests
{
    private readonly Mock<IPluginRepository> _pluginRepositoryMock;
    private readonly Mock<IUserPluginRepository> _userPluginRepositoryMock;
    private readonly Mock<IAgentRepository> _agentRepositoryMock;
    private readonly Mock<ILogger<DeletePluginHandler>> _loggerMock;
    private readonly DeletePluginHandler _handler;

    public DeletePluginHandlerTests()
    {
        _pluginRepositoryMock = new Mock<IPluginRepository>();
        _userPluginRepositoryMock = new Mock<IUserPluginRepository>();
        _agentRepositoryMock = new Mock<IAgentRepository>();
        _loggerMock = new Mock<ILogger<DeletePluginHandler>>();
        _handler = new DeletePluginHandler(
            _pluginRepositoryMock.Object,
            _userPluginRepositoryMock.Object,
            _agentRepositoryMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_DeletesPlugin_WhenUserOwnsPlugin()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        var command = new DeletePluginCommand(pluginId, userId);

        var plugin = new PluginEntity
        {
            Id = pluginId,
            Name = "Test Plugin"
        };

        _pluginRepositoryMock
            .Setup(x => x.GetByIdAsync(pluginId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin);

        _userPluginRepositoryMock
            .Setup(x => x.UserOwnsPluginAsync(userId, pluginId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _agentRepositoryMock
            .Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentEntity>());

        _userPluginRepositoryMock
            .Setup(x => x.DeleteByUserAndPluginAsync(userId, pluginId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(MediatR.Unit.Value, result);
        _userPluginRepositoryMock.Verify(
            x => x.DeleteByUserAndPluginAsync(userId, pluginId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ThrowsPluginNotFoundException_WhenPluginNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        var command = new DeletePluginCommand(pluginId, userId);

        _pluginRepositoryMock
            .Setup(x => x.GetByIdAsync(pluginId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PluginEntity?)null);

        // Act & Assert
        await Assert.ThrowsAsync<PluginNotFoundException>(() =>
            _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ThrowsUnauthorizedDomainAccessException_WhenUserDoesNotOwnPlugin()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        var command = new DeletePluginCommand(pluginId, userId);

        var plugin = new PluginEntity
        {
            Id = pluginId,
            Name = "Test Plugin"
        };

        _pluginRepositoryMock
            .Setup(x => x.GetByIdAsync(pluginId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin);

        _userPluginRepositoryMock
            .Setup(x => x.UserOwnsPluginAsync(userId, pluginId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedDomainAccessException>(() =>
            _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_RemovesPluginFromAgents_WhenAgentsUsePlugin()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var command = new DeletePluginCommand(pluginId, userId);

        var plugin = new PluginEntity
        {
            Id = pluginId,
            Name = "Test Plugin"
        };

        var agentWithPlugin = new AgentEntity
        {
            Id = agentId,
            Name = "Agent with Plugin",
            PluginIds = new[] { pluginId, Guid.NewGuid() }
        };

        var agentWithoutPlugin = new AgentEntity
        {
            Id = Guid.NewGuid(),
            Name = "Agent without Plugin",
            PluginIds = new[] { Guid.NewGuid() }
        };

        _pluginRepositoryMock
            .Setup(x => x.GetByIdAsync(pluginId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin);

        _userPluginRepositoryMock
            .Setup(x => x.UserOwnsPluginAsync(userId, pluginId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _agentRepositoryMock
            .Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentEntity> { agentWithPlugin, agentWithoutPlugin });

        AgentEntity? updatedAgent = null;
        _agentRepositoryMock
            .Setup(x => x.UpdateAsync(It.IsAny<AgentEntity>(), It.IsAny<CancellationToken>()))
            .Callback<AgentEntity, CancellationToken>((a, _) => updatedAgent = a)
            .Returns(Task.CompletedTask);

        _userPluginRepositoryMock
            .Setup(x => x.DeleteByUserAndPluginAsync(userId, pluginId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _agentRepositoryMock.Verify(
            x => x.UpdateAsync(It.Is<AgentEntity>(a => a.Id == agentId), It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.NotNull(updatedAgent);
        Assert.DoesNotContain(pluginId, updatedAgent.PluginIds);
    }

    [Fact]
    public async Task Handle_DoesNotUpdateAgents_WhenNoAgentsUsePlugin()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        var command = new DeletePluginCommand(pluginId, userId);

        var plugin = new PluginEntity
        {
            Id = pluginId,
            Name = "Test Plugin"
        };

        var agents = new List<AgentEntity>
        {
            new() { Id = Guid.NewGuid(), PluginIds = new[] { Guid.NewGuid() } },
            new() { Id = Guid.NewGuid(), PluginIds = Array.Empty<Guid>() }
        };

        _pluginRepositoryMock
            .Setup(x => x.GetByIdAsync(pluginId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin);

        _userPluginRepositoryMock
            .Setup(x => x.UserOwnsPluginAsync(userId, pluginId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _agentRepositoryMock
            .Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(agents);

        _userPluginRepositoryMock
            .Setup(x => x.DeleteByUserAndPluginAsync(userId, pluginId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _agentRepositoryMock.Verify(
            x => x.UpdateAsync(It.IsAny<AgentEntity>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
