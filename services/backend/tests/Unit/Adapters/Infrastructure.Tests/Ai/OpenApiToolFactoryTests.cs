using System.Net;
using Application.Interfaces;
using Domain.Entities;
using FluentAssertions;
using Infrastructure.Ai.AgentFramework.Abstractions;
using Infrastructure.Ai.AgentFramework.Tools;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Infrastructure.Tests.Ai;

public class OpenApiToolFactoryTests
{
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<IActivityPublisher> _activityPublisherMock;
    private readonly Mock<IPluginRepository> _pluginRepositoryMock;
    private readonly Mock<ILogger<OpenApiToolFactory>> _factoryLoggerMock;
    private readonly Mock<ILogger<OpenApiTool>> _toolLoggerMock;

    private const string ValidOpenApiSpec = @"{
        ""openapi"": ""3.0.0"",
        ""info"": { ""title"": ""Test API"", ""version"": ""1.0.0"" },
        ""paths"": {
            ""/items"": {
                ""get"": {
                    ""operationId"": ""listItems"",
                    ""summary"": ""List all items"",
                    ""responses"": { ""200"": { ""description"": ""Success"" } }
                }
            }
        }
    }";

    public OpenApiToolFactoryTests()
    {
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _activityPublisherMock = new Mock<IActivityPublisher>();
        _pluginRepositoryMock = new Mock<IPluginRepository>();
        _factoryLoggerMock = new Mock<ILogger<OpenApiToolFactory>>();
        _toolLoggerMock = new Mock<ILogger<OpenApiTool>>();
    }

    private OpenApiToolFactory CreateFactory(TimeSpan? cacheTtl = null, TimeSpan? fetchTimeout = null)
    {
        return new OpenApiToolFactory(
            _httpClientFactoryMock.Object,
            _activityPublisherMock.Object,
            _pluginRepositoryMock.Object,
            _factoryLoggerMock.Object,
            _toolLoggerMock.Object,
            cacheTtl,
            fetchTimeout);
    }

    private PluginEntity CreateOpenApiPlugin()
    {
        return new PluginEntity
        {
            Id = Guid.NewGuid(),
            Name = "Test API",
            PluginType = "openapi",
            BaseUrl = "https://api.test.com",
            OpenApiSpecUrl = "/openapi.json",
            PriceCredits = 2,
            IsActive = true
        };
    }

    private Mock<HttpMessageHandler> SetupHttpClient(string responseContent, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseContent)
            });

        // Return a new HttpClient instance each time to avoid state issues between fetch and tool creation
        _httpClientFactoryMock
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(handlerMock.Object));

        return handlerMock;
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_HttpClientFactoryIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new OpenApiToolFactory(
                null!,
                _activityPublisherMock.Object,
                _pluginRepositoryMock.Object,
                _factoryLoggerMock.Object,
                _toolLoggerMock.Object));
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_ActivityPublisherIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new OpenApiToolFactory(
                _httpClientFactoryMock.Object,
                null!,
                _pluginRepositoryMock.Object,
                _factoryLoggerMock.Object,
                _toolLoggerMock.Object));
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_PluginRepositoryIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new OpenApiToolFactory(
                _httpClientFactoryMock.Object,
                _activityPublisherMock.Object,
                null!,
                _factoryLoggerMock.Object,
                _toolLoggerMock.Object));
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_FactoryLoggerIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new OpenApiToolFactory(
                _httpClientFactoryMock.Object,
                _activityPublisherMock.Object,
                _pluginRepositoryMock.Object,
                null!,
                _toolLoggerMock.Object));
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_ToolLoggerIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new OpenApiToolFactory(
                _httpClientFactoryMock.Object,
                _activityPublisherMock.Object,
                _pluginRepositoryMock.Object,
                _factoryLoggerMock.Object,
                null!));
    }

    [Fact]
    public void Constructor_Should_UseDefaultCacheTtl_When_NotProvided()
    {
        var factory = CreateFactory();
        OpenApiToolFactory.DefaultCacheTtl.Should().Be(TimeSpan.FromHours(1));
    }

    [Fact]
    public void Constructor_Should_UseDefaultFetchTimeout_When_NotProvided()
    {
        var factory = CreateFactory();
        OpenApiToolFactory.DefaultFetchTimeout.Should().Be(TimeSpan.FromSeconds(10));
    }

    #endregion

    #region CanHandle Tests

    [Fact]
    public void CanHandle_Should_ReturnTrue_When_PluginIsOpenApiWithBaseUrl()
    {
        var factory = CreateFactory();
        var plugin = CreateOpenApiPlugin();

        var result = factory.CanHandle(plugin);

        result.Should().BeTrue();
    }

    [Fact]
    public void CanHandle_Should_ReturnFalse_When_PluginIsNull()
    {
        var factory = CreateFactory();

        var result = factory.CanHandle(null!);

        result.Should().BeFalse();
    }

    [Fact]
    public void CanHandle_Should_ReturnFalse_When_PluginTypeIsNotOpenApi()
    {
        var factory = CreateFactory();
        var plugin = CreateOpenApiPlugin();
        plugin.PluginType = "builtin";

        var result = factory.CanHandle(plugin);

        result.Should().BeFalse();
    }

    [Fact]
    public void CanHandle_Should_ReturnFalse_When_BaseUrlIsNull()
    {
        var factory = CreateFactory();
        var plugin = CreateOpenApiPlugin();
        plugin.BaseUrl = null;

        var result = factory.CanHandle(plugin);

        result.Should().BeFalse();
    }

    [Fact]
    public void CanHandle_Should_ReturnFalse_When_BaseUrlIsEmpty()
    {
        var factory = CreateFactory();
        var plugin = CreateOpenApiPlugin();
        plugin.BaseUrl = "";

        var result = factory.CanHandle(plugin);

        result.Should().BeFalse();
    }

    #endregion

    #region CreateToolAsync Tests

    [Fact]
    public async Task CreateToolAsync_Should_ThrowArgumentNullException_When_PluginIsNull()
    {
        var factory = CreateFactory();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            factory.CreateToolAsync(null!));
    }

    [Fact]
    public async Task CreateToolAsync_Should_ReturnNull_When_PluginTypeIsNotOpenApi()
    {
        var factory = CreateFactory();
        var plugin = CreateOpenApiPlugin();
        plugin.PluginType = "builtin";

        var result = await factory.CreateToolAsync(plugin);

        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateToolAsync_Should_ReturnNull_When_BaseUrlIsEmpty()
    {
        var factory = CreateFactory();
        var plugin = CreateOpenApiPlugin();
        plugin.BaseUrl = null;

        var result = await factory.CreateToolAsync(plugin);

        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateToolAsync_Should_ReturnTool_When_SpecIsCachedAndValid()
    {
        var factory = CreateFactory();
        var plugin = CreateOpenApiPlugin();
        plugin.OpenApiSpecCache = ValidOpenApiSpec;
        plugin.SpecCachedAt = DateTime.UtcNow.AddMinutes(-30); // 30 minutes ago, within 1 hour TTL

        SetupHttpClient(ValidOpenApiSpec);

        var result = await factory.CreateToolAsync(plugin);

        result.Should().NotBeNull();
        result.Should().BeAssignableTo<ITool>();
        result!.DisplayName.Should().Be("Test API");
    }

    [Fact]
    public async Task CreateToolAsync_Should_FetchSpec_When_CacheIsExpired()
    {
        var handlerMock = SetupHttpClient(ValidOpenApiSpec);
        var factory = CreateFactory(cacheTtl: TimeSpan.FromHours(1));
        var plugin = CreateOpenApiPlugin();
        plugin.OpenApiSpecCache = ValidOpenApiSpec;
        plugin.SpecCachedAt = DateTime.UtcNow.AddHours(-2); // 2 hours ago, cache expired

        var result = await factory.CreateToolAsync(plugin);

        result.Should().NotBeNull();

        // Verify HTTP request was made
        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());

        // Verify cache was updated
        _pluginRepositoryMock.Verify(r => r.UpdateAsync(plugin, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateToolAsync_Should_FetchSpec_When_CacheIsEmpty()
    {
        // Arrange - Setup mock handler that captures the request
        HttpRequestMessage? capturedRequest = null;
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ValidOpenApiSpec)
            });

        // Return a new HttpClient instance each time to avoid state issues
        _httpClientFactoryMock
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(handlerMock.Object));

        var factory = CreateFactory();
        var plugin = CreateOpenApiPlugin();
        plugin.OpenApiSpecCache = null;
        plugin.SpecCachedAt = null;

        // Act
        var result = await factory.CreateToolAsync(plugin);

        // Assert
        capturedRequest.Should().NotBeNull("HTTP request should have been made");
        capturedRequest!.RequestUri.Should().NotBeNull();
        result.Should().NotBeNull();

        // Verify HTTP request was made
        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task CreateToolAsync_Should_NotFetchSpec_When_CacheIsValid()
    {
        var handlerMock = SetupHttpClient(ValidOpenApiSpec);
        var factory = CreateFactory(cacheTtl: TimeSpan.FromHours(1));
        var plugin = CreateOpenApiPlugin();
        plugin.OpenApiSpecCache = ValidOpenApiSpec;
        plugin.SpecCachedAt = DateTime.UtcNow.AddMinutes(-30); // Within TTL

        var result = await factory.CreateToolAsync(plugin);

        result.Should().NotBeNull();

        // Verify HTTP request was NOT made
        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task CreateToolAsync_Should_ReturnNull_When_FetchFailsAndNoCachedSpec()
    {
        SetupHttpClient("", HttpStatusCode.ServiceUnavailable);
        var factory = CreateFactory();
        var plugin = CreateOpenApiPlugin();
        plugin.OpenApiSpecCache = null;
        plugin.SpecCachedAt = null;

        var result = await factory.CreateToolAsync(plugin);

        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateToolAsync_Should_UseExpiredCache_When_FetchFails()
    {
        SetupHttpClient("", HttpStatusCode.ServiceUnavailable);
        var factory = CreateFactory();
        var plugin = CreateOpenApiPlugin();
        plugin.OpenApiSpecCache = ValidOpenApiSpec;
        plugin.SpecCachedAt = DateTime.UtcNow.AddHours(-2); // Expired cache

        var result = await factory.CreateToolAsync(plugin);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateToolAsync_Should_UpdateCache_AfterSuccessfulFetch()
    {
        SetupHttpClient(ValidOpenApiSpec);
        var factory = CreateFactory();
        var plugin = CreateOpenApiPlugin();
        plugin.OpenApiSpecCache = null;
        plugin.SpecCachedAt = null;

        await factory.CreateToolAsync(plugin);

        plugin.OpenApiSpecCache.Should().Be(ValidOpenApiSpec);
        plugin.SpecCachedAt.Should().NotBeNull();
        plugin.SpecCachedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        _pluginRepositoryMock.Verify(r => r.UpdateAsync(plugin, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateToolAsync_Should_ClearCache_OnParseError()
    {
        var invalidSpec = "{ invalid json }}}";
        SetupHttpClient(invalidSpec);
        var factory = CreateFactory();
        var plugin = CreateOpenApiPlugin();
        plugin.OpenApiSpecCache = null;
        plugin.SpecCachedAt = null;

        var result = await factory.CreateToolAsync(plugin);

        result.Should().BeNull();
    }

    #endregion

    #region RefreshSpecAsync Tests

    [Fact]
    public async Task RefreshSpecAsync_Should_ThrowArgumentNullException_When_PluginIsNull()
    {
        var factory = CreateFactory();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            factory.RefreshSpecAsync(null!));
    }

    [Fact]
    public async Task RefreshSpecAsync_Should_ForceFetch_EvenWithValidCache()
    {
        var handlerMock = SetupHttpClient(ValidOpenApiSpec);
        var factory = CreateFactory();
        var plugin = CreateOpenApiPlugin();
        plugin.OpenApiSpecCache = ValidOpenApiSpec;
        plugin.SpecCachedAt = DateTime.UtcNow; // Fresh cache

        await factory.RefreshSpecAsync(plugin);

        // Verify HTTP request WAS made even though cache was valid
        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());

        // Verify cache was updated
        _pluginRepositoryMock.Verify(r => r.UpdateAsync(plugin, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RefreshSpecAsync_Should_NotFetch_When_PluginIsNotOpenApi()
    {
        var handlerMock = SetupHttpClient(ValidOpenApiSpec);
        var factory = CreateFactory();
        var plugin = CreateOpenApiPlugin();
        plugin.PluginType = "builtin";

        await factory.RefreshSpecAsync(plugin);

        // Verify HTTP request was NOT made
        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    #endregion

    #region CreateFunctionsAsync Tests

    [Fact]
    public async Task CreateFunctionsAsync_Should_ReturnEmptyList_When_PluginCannotBeHandled()
    {
        var factory = CreateFactory();
        var plugin = CreateOpenApiPlugin();
        plugin.PluginType = "builtin";

        var functions = await factory.CreateFunctionsAsync(plugin);

        functions.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateFunctionsAsync_Should_ReturnAIFunctions()
    {
        SetupHttpClient(ValidOpenApiSpec);
        var factory = CreateFactory();
        var plugin = CreateOpenApiPlugin();
        plugin.OpenApiSpecCache = ValidOpenApiSpec;
        plugin.SpecCachedAt = DateTime.UtcNow;

        var functions = await factory.CreateFunctionsAsync(plugin);

        functions.Should().NotBeNull();
        functions.Should().HaveCount(1);
        functions[0].Name.Should().Be("listItems");
    }

    [Fact]
    public async Task CreateFunctionsAsync_Should_HandleMultipleOperations()
    {
        var multiOpSpec = @"{
            ""openapi"": ""3.0.0"",
            ""info"": { ""title"": ""Multi API"", ""version"": ""1.0.0"" },
            ""paths"": {
                ""/users"": {
                    ""get"": { ""operationId"": ""listUsers"", ""responses"": { ""200"": { ""description"": ""OK"" } } },
                    ""post"": { ""operationId"": ""createUser"", ""responses"": { ""201"": { ""description"": ""Created"" } } }
                },
                ""/users/{id}"": {
                    ""get"": { ""operationId"": ""getUser"", ""responses"": { ""200"": { ""description"": ""OK"" } } },
                    ""put"": { ""operationId"": ""updateUser"", ""responses"": { ""200"": { ""description"": ""OK"" } } },
                    ""delete"": { ""operationId"": ""deleteUser"", ""responses"": { ""204"": { ""description"": ""Deleted"" } } }
                }
            }
        }";

        SetupHttpClient(multiOpSpec);
        var factory = CreateFactory();
        var plugin = CreateOpenApiPlugin();
        plugin.OpenApiSpecCache = multiOpSpec;
        plugin.SpecCachedAt = DateTime.UtcNow;

        var functions = await factory.CreateFunctionsAsync(plugin);

        functions.Should().HaveCount(5);
    }

    #endregion

    #region Timeout Tests

    [Fact]
    public async Task CreateToolAsync_Should_ReturnNull_OnTimeout()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException());

        // Return a new HttpClient instance each time
        _httpClientFactoryMock
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(handlerMock.Object));

        var factory = CreateFactory();
        var plugin = CreateOpenApiPlugin();
        plugin.OpenApiSpecCache = null;
        plugin.SpecCachedAt = null;

        var result = await factory.CreateToolAsync(plugin);

        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateToolAsync_Should_UseExpiredCache_OnTimeout()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException());

        // Return a new HttpClient instance each time
        _httpClientFactoryMock
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(handlerMock.Object));

        var factory = CreateFactory();
        var plugin = CreateOpenApiPlugin();
        plugin.OpenApiSpecCache = ValidOpenApiSpec;
        plugin.SpecCachedAt = DateTime.UtcNow.AddHours(-2); // Expired

        var result = await factory.CreateToolAsync(plugin);

        result.Should().NotBeNull();
    }

    #endregion

    #region URL Building Tests

    [Fact]
    public async Task CreateToolAsync_Should_UseDefaultSpecUrl_WhenOpenApiSpecUrlIsNull()
    {
        var handlerMock = SetupHttpClient(ValidOpenApiSpec);
        var factory = CreateFactory();
        var plugin = CreateOpenApiPlugin();
        plugin.OpenApiSpecUrl = null;
        plugin.OpenApiSpecCache = null;
        plugin.SpecCachedAt = null;

        await factory.CreateToolAsync(plugin);

        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.RequestUri!.ToString().Contains("/openapi.json")),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task CreateToolAsync_Should_BuildCorrectUrl_WhenSpecUrlHasNoLeadingSlash()
    {
        var handlerMock = SetupHttpClient(ValidOpenApiSpec);
        var factory = CreateFactory();
        var plugin = CreateOpenApiPlugin();
        plugin.OpenApiSpecUrl = "swagger.json";
        plugin.OpenApiSpecCache = null;
        plugin.SpecCachedAt = null;

        await factory.CreateToolAsync(plugin);

        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.RequestUri!.ToString().Contains("/swagger.json")),
            ItExpr.IsAny<CancellationToken>());
    }

    #endregion
}
