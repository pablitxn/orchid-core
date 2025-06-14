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

    public ListAgentsHandlerTests()
    {
        _handler = new ListAgentsHandler(_agentRepo.Object, _pluginRepo.Object);
    }

    [Fact]
    public async Task Handle_ReturnsPublicAgents()
    {
        var expected = new List<AgentEntity> { new() { Id = Guid.NewGuid(), Name = "a", PluginIds = [], IsPublic = true } };
        _agentRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        // Setup the plugin repository to return an empty list
        _pluginRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Domain.Entities.PluginEntity>());

        var result = await _handler.Handle(new ListAgentsQuery(), CancellationToken.None);

        Assert.Equal(expected.Count, result.Count);
    }

    [Fact]
    public async Task Handle_ReturnsUsersPrivateAgents()
    {
        var userId = Guid.NewGuid();
        var privateAgent = new AgentEntity { Id = Guid.NewGuid(), Name = "private", PluginIds = [], IsPublic = false, UserId = userId };
        var otherUserAgent = new AgentEntity { Id = Guid.NewGuid(), Name = "other", PluginIds = [], IsPublic = false, UserId = Guid.NewGuid() };
        var allAgents = new List<AgentEntity> { privateAgent, otherUserAgent };
        
        _agentRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(allAgents);
        _pluginRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Domain.Entities.PluginEntity>());

        var result = await _handler.Handle(new ListAgentsQuery(userId), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(privateAgent.Name, result[0].Name);
    }

    [Fact]
    public async Task Handle_FiltersOutPrivateAgentsForAnonymousUsers()
    {
        var privateAgent = new AgentEntity { Id = Guid.NewGuid(), Name = "private", PluginIds = [], IsPublic = false, UserId = Guid.NewGuid() };
        var publicAgent = new AgentEntity { Id = Guid.NewGuid(), Name = "public", PluginIds = [], IsPublic = true };
        var allAgents = new List<AgentEntity> { privateAgent, publicAgent };
        
        _agentRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(allAgents);
        _pluginRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Domain.Entities.PluginEntity>());

        var result = await _handler.Handle(new ListAgentsQuery(), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(publicAgent.Name, result[0].Name);
    }
}