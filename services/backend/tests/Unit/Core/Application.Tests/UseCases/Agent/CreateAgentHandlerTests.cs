using Application.Interfaces;
using Application.UseCases.Agent.CreateAgent;
using Domain.Entities;
using Moq;
using Xunit;

namespace Application.Tests.UseCases.Agent;

public class CreateAgentHandlerTests
{
    private readonly CreateAgentHandler _handler;
    private readonly Mock<IAgentRepository> _agentRepositoryMock;
    private readonly Mock<IPluginRepository> _pluginRepositoryMock;

    public CreateAgentHandlerTests()
    {
        _agentRepositoryMock = new Mock<IAgentRepository>();
        _pluginRepositoryMock = new Mock<IPluginRepository>();
        _handler = new CreateAgentHandler(_agentRepositoryMock.Object, _pluginRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_Should_Create_Agent_With_UserId_And_IsPublic()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateAgentCommand(
            "Test Agent",
            "Test Description",
            "https://example.com/avatar.png",
            "Friendly personality",
            null,
            "en",
            new Guid[] { },
            userId,
            true
        );

        var plugins = new List<PluginEntity>();
        _pluginRepositoryMock.Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugins);

        AgentEntity? capturedAgent = null;
        _agentRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<AgentEntity>(), It.IsAny<CancellationToken>()))
            .Callback<AgentEntity, CancellationToken>((agent, _) => capturedAgent = agent)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedAgent);
        Assert.Equal(userId, capturedAgent.UserId);
        Assert.True(capturedAgent.IsPublic);
        Assert.Equal("Test Agent", capturedAgent.Name);
        Assert.Equal("Test Description", capturedAgent.Description);
        
        Assert.NotNull(result);
        Assert.Equal(userId, result.UserId);
        Assert.True(result.IsPublic);
    }

    [Fact]
    public async Task Handle_Should_Create_Private_Agent_By_Default()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateAgentCommand(
            "Test Agent",
            null,
            null,
            null,
            null,
            null,
            null,
            userId,
            false // explicitly private
        );

        var plugins = new List<PluginEntity>();
        _pluginRepositoryMock.Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugins);

        AgentEntity? capturedAgent = null;
        _agentRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<AgentEntity>(), It.IsAny<CancellationToken>()))
            .Callback<AgentEntity, CancellationToken>((agent, _) => capturedAgent = agent)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedAgent);
        Assert.False(capturedAgent.IsPublic);
        Assert.Equal(userId, capturedAgent.UserId);
    }

    [Fact]
    public async Task Handle_Should_Create_Agent_Without_Owner()
    {
        // Arrange
        var command = new CreateAgentCommand(
            "Public System Agent",
            "System-wide agent",
            null,
            null,
            null,
            null,
            null,
            null, // no user ID
            true  // public
        );

        var plugins = new List<PluginEntity>();
        _pluginRepositoryMock.Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugins);

        AgentEntity? capturedAgent = null;
        _agentRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<AgentEntity>(), It.IsAny<CancellationToken>()))
            .Callback<AgentEntity, CancellationToken>((agent, _) => capturedAgent = agent)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedAgent);
        Assert.Null(capturedAgent.UserId);
        Assert.True(capturedAgent.IsPublic);
    }

    [Fact]
    public async Task Handle_Should_Include_Plugin_Details_In_Response()
    {
        // Arrange
        var pluginId = Guid.NewGuid();
        var command = new CreateAgentCommand(
            "Agent with Plugins",
            null,
            null,
            null,
            null,
            null,
            new[] { pluginId },
            Guid.NewGuid(),
            true
        );

        var plugin = new PluginEntity
        {
            Id = pluginId,
            Name = "Test Plugin",
            Description = "Test Description"
        };

        _pluginRepositoryMock.Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PluginEntity> { plugin });

        _agentRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<AgentEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Single(result.Plugins);
        Assert.Equal(pluginId, result.Plugins[0].Id);
        Assert.Equal("Test Plugin", result.Plugins[0].Name);
    }
}