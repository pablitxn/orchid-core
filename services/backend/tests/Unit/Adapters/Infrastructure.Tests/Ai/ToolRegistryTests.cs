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
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<ILogger<ToolRegistry>> _loggerMock;

    public ToolRegistryTests()
    {
        _serviceProviderMock = new Mock<IServiceProvider>();
        _loggerMock = new Mock<ILogger<ToolRegistry>>();
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
            new ToolRegistry(_serviceProviderMock.Object, null!));
    }

    #endregion

    #region GetToolBySystemName Tests

    [Fact]
    public void GetToolBySystemName_Should_ReturnNull_When_SystemNameIsNull()
    {
        // Arrange
        var registry = new ToolRegistry(_serviceProviderMock.Object, _loggerMock.Object);

        // Act
        var result = registry.GetToolBySystemName(null!);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetToolBySystemName_Should_ReturnNull_When_SystemNameIsEmpty()
    {
        // Arrange
        var registry = new ToolRegistry(_serviceProviderMock.Object, _loggerMock.Object);

        // Act
        var result = registry.GetToolBySystemName("");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetToolBySystemName_Should_ReturnNull_When_SystemNameIsWhitespace()
    {
        // Arrange
        var registry = new ToolRegistry(_serviceProviderMock.Object, _loggerMock.Object);

        // Act
        var result = registry.GetToolBySystemName("   ");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetToolBySystemName_Should_ReturnNull_When_UnknownToolRequested()
    {
        // Arrange
        var registry = new ToolRegistry(_serviceProviderMock.Object, _loggerMock.Object);

        // Act
        var result = registry.GetToolBySystemName("unknown_tool");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetToolBySystemName_Should_ReturnNull_When_ServiceResolutionFails()
    {
        // Arrange
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(VectorStoreTool)))
            .Throws(new InvalidOperationException("Service not registered"));

        var registry = new ToolRegistry(_serviceProviderMock.Object, _loggerMock.Object);

        // Act
        var result = registry.GetToolBySystemName("vector_store");

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetAIFunctionsBySystemName Tests

    [Fact]
    public void GetAIFunctionsBySystemName_Should_ReturnEmptyList_When_ToolNotFound()
    {
        // Arrange
        var registry = new ToolRegistry(_serviceProviderMock.Object, _loggerMock.Object);

        // Act
        var result = registry.GetAIFunctionsBySystemName("unknown_tool");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetAIFunctionsBySystemName_Should_ReturnEmptyList_When_ServiceFails()
    {
        // Arrange
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(VectorStoreTool)))
            .Throws(new InvalidOperationException("Service not registered"));

        var registry = new ToolRegistry(_serviceProviderMock.Object, _loggerMock.Object);

        // Act
        var result = registry.GetAIFunctionsBySystemName("vector_store");

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region IsToolRegistered Tests

    [Fact]
    public void IsToolRegistered_Should_ReturnFalse_When_SystemNameIsNullOrEmpty()
    {
        // Arrange
        var registry = new ToolRegistry(_serviceProviderMock.Object, _loggerMock.Object);

        // Act & Assert
        registry.IsToolRegistered(null!).Should().BeFalse();
        registry.IsToolRegistered("").Should().BeFalse();
        registry.IsToolRegistered("   ").Should().BeFalse();
    }

    [Fact]
    public void IsToolRegistered_Should_ReturnTrue_ForKnownTools()
    {
        // Arrange
        var registry = new ToolRegistry(_serviceProviderMock.Object, _loggerMock.Object);

        // Act & Assert
        registry.IsToolRegistered("vector_store").Should().BeTrue();
        registry.IsToolRegistered("web_search").Should().BeTrue();
        registry.IsToolRegistered("vectorstore").Should().BeTrue();
        registry.IsToolRegistered("websearch").Should().BeTrue();
    }

    [Fact]
    public void IsToolRegistered_Should_ReturnFalse_ForUnknownTools()
    {
        // Arrange
        var registry = new ToolRegistry(_serviceProviderMock.Object, _loggerMock.Object);

        // Act & Assert
        registry.IsToolRegistered("unknown_tool").Should().BeFalse();
        registry.IsToolRegistered("custom_tool").Should().BeFalse();
    }

    [Fact]
    public void IsToolRegistered_Should_BeCaseInsensitive()
    {
        // Arrange
        var registry = new ToolRegistry(_serviceProviderMock.Object, _loggerMock.Object);

        // Act & Assert
        registry.IsToolRegistered("VECTOR_STORE").Should().BeTrue();
        registry.IsToolRegistered("Vector_Store").Should().BeTrue();
        registry.IsToolRegistered("WEB_SEARCH").Should().BeTrue();
    }

    #endregion
}
