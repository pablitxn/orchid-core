using Application.Interfaces;
using Domain.Entities;
using FluentAssertions;
using Infrastructure.Ai.AgentFramework;
using Infrastructure.Ai.AgentFramework.Abstractions;
using Infrastructure.Ai.AgentFramework.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Infrastructure.Tests.Ai;

public class AgentToolLoaderTests
{
    private readonly Mock<IPluginRepository> _pluginRepositoryMock;
    private readonly Mock<IToolRegistry> _toolRegistryMock;
    private readonly Mock<ILogger<AgentToolLoader>> _loggerMock;
    private readonly AgentToolLoader _loader;

    public AgentToolLoaderTests()
    {
        _pluginRepositoryMock = new Mock<IPluginRepository>();
        _toolRegistryMock = new Mock<IToolRegistry>();
        _loggerMock = new Mock<ILogger<AgentToolLoader>>();
        _loader = new AgentToolLoader(
            _pluginRepositoryMock.Object,
            _toolRegistryMock.Object,
            _loggerMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_PluginRepositoryIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new AgentToolLoader(null!, _toolRegistryMock.Object, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_ToolRegistryIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new AgentToolLoader(_pluginRepositoryMock.Object, null!, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_LoggerIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new AgentToolLoader(_pluginRepositoryMock.Object, _toolRegistryMock.Object, null!));
    }

    #endregion

    #region LoadAgentToolsAsync Tests

    [Fact]
    public async Task LoadAgentToolsAsync_Should_ReturnEmptyList_When_NoPluginsFound()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        _pluginRepositoryMock
            .Setup(r => r.ListByAgentAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PluginEntity>());

        // Act
        var result = await _loader.LoadAgentToolsAsync(agentId, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadAgentToolsAsync_Should_ReturnEmptyList_When_AllPluginsInactive()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var plugins = new List<PluginEntity>
        {
            new() { Id = Guid.NewGuid(), SystemName = "vector_store", IsActive = false },
            new() { Id = Guid.NewGuid(), SystemName = "web_search", IsActive = false }
        };

        _pluginRepositoryMock
            .Setup(r => r.ListByAgentAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugins);

        // Act
        var result = await _loader.LoadAgentToolsAsync(agentId, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
        _toolRegistryMock.Verify(
            r => r.GetAIFunctionsBySystemName(It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task LoadAgentToolsAsync_Should_LoadToolsForActivePlugins()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var plugins = new List<PluginEntity>
        {
            new() { Id = Guid.NewGuid(), SystemName = "vector_store", IsActive = true, Name = "Vector Store" }
        };

        var mockAIFunctions = new List<AIFunction>
        {
            AIFunctionFactory.Create(() => "test", "test_function", "A test function")
        };

        _pluginRepositoryMock
            .Setup(r => r.ListByAgentAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugins);

        _toolRegistryMock
            .Setup(r => r.GetAIFunctionsBySystemName("vector_store"))
            .Returns(mockAIFunctions);

        // Act
        var result = await _loader.LoadAgentToolsAsync(agentId, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        _toolRegistryMock.Verify(
            r => r.GetAIFunctionsBySystemName("vector_store"),
            Times.Once);
    }

    [Fact]
    public async Task LoadAgentToolsAsync_Should_LoadMultipleToolsForMultipleActivePlugins()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var plugins = new List<PluginEntity>
        {
            new() { Id = Guid.NewGuid(), SystemName = "vector_store", IsActive = true, Name = "Vector Store" },
            new() { Id = Guid.NewGuid(), SystemName = "web_search", IsActive = true, Name = "Web Search" }
        };

        var vectorFunctions = new List<AIFunction>
        {
            AIFunctionFactory.Create(() => "vector", "search_documents", "Search documents")
        };

        var webSearchFunctions = new List<AIFunction>
        {
            AIFunctionFactory.Create(() => "web", "web_search", "Search the web")
        };

        _pluginRepositoryMock
            .Setup(r => r.ListByAgentAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugins);

        _toolRegistryMock
            .Setup(r => r.GetAIFunctionsBySystemName("vector_store"))
            .Returns(vectorFunctions);

        _toolRegistryMock
            .Setup(r => r.GetAIFunctionsBySystemName("web_search"))
            .Returns(webSearchFunctions);

        // Act
        var result = await _loader.LoadAgentToolsAsync(agentId, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task LoadAgentToolsAsync_Should_SkipPluginsWithNullSystemName()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var plugins = new List<PluginEntity>
        {
            new() { Id = Guid.NewGuid(), SystemName = null, IsActive = true, Name = "No System Name" },
            new() { Id = Guid.NewGuid(), SystemName = "vector_store", IsActive = true, Name = "Vector Store" }
        };

        var vectorFunctions = new List<AIFunction>
        {
            AIFunctionFactory.Create(() => "vector", "search_documents", "Search documents")
        };

        _pluginRepositoryMock
            .Setup(r => r.ListByAgentAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugins);

        _toolRegistryMock
            .Setup(r => r.GetAIFunctionsBySystemName("vector_store"))
            .Returns(vectorFunctions);

        // Act
        var result = await _loader.LoadAgentToolsAsync(agentId, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        _toolRegistryMock.Verify(
            r => r.GetAIFunctionsBySystemName(null!),
            Times.Never);
    }

    [Fact]
    public async Task LoadAgentToolsAsync_Should_SkipPluginsWithEmptySystemName()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var plugins = new List<PluginEntity>
        {
            new() { Id = Guid.NewGuid(), SystemName = "", IsActive = true, Name = "Empty System Name" },
            new() { Id = Guid.NewGuid(), SystemName = "   ", IsActive = true, Name = "Whitespace System Name" }
        };

        _pluginRepositoryMock
            .Setup(r => r.ListByAgentAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugins);

        // Act
        var result = await _loader.LoadAgentToolsAsync(agentId, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadAgentToolsAsync_Should_LogWarning_When_ToolNotFoundInRegistry()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        var plugins = new List<PluginEntity>
        {
            new() { Id = pluginId, SystemName = "unknown_tool", IsActive = true, Name = "Unknown Tool" }
        };

        _pluginRepositoryMock
            .Setup(r => r.ListByAgentAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugins);

        _toolRegistryMock
            .Setup(r => r.GetAIFunctionsBySystemName("unknown_tool"))
            .Returns(Array.Empty<AIFunction>());

        // Act
        var result = await _loader.LoadAgentToolsAsync(agentId, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
        _loggerMock.Verify(
            x => x.Log(
                Microsoft.Extensions.Logging.LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("not found in registry")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task LoadAgentToolsAsync_Should_ContinueLoading_When_SingleToolFails()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var plugins = new List<PluginEntity>
        {
            new() { Id = Guid.NewGuid(), SystemName = "failing_tool", IsActive = true, Name = "Failing Tool" },
            new() { Id = Guid.NewGuid(), SystemName = "vector_store", IsActive = true, Name = "Vector Store" }
        };

        var vectorFunctions = new List<AIFunction>
        {
            AIFunctionFactory.Create(() => "vector", "search_documents", "Search documents")
        };

        _pluginRepositoryMock
            .Setup(r => r.ListByAgentAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugins);

        _toolRegistryMock
            .Setup(r => r.GetAIFunctionsBySystemName("failing_tool"))
            .Throws(new InvalidOperationException("Tool initialization failed"));

        _toolRegistryMock
            .Setup(r => r.GetAIFunctionsBySystemName("vector_store"))
            .Returns(vectorFunctions);

        // Act
        var result = await _loader.LoadAgentToolsAsync(agentId, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        _loggerMock.Verify(
            x => x.Log(
                Microsoft.Extensions.Logging.LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<InvalidOperationException>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task LoadAgentToolsAsync_Should_ThrowException_When_RepositoryFails()
    {
        // Arrange
        var agentId = Guid.NewGuid();

        _pluginRepositoryMock
            .Setup(r => r.ListByAgentAsync(agentId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _loader.LoadAgentToolsAsync(agentId, CancellationToken.None));
    }

    [Fact]
    public async Task LoadAgentToolsAsync_Should_LogInformation_WithLoadedToolCount()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var plugins = new List<PluginEntity>
        {
            new() { Id = Guid.NewGuid(), SystemName = "vector_store", IsActive = true, Name = "Vector Store" }
        };

        var vectorFunctions = new List<AIFunction>
        {
            AIFunctionFactory.Create(() => "vector", "search_documents", "Search documents")
        };

        _pluginRepositoryMock
            .Setup(r => r.ListByAgentAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugins);

        _toolRegistryMock
            .Setup(r => r.GetAIFunctionsBySystemName("vector_store"))
            .Returns(vectorFunctions);

        // Act
        await _loader.LoadAgentToolsAsync(agentId, CancellationToken.None);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                Microsoft.Extensions.Logging.LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Loaded") && v.ToString()!.Contains("tool sets")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task LoadAgentToolsAsync_Should_RespectCancellationToken()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _pluginRepositoryMock
            .Setup(r => r.ListByAgentAsync(agentId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _loader.LoadAgentToolsAsync(agentId, cts.Token));
    }

    [Fact]
    public async Task LoadAgentToolsAsync_Should_FilterOnlyActivePlugins()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var plugins = new List<PluginEntity>
        {
            new() { Id = Guid.NewGuid(), SystemName = "vector_store", IsActive = true, Name = "Active Vector" },
            new() { Id = Guid.NewGuid(), SystemName = "web_search", IsActive = false, Name = "Inactive Web" },
            new() { Id = Guid.NewGuid(), SystemName = "vector_store", IsActive = false, Name = "Inactive Vector" }
        };

        var vectorFunctions = new List<AIFunction>
        {
            AIFunctionFactory.Create(() => "vector", "search_documents", "Search documents")
        };

        _pluginRepositoryMock
            .Setup(r => r.ListByAgentAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugins);

        _toolRegistryMock
            .Setup(r => r.GetAIFunctionsBySystemName("vector_store"))
            .Returns(vectorFunctions);

        // Act
        var result = await _loader.LoadAgentToolsAsync(agentId, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        _toolRegistryMock.Verify(
            r => r.GetAIFunctionsBySystemName("vector_store"),
            Times.Once);
        _toolRegistryMock.Verify(
            r => r.GetAIFunctionsBySystemName("web_search"),
            Times.Never);
    }

    #endregion
}
