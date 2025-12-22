using Application.Interfaces;
using Application.UseCases.Agent.ListRecycleBinAgents;
using Domain.Entities;
using Moq;
using Xunit;

namespace Application.Tests.UseCases.Agent;

public class ListRecycleBinAgentsHandlerTests
{
    private readonly Mock<IAgentRepository> _agentRepositoryMock;
    private readonly Mock<IPluginRepository> _pluginRepositoryMock;
    private readonly ListRecycleBinAgentsHandler _handler;

    public ListRecycleBinAgentsHandlerTests()
    {
        _agentRepositoryMock = new Mock<IAgentRepository>();
        _pluginRepositoryMock = new Mock<IPluginRepository>();
        _handler = new ListRecycleBinAgentsHandler(
            _agentRepositoryMock.Object,
            _pluginRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_Should_Return_Agents_In_RecycleBin_For_User()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        var agents = new List<AgentEntity>
        {
            new()
            {
                Id = agentId,
                Name = "Deleted Agent",
                UserId = userId,
                IsDeleted = true,
                IsInRecycleBin = true,
                PluginIds = Array.Empty<Guid>()
            }
        };

        _agentRepositoryMock.Setup(x => x.ListRecycleBinByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agents);

        _pluginRepositoryMock.Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PluginEntity>());

        var query = new ListRecycleBinAgentsQuery(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Single(result);
        Assert.Equal(agentId, result[0].Id);
        Assert.Equal("Deleted Agent", result[0].Name);
    }

    [Fact]
    public async Task Handle_Should_Return_Empty_List_When_No_Agents_In_RecycleBin()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _agentRepositoryMock.Setup(x => x.ListRecycleBinByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentEntity>());

        _pluginRepositoryMock.Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PluginEntity>());

        var query = new ListRecycleBinAgentsQuery(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_Should_Include_Plugin_Details_In_Response()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();

        var agents = new List<AgentEntity>
        {
            new()
            {
                Id = agentId,
                Name = "Deleted Agent",
                UserId = userId,
                IsDeleted = true,
                IsInRecycleBin = true,
                PluginIds = new[] { pluginId }
            }
        };

        var plugins = new List<PluginEntity>
        {
            new()
            {
                Id = pluginId,
                Name = "Test Plugin",
                Description = "Test Description"
            }
        };

        _agentRepositoryMock.Setup(x => x.ListRecycleBinByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agents);

        _pluginRepositoryMock.Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugins);

        var query = new ListRecycleBinAgentsQuery(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Single(result);
        Assert.Single(result[0].Plugins);
        Assert.Equal(pluginId, result[0].Plugins[0].Id);
        Assert.Equal("Test Plugin", result[0].Plugins[0].Name);
    }

    [Fact]
    public async Task Handle_Should_Return_Multiple_Agents()
    {
        // Arrange
        var userId = Guid.NewGuid();

        var agents = new List<AgentEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Deleted Agent 1",
                UserId = userId,
                IsDeleted = true,
                IsInRecycleBin = true,
                PluginIds = Array.Empty<Guid>()
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Deleted Agent 2",
                UserId = userId,
                IsDeleted = true,
                IsInRecycleBin = true,
                PluginIds = Array.Empty<Guid>()
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Deleted Agent 3",
                UserId = userId,
                IsDeleted = true,
                IsInRecycleBin = true,
                PluginIds = Array.Empty<Guid>()
            }
        };

        _agentRepositoryMock.Setup(x => x.ListRecycleBinByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agents);

        _pluginRepositoryMock.Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PluginEntity>());

        var query = new ListRecycleBinAgentsQuery(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task Handle_Should_Return_Stats_With_Zero_Values()
    {
        // Arrange - Recycle bin agents don't retrieve real stats
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        var agents = new List<AgentEntity>
        {
            new()
            {
                Id = agentId,
                Name = "Deleted Agent",
                UserId = userId,
                IsDeleted = true,
                IsInRecycleBin = true,
                PluginIds = Array.Empty<Guid>()
            }
        };

        _agentRepositoryMock.Setup(x => x.ListRecycleBinByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agents);

        _pluginRepositoryMock.Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PluginEntity>());

        var query = new ListRecycleBinAgentsQuery(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Single(result);
        Assert.NotNull(result[0].Stats);
        Assert.Equal(0, result[0].Stats.TotalConversations);
        Assert.Null(result[0].Stats.LastUsed);
    }

    [Fact]
    public async Task Handle_Should_Filter_Out_Unknown_Plugins()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var knownPluginId = Guid.NewGuid();
        var unknownPluginId = Guid.NewGuid();

        var agents = new List<AgentEntity>
        {
            new()
            {
                Id = agentId,
                Name = "Deleted Agent",
                UserId = userId,
                IsDeleted = true,
                IsInRecycleBin = true,
                PluginIds = new[] { knownPluginId, unknownPluginId }
            }
        };

        var plugins = new List<PluginEntity>
        {
            new()
            {
                Id = knownPluginId,
                Name = "Known Plugin",
                Description = "Description"
            }
            // unknownPluginId is not in the list
        };

        _agentRepositoryMock.Setup(x => x.ListRecycleBinByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agents);

        _pluginRepositoryMock.Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugins);

        var query = new ListRecycleBinAgentsQuery(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Single(result);
        Assert.Single(result[0].Plugins); // Only known plugin
        Assert.Equal(knownPluginId, result[0].Plugins[0].Id);
    }

    [Fact]
    public async Task Handle_Should_Call_ListRecycleBinByUserAsync_With_Correct_UserId()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _agentRepositoryMock.Setup(x => x.ListRecycleBinByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentEntity>());

        _pluginRepositoryMock.Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PluginEntity>());

        var query = new ListRecycleBinAgentsQuery(userId);

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _agentRepositoryMock.Verify(x => x.ListRecycleBinByUserAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
