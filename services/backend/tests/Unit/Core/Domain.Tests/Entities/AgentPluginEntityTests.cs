using Domain.Entities;

namespace Domain.Tests.Entities;

public class AgentPluginEntityTests
{
    [Fact]
    public void Properties_AreSetCorrectly()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();

        var agentPlugin = new AgentPluginEntity
        {
            AgentId = agentId,
            PluginId = pluginId
        };

        // Assert
        Assert.Equal(agentId, agentPlugin.AgentId);
        Assert.Equal(pluginId, agentPlugin.PluginId);
    }

    [Fact]
    public void NavigationProperties_CanBeSet()
    {
        // Arrange
        var agent = new AgentEntity { Id = Guid.NewGuid(), Name = "Test Agent" };
        var plugin = new PluginEntity { Id = Guid.NewGuid(), Name = "Test Plugin" };

        var agentPlugin = new AgentPluginEntity
        {
            AgentId = agent.Id,
            Agent = agent,
            PluginId = plugin.Id,
            Plugin = plugin
        };

        // Assert
        Assert.Equal(agent, agentPlugin.Agent);
        Assert.Equal(plugin, agentPlugin.Plugin);
    }
}
