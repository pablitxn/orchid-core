using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Domain.Entities;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using AppChatMessage = Application.Interfaces.ChatMessage;
using IChatCompletionPort = Application.Interfaces.IChatCompletionPort;

namespace AgentFramework.IntegrationTests;

/// <summary>
/// Integration tests for the AgentFramework data layer.
/// Tests agent tool loading, configuration persistence, and model configuration.
/// All tests use mocks (no real OpenAI API calls).
/// </summary>
public class AgentFrameworkIntegrationTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private static readonly Guid TestUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public AgentFrameworkIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: "TestDb_AgentFramework_" + Guid.NewGuid())
            .Options;
        _context = new ApplicationDbContext(options);
        SeedTestData();
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private void SeedTestData()
    {
        var user = new UserEntity
        {
            Id = TestUserId,
            Email = "test@agentframework.com",
            Name = "Test User",
            PasswordHash = "hash"
        };
        _context.Users.Add(user);
        _context.SaveChanges();
    }

    #region Agent Tool Loading Tests

    [Fact]
    public async Task Agent_WithActivePlugins_Should_HavePluginIdsStored()
    {
        // Arrange
        var pluginId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        var plugin = new PluginEntity
        {
            Id = pluginId,
            Name = "Vector Store",
            SystemName = "vector_store",
            IsActive = true
        };

        var agent = new AgentEntity
        {
            Id = agentId,
            Name = "Tool Test Agent",
            UserId = TestUserId,
            PluginIds = new[] { pluginId }
        };

        await _context.Plugins.AddAsync(plugin);
        await _context.Agents.AddAsync(agent);
        await _context.SaveChangesAsync();

        // Act
        var savedAgent = await _context.Agents.FindAsync(agentId);

        // Assert
        savedAgent.Should().NotBeNull();
        savedAgent!.PluginIds.Should().Contain(pluginId);
    }

    [Fact]
    public async Task Agent_CanQuery_OnlyActivePlugins()
    {
        // Arrange
        var activePluginId = Guid.NewGuid();
        var inactivePluginId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        var activePlugin = new PluginEntity
        {
            Id = activePluginId,
            Name = "Active Plugin",
            SystemName = "vector_store",
            IsActive = true
        };

        var inactivePlugin = new PluginEntity
        {
            Id = inactivePluginId,
            Name = "Inactive Plugin",
            SystemName = "web_search",
            IsActive = false
        };

        var agent = new AgentEntity
        {
            Id = agentId,
            Name = "Mixed Plugins Agent",
            UserId = TestUserId,
            PluginIds = new[] { activePluginId, inactivePluginId }
        };

        await _context.Plugins.AddRangeAsync(activePlugin, inactivePlugin);
        await _context.Agents.AddAsync(agent);
        await _context.SaveChangesAsync();

        // Act
        var savedAgent = await _context.Agents.FindAsync(agentId);
        var activePlugins = await _context.Plugins
            .Where(p => savedAgent!.PluginIds.Contains(p.Id) && p.IsActive)
            .ToListAsync();

        // Assert
        activePlugins.Should().HaveCount(1);
        activePlugins.First().SystemName.Should().Be("vector_store");
    }

    [Fact]
    public async Task Agent_WithNoPlugins_Should_HaveEmptyPluginIds()
    {
        // Arrange
        var agentId = Guid.NewGuid();

        var agent = new AgentEntity
        {
            Id = agentId,
            Name = "No Plugins Agent",
            UserId = TestUserId,
            PluginIds = Array.Empty<Guid>()
        };

        await _context.Agents.AddAsync(agent);
        await _context.SaveChangesAsync();

        // Act
        var savedAgent = await _context.Agents.FindAsync(agentId);

        // Assert
        savedAgent.Should().NotBeNull();
        savedAgent!.PluginIds.Should().BeEmpty();
    }

    [Fact]
    public async Task DifferentAgents_Can_HaveDifferentPlugins()
    {
        // Arrange
        var plugin1Id = Guid.NewGuid();
        var plugin2Id = Guid.NewGuid();
        var agent1Id = Guid.NewGuid();
        var agent2Id = Guid.NewGuid();

        var plugin1 = new PluginEntity
        {
            Id = plugin1Id,
            Name = "Vector Store",
            SystemName = "vector_store",
            IsActive = true
        };

        var plugin2 = new PluginEntity
        {
            Id = plugin2Id,
            Name = "Web Search",
            SystemName = "web_search",
            IsActive = true
        };

        var agent1 = new AgentEntity
        {
            Id = agent1Id,
            Name = "Agent With Vector Store",
            UserId = TestUserId,
            PluginIds = new[] { plugin1Id }
        };

        var agent2 = new AgentEntity
        {
            Id = agent2Id,
            Name = "Agent With Web Search",
            UserId = TestUserId,
            PluginIds = new[] { plugin2Id }
        };

        await _context.Plugins.AddRangeAsync(plugin1, plugin2);
        await _context.Agents.AddRangeAsync(agent1, agent2);
        await _context.SaveChangesAsync();

        // Act
        var savedAgent1 = await _context.Agents.FindAsync(agent1Id);
        var savedAgent2 = await _context.Agents.FindAsync(agent2Id);

        // Assert
        savedAgent1!.PluginIds.Should().Contain(plugin1Id);
        savedAgent1!.PluginIds.Should().NotContain(plugin2Id);
        savedAgent2!.PluginIds.Should().Contain(plugin2Id);
        savedAgent2!.PluginIds.Should().NotContain(plugin1Id);
    }

    #endregion

    #region Model Configuration Tests

    [Fact]
    public async Task Agent_WithCustomModel_Should_StoreConfiguredModel()
    {
        // Arrange
        var customModel = "gpt-4o-mini";
        var agentId = Guid.NewGuid();

        var agent = new AgentEntity
        {
            Id = agentId,
            Name = "Custom Model Agent",
            Model = customModel,
            UserId = TestUserId
        };

        await _context.Agents.AddAsync(agent);
        await _context.SaveChangesAsync();

        // Act
        var savedAgent = await _context.Agents.FindAsync(agentId);

        // Assert
        savedAgent.Should().NotBeNull();
        savedAgent!.Model.Should().Be(customModel);
    }

    [Fact]
    public async Task Agent_WithNoModel_Should_HaveNullModel()
    {
        // Arrange
        var agentId = Guid.NewGuid();

        var agent = new AgentEntity
        {
            Id = agentId,
            Name = "Default Model Agent",
            Model = null,
            UserId = TestUserId
        };

        await _context.Agents.AddAsync(agent);
        await _context.SaveChangesAsync();

        // Act
        var savedAgent = await _context.Agents.FindAsync(agentId);

        // Assert
        savedAgent.Should().NotBeNull();
        savedAgent!.Model.Should().BeNull();
    }

    [Fact]
    public async Task Agent_CanUpdate_Model()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var initialModel = "gpt-4o";
        var updatedModel = "gpt-4o-mini";

        var agent = new AgentEntity
        {
            Id = agentId,
            Name = "Updatable Model Agent",
            Model = initialModel,
            UserId = TestUserId
        };

        await _context.Agents.AddAsync(agent);
        await _context.SaveChangesAsync();

        // Act
        var savedAgent = await _context.Agents.FindAsync(agentId);
        savedAgent!.Model = updatedModel;
        await _context.SaveChangesAsync();

        var updatedAgent = await _context.Agents.FindAsync(agentId);

        // Assert
        updatedAgent!.Model.Should().Be(updatedModel);
    }

    #endregion

    #region Plugin Entity Tests

    [Fact]
    public async Task Plugin_ShouldPersist_AllProperties()
    {
        // Arrange
        var pluginId = Guid.NewGuid();
        var plugin = new PluginEntity
        {
            Id = pluginId,
            Name = "Test Plugin",
            Description = "A test plugin for unit testing",
            SystemName = "test_plugin",
            SourceUrl = "https://example.com/plugin",
            IsActive = true
        };

        await _context.Plugins.AddAsync(plugin);
        await _context.SaveChangesAsync();

        // Act
        var savedPlugin = await _context.Plugins.FindAsync(pluginId);

        // Assert
        savedPlugin.Should().NotBeNull();
        savedPlugin!.Name.Should().Be("Test Plugin");
        savedPlugin.Description.Should().Be("A test plugin for unit testing");
        savedPlugin.SystemName.Should().Be("test_plugin");
        savedPlugin.SourceUrl.Should().Be("https://example.com/plugin");
        savedPlugin.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Plugin_CanQuery_BySystemName()
    {
        // Arrange
        var plugin = new PluginEntity
        {
            Id = Guid.NewGuid(),
            Name = "Vector Store",
            SystemName = "vector_store_unique",
            IsActive = true
        };

        await _context.Plugins.AddAsync(plugin);
        await _context.SaveChangesAsync();

        // Act
        var foundPlugin = await _context.Plugins
            .FirstOrDefaultAsync(p => p.SystemName == "vector_store_unique");

        // Assert
        foundPlugin.Should().NotBeNull();
        foundPlugin!.Name.Should().Be("Vector Store");
    }

    #endregion

    #region Mock Chat Completion Port

    /// <summary>
    /// Mock chat completion port for testing without real OpenAI API calls.
    /// </summary>
    private sealed class MockChatCompletionPort : IChatCompletionPort
    {
        public Task<string> CompleteAsync(
            IEnumerable<AppChatMessage> messages,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult("Mock response from AI");
        }

        public Task<string> CompleteWithAgentAsync(
            IEnumerable<AppChatMessage> messages,
            Guid agentId,
            Guid? userId = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult($"Mock agent response for agent {agentId}");
        }

        public async IAsyncEnumerable<string> CompleteStreamingAsync(
            IEnumerable<AppChatMessage> messages,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return "Mock ";
            await Task.Yield();
            yield return "streaming ";
            await Task.Yield();
            yield return "response";
        }

        public async IAsyncEnumerable<string> CompleteWithAgentStreamingAsync(
            IEnumerable<AppChatMessage> messages,
            Guid agentId,
            Guid? userId = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return "Mock ";
            await Task.Yield();
            yield return "agent ";
            await Task.Yield();
            yield return $"streaming response for {agentId}";
        }
    }

    #endregion
}
