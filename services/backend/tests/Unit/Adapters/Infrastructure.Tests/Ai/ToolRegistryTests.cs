using FluentAssertions;
using Infrastructure.Ai.AgentFramework.Services;
using Infrastructure.Ai.AgentFramework.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Infrastructure.Tests.Ai;

public class ToolRegistryTests
{
    private readonly Mock<ILogger<ToolRegistry>> _loggerMock;
    private readonly ServiceProvider _serviceProvider;

    public ToolRegistryTests()
    {
        _loggerMock = new Mock<ILogger<ToolRegistry>>();

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
    public void GetToolBySystemName_Should_LogWarning_When_ToolNotFound()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        registry.GetToolBySystemName("nonexistent_tool");

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                Microsoft.Extensions.Logging.LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Unknown tool")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
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
}
