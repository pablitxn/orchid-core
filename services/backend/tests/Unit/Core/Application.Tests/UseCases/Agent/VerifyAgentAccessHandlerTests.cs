using Application.Interfaces;
using Application.UseCases.Agent.VerifyAgentAccess;
using Domain.Entities;
using Moq;
using Xunit;

namespace Application.Tests.UseCases.Agent;

public class VerifyAgentAccessHandlerTests
{
    private readonly Mock<IAgentRepository> _agentRepositoryMock;
    private readonly Mock<IUserPluginRepository> _userPluginRepositoryMock;
    private readonly VerifyAgentAccessHandler _handler;

    public VerifyAgentAccessHandlerTests()
    {
        _agentRepositoryMock = new Mock<IAgentRepository>();
        _userPluginRepositoryMock = new Mock<IUserPluginRepository>();
        _handler = new VerifyAgentAccessHandler(
            _agentRepositoryMock.Object,
            _userPluginRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_Should_Allow_Access_To_Public_Agent_Without_Plugins()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var agent = new AgentEntity
        {
            Id = agentId,
            Name = "Public Agent",
            IsPublic = true,
            UserId = Guid.NewGuid(), // Different owner
            PluginIds = Array.Empty<Guid>()
        };

        _agentRepositoryMock.Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        var query = new VerifyAgentAccessQuery(userId, agentId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.HasAccess);
        Assert.Null(result.Reason);
        Assert.Null(result.MissingPlugins);
    }

    [Fact]
    public async Task Handle_Should_Allow_Access_To_Own_Private_Agent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var agent = new AgentEntity
        {
            Id = agentId,
            Name = "My Private Agent",
            IsPublic = false,
            UserId = userId, // Same owner
            PluginIds = Array.Empty<Guid>()
        };

        _agentRepositoryMock.Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        var query = new VerifyAgentAccessQuery(userId, agentId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.HasAccess);
    }

    [Fact]
    public async Task Handle_Should_Deny_Access_To_Others_Private_Agent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var agent = new AgentEntity
        {
            Id = agentId,
            Name = "Someone's Private Agent",
            IsPublic = false,
            UserId = ownerId, // Different owner
            PluginIds = Array.Empty<Guid>()
        };

        _agentRepositoryMock.Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        var query = new VerifyAgentAccessQuery(userId, agentId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.False(result.HasAccess);
        Assert.Equal("Agent is private and you are not the owner", result.Reason);
    }

    [Fact]
    public async Task Handle_Should_Deny_Access_When_Missing_Required_Plugins()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var plugin1 = Guid.NewGuid();
        var plugin2 = Guid.NewGuid();
        var plugin3 = Guid.NewGuid();

        var agent = new AgentEntity
        {
            Id = agentId,
            Name = "Agent with Plugins",
            IsPublic = true,
            UserId = Guid.NewGuid(),
            PluginIds = new[] { plugin1, plugin2, plugin3 }
        };

        _agentRepositoryMock.Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        // User only owns plugin1
        _userPluginRepositoryMock.Setup(x => x.UserOwnsPluginAsync(userId, plugin1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _userPluginRepositoryMock.Setup(x => x.UserOwnsPluginAsync(userId, plugin2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _userPluginRepositoryMock.Setup(x => x.UserOwnsPluginAsync(userId, plugin3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var query = new VerifyAgentAccessQuery(userId, agentId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.False(result.HasAccess);
        Assert.Equal("You don't have access to all required plugins", result.Reason);
        Assert.NotNull(result.MissingPlugins);
        Assert.Equal(2, result.MissingPlugins.Count);
        Assert.Contains(plugin2, result.MissingPlugins);
        Assert.Contains(plugin3, result.MissingPlugins);
    }

    [Fact]
    public async Task Handle_Should_Allow_Access_When_User_Has_All_Required_Plugins()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var plugin1 = Guid.NewGuid();
        var plugin2 = Guid.NewGuid();

        var agent = new AgentEntity
        {
            Id = agentId,
            Name = "Agent with Plugins",
            IsPublic = true,
            UserId = Guid.NewGuid(),
            PluginIds = new[] { plugin1, plugin2 }
        };

        _agentRepositoryMock.Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        // User owns both plugins
        _userPluginRepositoryMock.Setup(x => x.UserOwnsPluginAsync(userId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var query = new VerifyAgentAccessQuery(userId, agentId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.HasAccess);
        Assert.Null(result.MissingPlugins);
    }

    [Fact]
    public async Task Handle_Should_Return_Agent_Not_Found_When_Agent_Does_Not_Exist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        _agentRepositoryMock.Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentEntity?)null);

        var query = new VerifyAgentAccessQuery(userId, agentId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.False(result.HasAccess);
        Assert.Equal("Agent not found", result.Reason);
    }
}