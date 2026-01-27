using Application.Interfaces;
using Domain.Entities;
using FluentAssertions;
using Infrastructure.Ai.AgentFramework.Abstractions;
using Infrastructure.Ai.AgentFramework.Services;
using Infrastructure.Ai.AgentFramework.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Infrastructure.Tests.Ai;

public class ToolRegistryTests
{
    private readonly Mock<ILogger<ToolRegistry>> _loggerMock;
    private readonly Mock<IOpenApiToolFactory> _openApiFactoryMock;
    private readonly Mock<IPluginRepository> _pluginRepositoryMock;
    private readonly ServiceProvider _serviceProvider;

    public ToolRegistryTests()
    {
        _loggerMock = new Mock<ILogger<ToolRegistry>>();
        _openApiFactoryMock = new Mock<IOpenApiToolFactory>();
        _pluginRepositoryMock = new Mock<IPluginRepository>();

        // Setup real service collection with mocked dependencies
        var services = new ServiceCollection();

        // Add mocked dependencies for VectorStoreTool
        services.AddSingleton(new Mock<ILogger<VectorStoreTool>>().Object);
        services.AddSingleton(new Mock<MediatR.IMediator>().Object);

        // Add mocked dependencies for WebSearchTool
        services.AddSingleton(new Mock<ILogger<WebSearchTool>>().Object);
        services.AddSingleton(new Mock<Application.Interfaces.ISearchProvider>().Object);
        services.AddSingleton(new Mock<IHttpClientFactory>().Object);
        services.AddSingleton(new Mock<Application.Interfaces.IChatCompletionPort>().Object);
        services.AddSingleton(new Mock<Application.Interfaces.IEmbeddingGeneratorPort>().Object);
        services.AddSingleton(new Mock<Application.Interfaces.IVectorStorePort>().Object);
        services.AddSingleton(new Mock<Application.Interfaces.IActivityPublisher>().Object);

        // Register the actual tools
        services.AddTransient<VectorStoreTool>();
        services.AddTransient<WebSearchTool>();

        _serviceProvider = services.BuildServiceProvider();
    }

    private ToolRegistry CreateRegistry()
    {
        return new ToolRegistry(_serviceProvider, _loggerMock.Object);
    }

    private ToolRegistry CreateRegistryWithOpenApiSupport()
    {
        return new ToolRegistry(
            _serviceProvider,
            _loggerMock.Object,
            _openApiFactoryMock.Object,
            _pluginRepositoryMock.Object);
    }

    private ToolRegistry CreateRegistryWithFailingServiceProvider()
    {
        var failingServiceProviderMock = new Mock<IServiceProvider>();
        failingServiceProviderMock
            .Setup(sp => sp.GetService(It.IsAny<Type>()))
            .Returns((Type _) => null);

        return new ToolRegistry(failingServiceProviderMock.Object, _loggerMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_ServiceProviderIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ToolRegistry(null!, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_LoggerIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ToolRegistry(_serviceProvider, null!));
    }

    [Fact]
    public void Constructor_Should_CreateInstanceSuccessfully()
    {
        // Act
        var registry = CreateRegistry();

        // Assert
        registry.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_Should_AcceptOptionalDependencies()
    {
        // Act
        var registry = CreateRegistryWithOpenApiSupport();

        // Assert
        registry.Should().NotBeNull();
    }

    #endregion

    #region GetToolBySystemName Tests

    [Fact]
    public void GetToolBySystemName_Should_ReturnNull_When_NameIsNull()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var result = registry.GetToolBySystemName(null!);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetToolBySystemName_Should_ReturnNull_When_NameIsEmpty()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var result = registry.GetToolBySystemName("");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetToolBySystemName_Should_ReturnNull_When_NameIsWhitespace()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var result = registry.GetToolBySystemName("   ");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetToolBySystemName_Should_ReturnVectorStoreTool_When_NameIsVectorStore()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var result = registry.GetToolBySystemName("vector_store");

        // Assert
        result.Should().NotBeNull();
        result!.SystemName.Should().Be("vector_store");
        result.Should().BeOfType<VectorStoreTool>();
    }

    [Fact]
    public void GetToolBySystemName_Should_ReturnVectorStoreTool_When_NameIsVectorstoreAlias()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var result = registry.GetToolBySystemName("vectorstore");

        // Assert
        result.Should().NotBeNull();
        result!.SystemName.Should().Be("vector_store");
    }

    [Fact]
    public void GetToolBySystemName_Should_ReturnWebSearchTool_When_NameIsWebSearch()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var result = registry.GetToolBySystemName("web_search");

        // Assert
        result.Should().NotBeNull();
        result!.SystemName.Should().Be("web_search");
        result.Should().BeOfType<WebSearchTool>();
    }

    [Fact]
    public void GetToolBySystemName_Should_ReturnWebSearchTool_When_NameIsWebsearchAlias()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var result = registry.GetToolBySystemName("websearch");

        // Assert
        result.Should().NotBeNull();
        result!.SystemName.Should().Be("web_search");
    }

    [Fact]
    public void GetToolBySystemName_Should_BeCaseInsensitive()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act & Assert
        registry.GetToolBySystemName("VECTOR_STORE").Should().NotBeNull();
        registry.GetToolBySystemName("Vector_Store").Should().NotBeNull();
        registry.GetToolBySystemName("WEB_SEARCH").Should().NotBeNull();
        registry.GetToolBySystemName("Web_Search").Should().NotBeNull();
    }

    [Fact]
    public void GetToolBySystemName_Should_TrimWhitespace()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var result = registry.GetToolBySystemName("  vector_store  ");

        // Assert
        result.Should().NotBeNull();
        result!.SystemName.Should().Be("vector_store");
    }

    [Fact]
    public void GetToolBySystemName_Should_ReturnNull_When_ToolNotRegistered()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var result = registry.GetToolBySystemName("unknown_tool");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetToolBySystemName_Should_ReturnNull_When_ServiceResolutionFails()
    {
        // Arrange
        var registry = CreateRegistryWithFailingServiceProvider();

        // Act
        var result = registry.GetToolBySystemName("vector_store");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetToolBySystemName_Should_LogWarning_When_NameIsNullOrEmpty()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        registry.GetToolBySystemName("");

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                Microsoft.Extensions.Logging.LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("null or empty")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void GetToolBySystemName_Should_LogDebug_When_ToolNotFound()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        registry.GetToolBySystemName("nonexistent_tool");

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                Microsoft.Extensions.Logging.LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Builtin tool not found")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region GetToolBySystemNameAsync Tests

    [Fact]
    public async Task GetToolBySystemNameAsync_Should_ReturnNull_When_NameIsNull()
    {
        // Arrange
        var registry = CreateRegistryWithOpenApiSupport();

        // Act
        var result = await registry.GetToolBySystemNameAsync(null!);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetToolBySystemNameAsync_Should_ReturnNull_When_NameIsEmpty()
    {
        // Arrange
        var registry = CreateRegistryWithOpenApiSupport();

        // Act
        var result = await registry.GetToolBySystemNameAsync("");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetToolBySystemNameAsync_Should_ReturnBuiltinTool_When_Found()
    {
        // Arrange
        var registry = CreateRegistryWithOpenApiSupport();

        // Act
        var result = await registry.GetToolBySystemNameAsync("vector_store");

        // Assert
        result.Should().NotBeNull();
        result!.SystemName.Should().Be("vector_store");
        result.Should().BeOfType<VectorStoreTool>();

        // Should not query database for builtin tools
        _pluginRepositoryMock.Verify(
            x => x.GetBySystemNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetToolBySystemNameAsync_Should_QueryDatabase_When_BuiltinNotFound()
    {
        // Arrange
        var registry = CreateRegistryWithOpenApiSupport();
        _pluginRepositoryMock
            .Setup(x => x.GetBySystemNameAsync("custom_api", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PluginEntity?)null);

        // Act
        var result = await registry.GetToolBySystemNameAsync("custom_api");

        // Assert
        result.Should().BeNull();
        _pluginRepositoryMock.Verify(
            x => x.GetBySystemNameAsync("custom_api", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetToolBySystemNameAsync_Should_ReturnOpenApiTool_When_PluginFound()
    {
        // Arrange
        var plugin = new PluginEntity
        {
            Id = Guid.NewGuid(),
            Name = "Custom API",
            SystemName = "custom_api",
            PluginType = "openapi",
            IsActive = true,
            BaseUrl = "https://api.example.com",
            OpenApiSpecUrl = "/openapi.json"
        };

        var mockTool = new Mock<ITool>();
        mockTool.Setup(t => t.SystemName).Returns("custom_api");

        var registry = CreateRegistryWithOpenApiSupport();

        _pluginRepositoryMock
            .Setup(x => x.GetBySystemNameAsync("custom_api", It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin);

        _openApiFactoryMock
            .Setup(x => x.CanHandle(plugin))
            .Returns(true);

        _openApiFactoryMock
            .Setup(x => x.CreateToolAsync(plugin, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockTool.Object);

        // Act
        var result = await registry.GetToolBySystemNameAsync("custom_api");

        // Assert
        result.Should().NotBeNull();
        result!.SystemName.Should().Be("custom_api");
    }

    [Fact]
    public async Task GetToolBySystemNameAsync_Should_ReturnNull_When_PluginNotActive()
    {
        // Arrange
        var plugin = new PluginEntity
        {
            Id = Guid.NewGuid(),
            Name = "Inactive API",
            SystemName = "inactive_api",
            PluginType = "openapi",
            IsActive = false
        };

        var registry = CreateRegistryWithOpenApiSupport();

        _pluginRepositoryMock
            .Setup(x => x.GetBySystemNameAsync("inactive_api", It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin);

        // Act
        var result = await registry.GetToolBySystemNameAsync("inactive_api");

        // Assert
        result.Should().BeNull();

        // Should not attempt to create tool for inactive plugin
        _openApiFactoryMock.Verify(
            x => x.CreateToolAsync(It.IsAny<PluginEntity>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetToolBySystemNameAsync_Should_ReturnNull_When_FactoryCannotHandle()
    {
        // Arrange
        var plugin = new PluginEntity
        {
            Id = Guid.NewGuid(),
            Name = "Builtin Plugin",
            SystemName = "builtin_plugin",
            PluginType = "builtin",
            IsActive = true
        };

        var registry = CreateRegistryWithOpenApiSupport();

        _pluginRepositoryMock
            .Setup(x => x.GetBySystemNameAsync("builtin_plugin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin);

        _openApiFactoryMock
            .Setup(x => x.CanHandle(plugin))
            .Returns(false);

        // Act
        var result = await registry.GetToolBySystemNameAsync("builtin_plugin");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetToolBySystemNameAsync_Should_ReturnNull_When_NoDependenciesConfigured()
    {
        // Arrange - Registry without OpenAPI support
        var registry = CreateRegistry();

        // Act
        var result = await registry.GetToolBySystemNameAsync("custom_api");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetToolBySystemNameAsync_Should_HandleException_Gracefully()
    {
        // Arrange
        var registry = CreateRegistryWithOpenApiSupport();

        _pluginRepositoryMock
            .Setup(x => x.GetBySystemNameAsync("error_api", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act
        var result = await registry.GetToolBySystemNameAsync("error_api");

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetAIFunctionsBySystemName Tests

    [Fact]
    public void GetAIFunctionsBySystemName_Should_ReturnEmpty_When_ToolNotFound()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var result = registry.GetAIFunctionsBySystemName("unknown_tool");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetAIFunctionsBySystemName_Should_ReturnFunctions_When_ToolExists()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var result = registry.GetAIFunctionsBySystemName("vector_store");

        // Assert
        result.Should().NotBeEmpty();
        result.Should().HaveCount(2); // VectorStoreTool has 2 functions
    }

    [Fact]
    public void GetAIFunctionsBySystemName_Should_ReturnWebSearchFunctions()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var result = registry.GetAIFunctionsBySystemName("web_search");

        // Assert
        result.Should().NotBeEmpty();
        result.Should().HaveCount(5); // WebSearchTool has 5 functions
    }

    [Fact]
    public void GetAIFunctionsBySystemName_Should_ReturnEmpty_When_NameIsNull()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var result = registry.GetAIFunctionsBySystemName(null!);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetAIFunctionsBySystemName_Should_ReturnVectorStoreFunctionNames()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var result = registry.GetAIFunctionsBySystemName("vector_store");

        // Assert
        result.Should().Contain(f => f.Name == "AddDocument");
        result.Should().Contain(f => f.Name == "SearchDocuments");
    }

    [Fact]
    public void GetAIFunctionsBySystemName_Should_ReturnWebSearchFunctionNames()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var result = registry.GetAIFunctionsBySystemName("web_search");

        // Assert
        result.Should().Contain(f => f.Name == "SearchWeb");
        result.Should().Contain(f => f.Name == "FetchContent");
        result.Should().Contain(f => f.Name == "CleanContent");
        result.Should().Contain(f => f.Name == "SummarizeContent");
        result.Should().Contain(f => f.Name == "UpsertToVector");
    }

    #endregion

    #region GetAIFunctionsBySystemNameAsync Tests

    [Fact]
    public async Task GetAIFunctionsBySystemNameAsync_Should_ReturnBuiltinFunctions()
    {
        // Arrange
        var registry = CreateRegistryWithOpenApiSupport();

        // Act
        var result = await registry.GetAIFunctionsBySystemNameAsync("vector_store");

        // Assert
        result.Should().NotBeEmpty();
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAIFunctionsBySystemNameAsync_Should_ReturnOpenApiFunctions()
    {
        // Arrange
        var plugin = new PluginEntity
        {
            Id = Guid.NewGuid(),
            Name = "Custom API",
            SystemName = "custom_api",
            PluginType = "openapi",
            IsActive = true,
            BaseUrl = "https://api.example.com"
        };

        var mockFunctions = new List<AIFunction>
        {
            AIFunctionFactory.Create(() => "test", "TestFunction", "Test function description")
        };

        var mockTool = new Mock<ITool>();
        mockTool.Setup(t => t.SystemName).Returns("custom_api");
        mockTool.Setup(t => t.GetAIFunctions()).Returns(mockFunctions);

        var registry = CreateRegistryWithOpenApiSupport();

        _pluginRepositoryMock
            .Setup(x => x.GetBySystemNameAsync("custom_api", It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin);

        _openApiFactoryMock
            .Setup(x => x.CanHandle(plugin))
            .Returns(true);

        _openApiFactoryMock
            .Setup(x => x.CreateToolAsync(plugin, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockTool.Object);

        // Act
        var result = await registry.GetAIFunctionsBySystemNameAsync("custom_api");

        // Assert
        result.Should().NotBeEmpty();
        result.Should().HaveCount(1);
        result.Should().Contain(f => f.Name == "TestFunction");
    }

    [Fact]
    public async Task GetAIFunctionsBySystemNameAsync_Should_ReturnEmpty_When_ToolNotFound()
    {
        // Arrange
        var registry = CreateRegistryWithOpenApiSupport();

        _pluginRepositoryMock
            .Setup(x => x.GetBySystemNameAsync("unknown_api", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PluginEntity?)null);

        // Act
        var result = await registry.GetAIFunctionsBySystemNameAsync("unknown_api");

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region GetAllTools Tests

    [Fact]
    public void GetAllTools_Should_ReturnUniqueTools()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var result = registry.GetAllTools();

        // Assert - Should have 2 unique tools (vector_store and web_search), not 4 (including aliases)
        result.Should().HaveCount(2);
    }

    [Fact]
    public void GetAllTools_Should_ContainVectorStoreTool()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var result = registry.GetAllTools();

        // Assert
        result.Should().Contain(t => t.SystemName == "vector_store");
    }

    [Fact]
    public void GetAllTools_Should_ContainWebSearchTool()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var result = registry.GetAllTools();

        // Assert
        result.Should().Contain(t => t.SystemName == "web_search");
    }

    [Fact]
    public void GetAllTools_Should_ReturnToolsOfCorrectTypes()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var result = registry.GetAllTools();

        // Assert
        result.Should().Contain(t => t is VectorStoreTool);
        result.Should().Contain(t => t is WebSearchTool);
    }

    #endregion

    #region IsToolRegistered Tests

    [Fact]
    public void IsToolRegistered_Should_ReturnTrue_When_ToolExists()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act & Assert
        registry.IsToolRegistered("vector_store").Should().BeTrue();
        registry.IsToolRegistered("web_search").Should().BeTrue();
    }

    [Fact]
    public void IsToolRegistered_Should_ReturnTrue_ForAliases()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act & Assert
        registry.IsToolRegistered("vectorstore").Should().BeTrue();
        registry.IsToolRegistered("websearch").Should().BeTrue();
    }

    [Fact]
    public void IsToolRegistered_Should_ReturnFalse_When_ToolNotExists()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var result = registry.IsToolRegistered("unknown_tool");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsToolRegistered_Should_ReturnFalse_When_NameIsNull()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var result = registry.IsToolRegistered(null!);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsToolRegistered_Should_ReturnFalse_When_NameIsEmpty()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var result = registry.IsToolRegistered("");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsToolRegistered_Should_ReturnFalse_When_NameIsWhitespace()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var result = registry.IsToolRegistered("   ");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsToolRegistered_Should_BeCaseInsensitive()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act & Assert
        registry.IsToolRegistered("VECTOR_STORE").Should().BeTrue();
        registry.IsToolRegistered("Vector_Store").Should().BeTrue();
        registry.IsToolRegistered("WEB_SEARCH").Should().BeTrue();
    }

    [Fact]
    public void IsToolRegistered_Should_TrimWhitespace()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var result = registry.IsToolRegistered("  vector_store  ");

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region IsToolRegisteredAsync Tests

    [Fact]
    public async Task IsToolRegisteredAsync_Should_ReturnTrue_When_BuiltinToolExists()
    {
        // Arrange
        var registry = CreateRegistryWithOpenApiSupport();

        // Act
        var result = await registry.IsToolRegisteredAsync("vector_store");

        // Assert
        result.Should().BeTrue();

        // Should not query database for builtin tools
        _pluginRepositoryMock.Verify(
            x => x.GetBySystemNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task IsToolRegisteredAsync_Should_ReturnTrue_When_OpenApiPluginExists()
    {
        // Arrange
        var plugin = new PluginEntity
        {
            Id = Guid.NewGuid(),
            Name = "Custom API",
            SystemName = "custom_api",
            PluginType = "openapi",
            IsActive = true
        };

        var registry = CreateRegistryWithOpenApiSupport();

        _pluginRepositoryMock
            .Setup(x => x.GetBySystemNameAsync("custom_api", It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin);

        // Act
        var result = await registry.IsToolRegisteredAsync("custom_api");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsToolRegisteredAsync_Should_ReturnFalse_When_PluginNotActive()
    {
        // Arrange
        var plugin = new PluginEntity
        {
            Id = Guid.NewGuid(),
            Name = "Inactive API",
            SystemName = "inactive_api",
            PluginType = "openapi",
            IsActive = false
        };

        var registry = CreateRegistryWithOpenApiSupport();

        _pluginRepositoryMock
            .Setup(x => x.GetBySystemNameAsync("inactive_api", It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin);

        // Act
        var result = await registry.IsToolRegisteredAsync("inactive_api");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsToolRegisteredAsync_Should_ReturnFalse_When_PluginNotFound()
    {
        // Arrange
        var registry = CreateRegistryWithOpenApiSupport();

        _pluginRepositoryMock
            .Setup(x => x.GetBySystemNameAsync("unknown_api", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PluginEntity?)null);

        // Act
        var result = await registry.IsToolRegisteredAsync("unknown_api");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsToolRegisteredAsync_Should_ReturnFalse_When_NameIsNull()
    {
        // Arrange
        var registry = CreateRegistryWithOpenApiSupport();

        // Act
        var result = await registry.IsToolRegisteredAsync(null!);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsToolRegisteredAsync_Should_ReturnFalse_When_NoDependenciesConfigured()
    {
        // Arrange - Registry without OpenAPI support
        var registry = CreateRegistry();

        // Act
        var result = await registry.IsToolRegisteredAsync("custom_api");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsToolRegisteredAsync_Should_HandleException_Gracefully()
    {
        // Arrange
        var registry = CreateRegistryWithOpenApiSupport();

        _pluginRepositoryMock
            .Setup(x => x.GetBySystemNameAsync("error_api", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act
        var result = await registry.IsToolRegisteredAsync("error_api");

        // Assert
        result.Should().BeFalse();
    }

    #endregion
}
