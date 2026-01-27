using System.Net;
using System.Text.Json;
using Application.Interfaces;
using Domain.Entities;
using FluentAssertions;
using Infrastructure.Ai.AgentFramework.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Infrastructure.Tests.Ai;

public class OpenApiToolTests
{
    private readonly Mock<ILogger<OpenApiTool>> _loggerMock;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<IActivityPublisher> _activityPublisherMock;

    private const string PetStoreSpec = @"{
        ""openapi"": ""3.0.0"",
        ""info"": { ""title"": ""Pet Store"", ""version"": ""1.0.0"" },
        ""paths"": {
            ""/pets"": {
                ""get"": {
                    ""operationId"": ""listPets"",
                    ""summary"": ""List all pets"",
                    ""parameters"": [
                        { ""name"": ""limit"", ""in"": ""query"", ""schema"": { ""type"": ""integer"" } }
                    ],
                    ""responses"": { ""200"": { ""description"": ""Success"" } }
                },
                ""post"": {
                    ""operationId"": ""createPet"",
                    ""summary"": ""Create a pet"",
                    ""requestBody"": {
                        ""content"": { ""application/json"": { ""schema"": { ""type"": ""object"" } } }
                    },
                    ""responses"": { ""201"": { ""description"": ""Created"" } }
                }
            },
            ""/pets/{petId}"": {
                ""get"": {
                    ""operationId"": ""getPetById"",
                    ""summary"": ""Get a pet by ID"",
                    ""parameters"": [
                        { ""name"": ""petId"", ""in"": ""path"", ""required"": true, ""schema"": { ""type"": ""string"" } }
                    ],
                    ""responses"": { ""200"": { ""description"": ""Success"" } }
                },
                ""delete"": {
                    ""operationId"": ""deletePet"",
                    ""summary"": ""Delete a pet"",
                    ""parameters"": [
                        { ""name"": ""petId"", ""in"": ""path"", ""required"": true, ""schema"": { ""type"": ""string"" } }
                    ],
                    ""responses"": { ""204"": { ""description"": ""Deleted"" } }
                }
            }
        }
    }";

    public OpenApiToolTests()
    {
        _loggerMock = new Mock<ILogger<OpenApiTool>>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _activityPublisherMock = new Mock<IActivityPublisher>();
    }

    private PluginEntity CreateTestPlugin(string? specCache = null)
    {
        return new PluginEntity
        {
            Id = Guid.NewGuid(),
            Name = "Pet Store API",
            Description = "A sample pet store API",
            SystemName = "pet_store",
            PluginType = "openapi",
            BaseUrl = "https://api.petstore.com",
            OpenApiSpecUrl = "/openapi.json",
            OpenApiSpecCache = specCache ?? PetStoreSpec,
            SpecCachedAt = DateTime.UtcNow,
            PriceCredits = 5,
            IsSubscriptionBased = false,
            IsActive = true
        };
    }

    private Mock<HttpMessageHandler> CreateMockHandler(string responseContent, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(responseContent)
            });
        return handlerMock;
    }

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_Should_ThrowArgumentNullException_When_PluginIsNull()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            OpenApiTool.CreateAsync(
                null!,
                _httpClientFactoryMock.Object,
                _activityPublisherMock.Object,
                _loggerMock.Object));
    }

    [Fact]
    public async Task CreateAsync_Should_ThrowArgumentNullException_When_HttpClientFactoryIsNull()
    {
        // Arrange
        var plugin = CreateTestPlugin();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            OpenApiTool.CreateAsync(
                plugin,
                null!,
                _activityPublisherMock.Object,
                _loggerMock.Object));
    }

    [Fact]
    public async Task CreateAsync_Should_ParseCachedSpec()
    {
        // Arrange
        var plugin = CreateTestPlugin();
        var handlerMock = CreateMockHandler(PetStoreSpec);
        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri(plugin.BaseUrl!) };
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        // Act
        var tool = await OpenApiTool.CreateAsync(
            plugin,
            _httpClientFactoryMock.Object,
            _activityPublisherMock.Object,
            _loggerMock.Object);

        // Assert
        tool.Should().NotBeNull();
        tool.SystemName.Should().Be("pet_store");
        tool.DisplayName.Should().Be("Pet Store API");
    }

    [Fact]
    public async Task CreateAsync_Should_FetchSpec_When_CacheIsExpired()
    {
        // Arrange
        var plugin = CreateTestPlugin();
        plugin.SpecCachedAt = DateTime.UtcNow.AddHours(-25); // Expired (>24h)

        var handlerMock = CreateMockHandler(PetStoreSpec);
        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri(plugin.BaseUrl!) };
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        // Act
        var tool = await OpenApiTool.CreateAsync(
            plugin,
            _httpClientFactoryMock.Object,
            _activityPublisherMock.Object,
            _loggerMock.Object);

        // Assert
        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.PathAndQuery.Contains("openapi.json")),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_Should_FetchSpec_When_CacheIsEmpty()
    {
        // Arrange
        var plugin = CreateTestPlugin();
        plugin.OpenApiSpecCache = null;
        plugin.SpecCachedAt = null;

        var handlerMock = CreateMockHandler(PetStoreSpec);
        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri(plugin.BaseUrl!) };
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        // Act
        var tool = await OpenApiTool.CreateAsync(
            plugin,
            _httpClientFactoryMock.Object,
            _activityPublisherMock.Object,
            _loggerMock.Object);

        // Assert
        tool.Should().NotBeNull();
        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    #endregion

    #region ITool Properties Tests

    [Fact]
    public async Task SystemName_Should_ReturnPluginSystemName()
    {
        // Arrange
        var plugin = CreateTestPlugin();
        var handlerMock = CreateMockHandler(PetStoreSpec);
        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri(plugin.BaseUrl!) };
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var tool = await OpenApiTool.CreateAsync(
            plugin,
            _httpClientFactoryMock.Object,
            _activityPublisherMock.Object,
            _loggerMock.Object);

        // Assert
        tool.SystemName.Should().Be("pet_store");
    }

    [Fact]
    public async Task DisplayName_Should_ReturnPluginName()
    {
        // Arrange
        var plugin = CreateTestPlugin();
        var handlerMock = CreateMockHandler(PetStoreSpec);
        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri(plugin.BaseUrl!) };
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var tool = await OpenApiTool.CreateAsync(
            plugin,
            _httpClientFactoryMock.Object,
            _activityPublisherMock.Object,
            _loggerMock.Object);

        // Assert
        tool.DisplayName.Should().Be("Pet Store API");
    }

    [Fact]
    public async Task CreditCost_Should_ReturnPluginPriceCredits()
    {
        // Arrange
        var plugin = CreateTestPlugin();
        plugin.PriceCredits = 10;
        var handlerMock = CreateMockHandler(PetStoreSpec);
        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri(plugin.BaseUrl!) };
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var tool = await OpenApiTool.CreateAsync(
            plugin,
            _httpClientFactoryMock.Object,
            _activityPublisherMock.Object,
            _loggerMock.Object);

        // Assert
        tool.CreditCost.Should().Be(10);
    }

    [Fact]
    public async Task RequiresSubscription_Should_ReturnPluginIsSubscriptionBased()
    {
        // Arrange
        var plugin = CreateTestPlugin();
        plugin.IsSubscriptionBased = true;
        var handlerMock = CreateMockHandler(PetStoreSpec);
        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri(plugin.BaseUrl!) };
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var tool = await OpenApiTool.CreateAsync(
            plugin,
            _httpClientFactoryMock.Object,
            _activityPublisherMock.Object,
            _loggerMock.Object);

        // Assert
        tool.RequiresSubscription.Should().BeTrue();
    }

    #endregion

    #region GetAIFunctions Tests

    [Fact]
    public async Task GetAIFunctions_Should_ReturnFunctionsForEachOperation()
    {
        // Arrange
        var plugin = CreateTestPlugin();
        var handlerMock = CreateMockHandler(PetStoreSpec);
        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri(plugin.BaseUrl!) };
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var tool = await OpenApiTool.CreateAsync(
            plugin,
            _httpClientFactoryMock.Object,
            _activityPublisherMock.Object,
            _loggerMock.Object);

        // Act
        var functions = tool.GetAIFunctions();

        // Assert - PetStore spec has 4 operations: listPets, createPet, getPetById, deletePet
        functions.Should().HaveCount(4);
    }

    [Fact]
    public async Task GetAIFunctions_Should_UseOperationIdAsName()
    {
        // Arrange
        var plugin = CreateTestPlugin();
        var handlerMock = CreateMockHandler(PetStoreSpec);
        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri(plugin.BaseUrl!) };
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var tool = await OpenApiTool.CreateAsync(
            plugin,
            _httpClientFactoryMock.Object,
            _activityPublisherMock.Object,
            _loggerMock.Object);

        // Act
        var functions = tool.GetAIFunctions();

        // Assert
        functions.Should().Contain(f => f.Name == "listPets");
        functions.Should().Contain(f => f.Name == "createPet");
        functions.Should().Contain(f => f.Name == "getPetById");
        functions.Should().Contain(f => f.Name == "deletePet");
    }

    [Fact]
    public async Task GetAIFunctions_Should_GenerateNameFromPath_When_OperationIdMissing()
    {
        // Arrange
        var specWithoutOperationId = @"{
            ""openapi"": ""3.0.0"",
            ""info"": { ""title"": ""Test API"", ""version"": ""1.0.0"" },
            ""paths"": {
                ""/users/{userId}"": {
                    ""get"": {
                        ""summary"": ""Get user by ID"",
                        ""responses"": { ""200"": { ""description"": ""Success"" } }
                    }
                }
            }
        }";
        var plugin = CreateTestPlugin(specWithoutOperationId);
        var handlerMock = CreateMockHandler(specWithoutOperationId);
        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri(plugin.BaseUrl!) };
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var tool = await OpenApiTool.CreateAsync(
            plugin,
            _httpClientFactoryMock.Object,
            _activityPublisherMock.Object,
            _loggerMock.Object);

        // Act
        var functions = tool.GetAIFunctions();

        // Assert - should generate name like "GetUsersByUserId"
        functions.Should().HaveCount(1);
        functions[0].Name.Should().Contain("Get");
        functions[0].Name.Should().Contain("Users");
    }

    #endregion

    #region HTTP Execution Tests

    [Fact]
    public async Task ExecuteOperation_Should_MakeGetRequest()
    {
        // Arrange
        var plugin = CreateTestPlugin();
        var specHandlerMock = CreateMockHandler(PetStoreSpec);
        var operationHandlerMock = CreateMockHandler(@"[{""id"": 1, ""name"": ""Fluffy""}]");

        var requestCount = 0;
        var combinedHandlerMock = new Mock<HttpMessageHandler>();
        combinedHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken ct) =>
            {
                requestCount++;
                if (req.RequestUri!.PathAndQuery.Contains("openapi"))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(PetStoreSpec)
                    };
                }
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(@"[{""id"": 1, ""name"": ""Fluffy""}]")
                };
            });

        var httpClient = new HttpClient(combinedHandlerMock.Object) { BaseAddress = new Uri(plugin.BaseUrl!) };
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        // Force fetch from URL to test HTTP
        plugin.OpenApiSpecCache = null;
        plugin.SpecCachedAt = null;

        var tool = await OpenApiTool.CreateAsync(
            plugin,
            _httpClientFactoryMock.Object,
            _activityPublisherMock.Object,
            _loggerMock.Object);

        var functions = tool.GetAIFunctions();
        var listPetsFunc = functions.First(f => f.Name == "listPets");

        // Act
        var args = new AIFunctionArguments
        {
            ["parameters"] = JsonSerializer.SerializeToNode(new { limit = 10 })
        };
        var result = await listPetsFunc.InvokeAsync(args);

        // Assert
        result.Should().NotBeNull();
        var resultStr = result?.ToString();
        resultStr.Should().Contain("success");
    }

    [Fact]
    public async Task ExecuteOperation_Should_ReplacePathParameters()
    {
        // Arrange
        var plugin = CreateTestPlugin();
        HttpRequestMessage? capturedRequest = null;

        var combinedHandlerMock = new Mock<HttpMessageHandler>();
        combinedHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken ct) =>
            {
                capturedRequest = req;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(@"{""id"": ""123"", ""name"": ""Buddy""}")
                };
            });

        var httpClient = new HttpClient(combinedHandlerMock.Object) { BaseAddress = new Uri(plugin.BaseUrl!) };
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var tool = await OpenApiTool.CreateAsync(
            plugin,
            _httpClientFactoryMock.Object,
            _activityPublisherMock.Object,
            _loggerMock.Object);

        var functions = tool.GetAIFunctions();
        var getPetFunc = functions.First(f => f.Name == "getPetById");

        // Act
        var args = new AIFunctionArguments
        {
            ["parameters"] = JsonSerializer.SerializeToNode(new { petId = "123" })
        };
        await getPetFunc.InvokeAsync(args);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.RequestUri!.PathAndQuery.Should().Contain("/pets/123");
    }

    [Fact]
    public async Task ExecuteOperation_Should_HandleErrorResponses()
    {
        // Arrange
        var plugin = CreateTestPlugin();
        var combinedHandlerMock = new Mock<HttpMessageHandler>();
        combinedHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent(@"{""error"": ""Pet not found""}")
            });

        var httpClient = new HttpClient(combinedHandlerMock.Object) { BaseAddress = new Uri(plugin.BaseUrl!) };
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var tool = await OpenApiTool.CreateAsync(
            plugin,
            _httpClientFactoryMock.Object,
            _activityPublisherMock.Object,
            _loggerMock.Object);

        var functions = tool.GetAIFunctions();
        var getPetFunc = functions.First(f => f.Name == "getPetById");

        // Act
        var args = new AIFunctionArguments
        {
            ["parameters"] = JsonSerializer.SerializeToNode(new { petId = "999" })
        };
        var result = await getPetFunc.InvokeAsync(args);

        // Assert
        var resultStr = result?.ToString();
        resultStr.Should().Contain("404");
        resultStr.Should().Contain("false"); // success: false
    }

    [Fact]
    public async Task ExecuteOperation_Should_PublishActivityEvent()
    {
        // Arrange
        var plugin = CreateTestPlugin();
        var combinedHandlerMock = new Mock<HttpMessageHandler>();
        combinedHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(@"[]")
            });

        var httpClient = new HttpClient(combinedHandlerMock.Object) { BaseAddress = new Uri(plugin.BaseUrl!) };
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var tool = await OpenApiTool.CreateAsync(
            plugin,
            _httpClientFactoryMock.Object,
            _activityPublisherMock.Object,
            _loggerMock.Object);

        var functions = tool.GetAIFunctions();
        var listPetsFunc = functions.First(f => f.Name == "listPets");

        // Act
        var args = new AIFunctionArguments
        {
            ["parameters"] = JsonSerializer.SerializeToNode(new { })
        };
        await listPetsFunc.InvokeAsync(args);

        // Assert
        _activityPublisherMock.Verify(
            a => a.PublishAsync(
                "tool_invocation",
                It.Is<object>(o => o.ToString()!.Contains("pet_store")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ExecuteOperation_Should_HandleHttpRequestException()
    {
        // Arrange
        var plugin = CreateTestPlugin();
        var combinedHandlerMock = new Mock<HttpMessageHandler>();
        combinedHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection failed"));

        var httpClient = new HttpClient(combinedHandlerMock.Object) { BaseAddress = new Uri(plugin.BaseUrl!) };
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var tool = await OpenApiTool.CreateAsync(
            plugin,
            _httpClientFactoryMock.Object,
            _activityPublisherMock.Object,
            _loggerMock.Object);

        var functions = tool.GetAIFunctions();
        var listPetsFunc = functions.First(f => f.Name == "listPets");

        // Act
        var args = new AIFunctionArguments
        {
            ["parameters"] = JsonSerializer.SerializeToNode(new { })
        };
        var result = await listPetsFunc.InvokeAsync(args);

        // Assert
        var resultStr = result?.ToString();
        resultStr.Should().Contain("false"); // success: false
        resultStr.Should().Contain("error");
    }

    [Fact]
    public async Task CreateAsync_Should_ThrowOnInvalidSpec()
    {
        // Arrange
        var invalidSpec = "{ invalid json }";
        var plugin = CreateTestPlugin(invalidSpec);
        var handlerMock = CreateMockHandler(invalidSpec);
        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri(plugin.BaseUrl!) };
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        // Act & Assert - OpenAPI library throws various exceptions for invalid specs
        await Assert.ThrowsAnyAsync<Exception>(() =>
            OpenApiTool.CreateAsync(
                plugin,
                _httpClientFactoryMock.Object,
                _activityPublisherMock.Object,
                _loggerMock.Object));
    }

    #endregion
}
