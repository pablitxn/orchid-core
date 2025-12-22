using Application.Interfaces;
using Application.UseCases.Agent.ListAgents;
using Domain.Entities;
using Moq;

namespace Application.Tests.UseCases.Agent;

public class ListAgentsHandlerTests
{
    private readonly ListAgentsHandler _handler;
    private readonly Mock<IAgentRepository> _agentRepo = new();
    private readonly Mock<IPluginRepository> _pluginRepo = new();
    private readonly Mock<IChatSessionRepository> _chatSessionRepo = new();

    public ListAgentsHandlerTests()
    {
        _handler = new ListAgentsHandler(_agentRepo.Object, _pluginRepo.Object, _chatSessionRepo.Object);
    }

    [Fact]
    public async Task Handle_ReturnsPublicAgents()
    {
        var expected = new List<AgentEntity> { new() { Id = Guid.NewGuid(), Name = "a", PluginIds = [], IsPublic = true } };
        _agentRepo.Setup(r => r.ListByUserAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        // Setup the plugin repository to return an empty list
        _pluginRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Domain.Entities.PluginEntity>());

        // Setup stats to return empty dictionary
        _chatSessionRepo.Setup(r => r.GetStatsForAgentsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, (int, DateTime?)>());

        var result = await _handler.Handle(new ListAgentsQuery(), CancellationToken.None);

        Assert.Equal(expected.Count, result.Count);
    }

    [Fact]
    public async Task Handle_ReturnsUsersPrivateAgents()
    {
        var userId = Guid.NewGuid();
        var privateAgent = new AgentEntity { Id = Guid.NewGuid(), Name = "private", PluginIds = [], IsPublic = false, UserId = userId };
        var expectedAgents = new List<AgentEntity> { privateAgent };

        // ListByUserAsync now filters at DB level, so we return only the expected result
        _agentRepo.Setup(r => r.ListByUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(expectedAgents);
        _pluginRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Domain.Entities.PluginEntity>());
        _chatSessionRepo.Setup(r => r.GetStatsForAgentsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, (int, DateTime?)>());

        var result = await _handler.Handle(new ListAgentsQuery(userId), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(privateAgent.Name, result[0].Name);
    }

    [Fact]
    public async Task Handle_FiltersOutPrivateAgentsForAnonymousUsers()
    {
        var publicAgent = new AgentEntity { Id = Guid.NewGuid(), Name = "public", PluginIds = [], IsPublic = true };
        var expectedAgents = new List<AgentEntity> { publicAgent };

        // ListByUserAsync now filters at DB level, so we return only public agents for anonymous users
        _agentRepo.Setup(r => r.ListByUserAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(expectedAgents);
        _pluginRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Domain.Entities.PluginEntity>());
        _chatSessionRepo.Setup(r => r.GetStatsForAgentsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, (int, DateTime?)>());

        var result = await _handler.Handle(new ListAgentsQuery(), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(publicAgent.Name, result[0].Name);
    }

    [Fact]
    public async Task Handle_ReturnsRealStats()
    {
        var agentId = Guid.NewGuid();
        var agent = new AgentEntity { Id = agentId, Name = "agent", PluginIds = [], IsPublic = true };
        var lastUsed = DateTime.UtcNow.AddHours(-1);

        _agentRepo.Setup(r => r.ListByUserAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentEntity> { agent });
        _pluginRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Domain.Entities.PluginEntity>());
        _chatSessionRepo.Setup(r => r.GetStatsForAgentsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, (int, DateTime?)> { { agentId, (5, lastUsed) } });

        var result = await _handler.Handle(new ListAgentsQuery(), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(5, result[0].Stats.TotalConversations);
        Assert.Equal(lastUsed, result[0].Stats.LastUsed);
    }
}
