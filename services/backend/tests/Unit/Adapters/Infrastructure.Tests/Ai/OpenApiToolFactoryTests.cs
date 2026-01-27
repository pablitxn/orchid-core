using System.Net;
using Application.Interfaces;
using Domain.Entities;
using FluentAssertions;
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
    private readonly Mock<ILogger<OpenApiTool>> _loggerMock;

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
        _loggerMock = new Mock<ILogger<OpenApiTool>>();
    }

    private OpenApiToolFactory CreateFactory()
    {
        return new OpenApiToolFactory(
            _httpClientFactoryMock.Object,
            _activityPublisherMock.Object,
            _loggerMock.Object);
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
            OpenApiSpecCache = ValidOpenApiSpec,
            SpecCachedAt = DateTime.UtcNow,
            PriceCredits = 2,
            IsActive = true
        };
    }

    private void SetupHttpClient(string responseContent)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseContent)
            });

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://api.test.com")
        };
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_HttpClientFactoryIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new OpenApiToolFactory(null!, _activityPublisherMock.Object, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_ActivityPublisherIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new OpenApiToolFactory(_httpClientFactoryMock.Object, null!, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_LoggerIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new OpenApiToolFactory(_httpClientFactoryMock.Object, _activityPublisherMock.Object, null!));
    }

    #endregion

    #region CreateFunctionsAsync Tests

    [Fact]
    public async Task CreateFunctionsAsync_Should_ThrowArgumentNullException_When_PluginIsNull()
    {
        // Arrange
        var factory = CreateFactory();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            factory.CreateFunctionsAsync(null!));
    }

    [Fact]
    public async Task CreateFunctionsAsync_Should_ThrowArgumentException_When_PluginTypeIsNotOpenApi()
    {
        // Arrange
        var factory = CreateFactory();
        var plugin = CreateOpenApiPlugin();
        plugin.PluginType = "builtin";

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            factory.CreateFunctionsAsync(plugin));
        ex.Message.Should().Contain("not an OpenAPI plugin");
    }

    [Fact]
    public async Task CreateFunctionsAsync_Should_ThrowArgumentException_When_BaseUrlIsEmpty()
    {
        // Arrange
        var factory = CreateFactory();
        var plugin = CreateOpenApiPlugin();
        plugin.BaseUrl = null;

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            factory.CreateFunctionsAsync(plugin));
        ex.Message.Should().Contain("BaseUrl");
    }

    [Fact]
    public async Task CreateFunctionsAsync_Should_ReturnAIFunctions()
    {
        // Arrange
        SetupHttpClient(ValidOpenApiSpec);
        var factory = CreateFactory();
        var plugin = CreateOpenApiPlugin();

        // Act
        var functions = await factory.CreateFunctionsAsync(plugin);

        // Assert
        functions.Should().NotBeNull();
        functions.Should().HaveCount(1);
        functions[0].Name.Should().Be("listItems");
    }

    [Fact]
    public async Task CreateFunctionsAsync_Should_HandleMultipleOperations()
    {
        // Arrange
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

        // Act
        var functions = await factory.CreateFunctionsAsync(plugin);

        // Assert
        functions.Should().HaveCount(5);
    }

    [Fact]
    public async Task CreateFunctionsAsync_Should_SupportCancellation()
    {
        // Arrange
        var factory = CreateFactory();
        var plugin = CreateOpenApiPlugin();
        plugin.OpenApiSpecCache = null;
        plugin.SpecCachedAt = null;

        var cts = new CancellationTokenSource();
        cts.Cancel();

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://api.test.com")
        };
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            factory.CreateFunctionsAsync(plugin, cts.Token));
    }

    #endregion
}
