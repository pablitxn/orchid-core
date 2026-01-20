using System.Net;
using System.Text.Json;
using Application.Interfaces;
using FluentAssertions;
using Infrastructure.Ai.AgentFramework.Tools;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Infrastructure.Tests.Ai;

public class WebSearchToolTests
{
    private readonly Mock<ILogger<WebSearchTool>> _loggerMock;
    private readonly Mock<ISearchProvider> _searchProviderMock;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<IChatCompletionPort> _chatMock;
    private readonly Mock<IEmbeddingGeneratorPort> _embedderMock;
    private readonly Mock<IVectorStorePort> _vectorStoreMock;
    private readonly Mock<IActivityPublisher> _activityPublisherMock;
    private readonly WebSearchTool _tool;

    public WebSearchToolTests()
    {
        _loggerMock = new Mock<ILogger<WebSearchTool>>();
        _searchProviderMock = new Mock<ISearchProvider>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _chatMock = new Mock<IChatCompletionPort>();
        _embedderMock = new Mock<IEmbeddingGeneratorPort>();
        _vectorStoreMock = new Mock<IVectorStorePort>();
        _activityPublisherMock = new Mock<IActivityPublisher>();

        _tool = new WebSearchTool(
            _loggerMock.Object,
            _searchProviderMock.Object,
            _httpClientFactoryMock.Object,
            _chatMock.Object,
            _embedderMock.Object,
            _vectorStoreMock.Object,
            _activityPublisherMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_LoggerIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new WebSearchTool(null!, _searchProviderMock.Object, _httpClientFactoryMock.Object,
                _chatMock.Object, _embedderMock.Object, _vectorStoreMock.Object, _activityPublisherMock.Object));
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_SearchProviderIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new WebSearchTool(_loggerMock.Object, null!, _httpClientFactoryMock.Object,
                _chatMock.Object, _embedderMock.Object, _vectorStoreMock.Object, _activityPublisherMock.Object));
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_HttpClientFactoryIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new WebSearchTool(_loggerMock.Object, _searchProviderMock.Object, null!,
                _chatMock.Object, _embedderMock.Object, _vectorStoreMock.Object, _activityPublisherMock.Object));
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_ChatIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new WebSearchTool(_loggerMock.Object, _searchProviderMock.Object, _httpClientFactoryMock.Object,
                null!, _embedderMock.Object, _vectorStoreMock.Object, _activityPublisherMock.Object));
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_EmbedderIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new WebSearchTool(_loggerMock.Object, _searchProviderMock.Object, _httpClientFactoryMock.Object,
                _chatMock.Object, null!, _vectorStoreMock.Object, _activityPublisherMock.Object));
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_VectorStoreIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new WebSearchTool(_loggerMock.Object, _searchProviderMock.Object, _httpClientFactoryMock.Object,
                _chatMock.Object, _embedderMock.Object, null!, _activityPublisherMock.Object));
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_ActivityPublisherIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new WebSearchTool(_loggerMock.Object, _searchProviderMock.Object, _httpClientFactoryMock.Object,
                _chatMock.Object, _embedderMock.Object, _vectorStoreMock.Object, null!));
    }

    #endregion

    #region ITool Properties Tests

    [Fact]
    public void SystemName_Should_Return_WebSearch()
    {
        _tool.SystemName.Should().Be("web_search");
    }

    [Fact]
    public void DisplayName_Should_Return_WebSearch()
    {
        _tool.DisplayName.Should().Be("Web Search");
    }

    [Fact]
    public void Description_Should_Return_ExpectedDescription()
    {
        _tool.Description.Should().Be("Provides web search, content fetching, and text processing capabilities.");
    }

    [Fact]
    public void CreditCost_Should_Return_Two()
    {
        _tool.CreditCost.Should().Be(2);
    }

    [Fact]
    public void RequiresSubscription_Should_Return_False()
    {
        _tool.RequiresSubscription.Should().BeFalse();
    }

    #endregion

    #region SearchWebAsync Tests

    [Fact]
    public async Task SearchWebAsync_Should_CallSearchProvider()
    {
        // Arrange
        var query = "test query";
        var topK = 5;
        var siteFilter = "example.com";
        var results = new List<WebSearchResult>
        {
            new("Title 1", "https://example.com/1", "Snippet 1"),
            new("Title 2", "https://example.com/2", "Snippet 2")
        };

        _searchProviderMock
            .Setup(s => s.SearchAsync(query, topK, siteFilter, It.IsAny<CancellationToken>()))
            .ReturnsAsync(results);

        // Act
        await _tool.SearchWebAsync(query, topK, siteFilter);

        // Assert
        _searchProviderMock.Verify(
            s => s.SearchAsync(query, topK, siteFilter, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SearchWebAsync_Should_UseDefaultTopK_When_NotSpecified()
    {
        // Arrange
        var query = "test query";
        _searchProviderMock
            .Setup(s => s.SearchAsync(query, 8, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WebSearchResult>());

        // Act
        await _tool.SearchWebAsync(query);

        // Assert
        _searchProviderMock.Verify(
            s => s.SearchAsync(query, 8, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SearchWebAsync_Should_ReturnSerializedResults()
    {
        // Arrange
        var query = "test query";
        var results = new List<WebSearchResult>
        {
            new("Test Title", "https://example.com", "Test Snippet")
        };

        _searchProviderMock
            .Setup(s => s.SearchAsync(query, It.IsAny<int>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(results);

        // Act
        var json = await _tool.SearchWebAsync(query);

        // Assert
        json.Should().NotBeNullOrEmpty();
        var parsed = JsonSerializer.Deserialize<JsonElement>(json);
        parsed.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task SearchWebAsync_Should_PublishActivityEvent()
    {
        // Arrange
        var query = "test query";
        _searchProviderMock
            .Setup(s => s.SearchAsync(query, It.IsAny<int>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WebSearchResult>());

        // Act
        await _tool.SearchWebAsync(query);

        // Assert
        _activityPublisherMock.Verify(
            a => a.PublishAsync(
                "tool_invocation",
                It.Is<object>(o => o.ToString()!.Contains("search_web")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region FetchContentAsync Tests - SSRF Validation

    [Fact]
    public async Task FetchContentAsync_Should_ReturnEmpty_When_UrlIsInvalid()
    {
        // Arrange
        var invalidUrl = "not-a-valid-url";

        // Act
        var result = await _tool.FetchContentAsync(invalidUrl);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchContentAsync_Should_ReturnEmpty_When_SchemeIsNotHttp()
    {
        // Arrange - file:// scheme should be blocked (SSRF prevention)
        var fileUrl = "file:///etc/passwd";

        // Act
        var result = await _tool.FetchContentAsync(fileUrl);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchContentAsync_Should_ReturnEmpty_When_SchemeIsFtp()
    {
        // Arrange - ftp:// scheme should be blocked (SSRF prevention)
        var ftpUrl = "ftp://example.com/file.txt";

        // Act
        var result = await _tool.FetchContentAsync(ftpUrl);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchContentAsync_Should_ReturnEmpty_When_UrlIsIpAddress()
    {
        // Arrange - IP addresses should be blocked (SSRF prevention for internal networks)
        var ipUrl = "http://192.168.1.1/";

        // Act
        var result = await _tool.FetchContentAsync(ipUrl);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchContentAsync_Should_ReturnEmpty_When_UrlIsLocalhost()
    {
        // Arrange - localhost IP should be blocked
        var localhostUrl = "http://127.0.0.1/admin";

        // Act
        var result = await _tool.FetchContentAsync(localhostUrl);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchContentAsync_Should_FetchContent_When_UrlIsValidHttps()
    {
        // Arrange
        var validUrl = "https://example.com/page";
        var expectedContent = "<html><body>Hello World</body></html>";

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(expectedContent)
            });

        var httpClient = new HttpClient(handlerMock.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        // Act
        var result = await _tool.FetchContentAsync(validUrl);

        // Assert
        result.Should().Be(expectedContent);
    }

    [Fact]
    public async Task FetchContentAsync_Should_FetchContent_When_UrlIsValidHttp()
    {
        // Arrange
        var validUrl = "http://example.com/page";
        var expectedContent = "Test content";

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(expectedContent)
            });

        var httpClient = new HttpClient(handlerMock.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        // Act
        var result = await _tool.FetchContentAsync(validUrl);

        // Assert
        result.Should().Be(expectedContent);
    }

    [Fact]
    public async Task FetchContentAsync_Should_ReturnEmpty_When_HttpRequestFails()
    {
        // Arrange
        var validUrl = "https://example.com/error";

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection failed"));

        var httpClient = new HttpClient(handlerMock.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        // Act
        var result = await _tool.FetchContentAsync(validUrl);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchContentAsync_Should_PublishActivityEvent_OnSuccess()
    {
        // Arrange
        var validUrl = "https://example.com/page";
        var content = "Test content";

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(content)
            });

        var httpClient = new HttpClient(handlerMock.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        // Act
        await _tool.FetchContentAsync(validUrl);

        // Assert
        _activityPublisherMock.Verify(
            a => a.PublishAsync(
                "tool_invocation",
                It.Is<object>(o => o.ToString()!.Contains("fetch_content")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region CleanContent Tests

    [Fact]
    public void CleanContent_Should_RemoveHtmlTags()
    {
        // Arrange
        var html = "<html><body><h1>Title</h1><p>Content</p></body></html>";

        // Act
        var result = _tool.CleanContent(html);

        // Assert
        result.Should().NotContain("<");
        result.Should().NotContain(">");
        result.Should().Contain("Title");
        result.Should().Contain("Content");
    }

    [Fact]
    public void CleanContent_Should_DecodeHtmlEntities()
    {
        // Arrange
        var html = "Hello &amp; World &lt;test&gt;";

        // Act
        var result = _tool.CleanContent(html);

        // Assert
        result.Should().Contain("Hello & World <test>");
    }

    [Fact]
    public void CleanContent_Should_NormalizeWhitespace()
    {
        // Arrange
        var html = "Hello     \n\n\t    World";

        // Act
        var result = _tool.CleanContent(html);

        // Assert
        result.Should().Be("Hello World");
    }

    [Fact]
    public void CleanContent_Should_HandleEmptyString()
    {
        // Arrange
        var html = "";

        // Act
        var result = _tool.CleanContent(html);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void CleanContent_Should_HandleComplexHtml()
    {
        // Arrange
        var html = @"
            <html>
                <head><title>Page Title</title></head>
                <body>
                    <div class=""container"">
                        <p>First paragraph</p>
                        <p>Second paragraph</p>
                    </div>
                </body>
            </html>";

        // Act
        var result = _tool.CleanContent(html);

        // Assert
        result.Should().Contain("Page Title");
        result.Should().Contain("First paragraph");
        result.Should().Contain("Second paragraph");
        result.Should().NotContain("div");
        result.Should().NotContain("class");
    }

    #endregion

    #region SummarizeContentAsync Tests

    [Fact]
    public async Task SummarizeContentAsync_Should_CallChatCompletion()
    {
        // Arrange
        var content = "Long text content to summarize...";
        var expectedSummary = "This is a summary.";

        _chatMock
            .Setup(c => c.CompleteAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedSummary);

        // Act
        var result = await _tool.SummarizeContentAsync(content);

        // Assert
        result.Should().Be(expectedSummary);
        _chatMock.Verify(
            c => c.CompleteAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SummarizeContentAsync_Should_PassContentAsUserMessage()
    {
        // Arrange
        var content = "Content to summarize";
        IEnumerable<ChatMessage>? capturedMessages = null;

        _chatMock
            .Setup(c => c.CompleteAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<ChatMessage>, CancellationToken>((msgs, _) => capturedMessages = msgs)
            .ReturnsAsync("Summary");

        // Act
        await _tool.SummarizeContentAsync(content);

        // Assert
        capturedMessages.Should().NotBeNull();
        var messages = capturedMessages!.ToList();
        messages.Should().HaveCount(2);
        messages[0].Role.Should().Be("system");
        messages[1].Role.Should().Be("user");
        messages[1].Content.Should().Be(content);
    }

    [Fact]
    public async Task SummarizeContentAsync_Should_PublishActivityEvent()
    {
        // Arrange
        var content = "Content to summarize";

        _chatMock
            .Setup(c => c.CompleteAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Summary");

        // Act
        await _tool.SummarizeContentAsync(content);

        // Assert
        _activityPublisherMock.Verify(
            a => a.PublishAsync(
                "tool_invocation",
                It.Is<object>(o => o.ToString()!.Contains("summarize_content")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region UpsertToVectorAsync Tests

    [Fact]
    public async Task UpsertToVectorAsync_Should_ChunkAndEmbedContent()
    {
        // Arrange
        var url = "https://example.com/page";
        var text = new string('a', 2500); // 2500 chars = 3 chunks at 1000 chars each

        _embedderMock
            .Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 0.1f, 0.2f, 0.3f });

        // Act
        var result = await _tool.UpsertToVectorAsync(url, text);

        // Assert
        _embedderMock.Verify(
            e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3)); // 3 chunks
    }

    [Fact]
    public async Task UpsertToVectorAsync_Should_UpsertAllChunksToVectorStore()
    {
        // Arrange
        var url = "https://example.com/page";
        var text = new string('a', 2000); // 2 chunks

        _embedderMock
            .Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 0.1f });

        // Act
        await _tool.UpsertToVectorAsync(url, text);

        // Assert
        _vectorStoreMock.Verify(
            v => v.UpsertChunkAsync(It.IsAny<VectorChunk>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task UpsertToVectorAsync_Should_GenerateConsistentFileId()
    {
        // Arrange
        var url = "https://example.com/page";
        var text = "Test content";
        VectorChunk? capturedChunk = null;

        _embedderMock
            .Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 0.1f });

        _vectorStoreMock
            .Setup(v => v.UpsertChunkAsync(It.IsAny<VectorChunk>(), It.IsAny<CancellationToken>()))
            .Callback<VectorChunk, CancellationToken>((c, _) => capturedChunk = c)
            .Returns(Task.CompletedTask);

        // Act
        await _tool.UpsertToVectorAsync(url, text);
        var firstFileId = capturedChunk?.FileId;

        await _tool.UpsertToVectorAsync(url, text);
        var secondFileId = capturedChunk?.FileId;

        // Assert - same URL should produce same fileId (MD5 hash)
        firstFileId.Should().Be(secondFileId);
    }

    [Fact]
    public async Task UpsertToVectorAsync_Should_ReturnChunkIds()
    {
        // Arrange
        var url = "https://example.com/page";
        var text = "Test content";

        _embedderMock
            .Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 0.1f });

        // Act
        var result = await _tool.UpsertToVectorAsync(url, text);

        // Assert
        var parsed = JsonSerializer.Deserialize<JsonElement>(result);
        parsed.GetProperty("url").GetString().Should().Be(url);
        parsed.GetProperty("chunkIds").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task UpsertToVectorAsync_Should_HandleEmptyText()
    {
        // Arrange
        var url = "https://example.com/page";
        var text = "";

        // Act
        var result = await _tool.UpsertToVectorAsync(url, text);

        // Assert
        _embedderMock.Verify(
            e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        var parsed = JsonSerializer.Deserialize<JsonElement>(result);
        parsed.GetProperty("chunkIds").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task UpsertToVectorAsync_Should_PublishActivityEvent()
    {
        // Arrange
        var url = "https://example.com/page";
        var text = "Test content";

        _embedderMock
            .Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 0.1f });

        // Act
        await _tool.UpsertToVectorAsync(url, text);

        // Assert
        _activityPublisherMock.Verify(
            a => a.PublishAsync(
                "tool_invocation",
                It.Is<object>(o => o.ToString()!.Contains("upsert_to_vector")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region GetAIFunctions Tests

    [Fact]
    public void GetAIFunctions_Should_ReturnFiveFunctions()
    {
        // Act
        var functions = _tool.GetAIFunctions();

        // Assert
        functions.Should().HaveCount(5);
    }

    [Fact]
    public void GetAIFunctions_Should_ContainSearchWebFunction()
    {
        // Act
        var functions = _tool.GetAIFunctions();

        // Assert
        functions.Should().Contain(f => f.Name == "SearchWeb");
    }

    [Fact]
    public void GetAIFunctions_Should_ContainFetchContentFunction()
    {
        // Act
        var functions = _tool.GetAIFunctions();

        // Assert
        functions.Should().Contain(f => f.Name == "FetchContent");
    }

    [Fact]
    public void GetAIFunctions_Should_ContainCleanContentFunction()
    {
        // Act
        var functions = _tool.GetAIFunctions();

        // Assert
        functions.Should().Contain(f => f.Name == "CleanContent");
    }

    [Fact]
    public void GetAIFunctions_Should_ContainSummarizeContentFunction()
    {
        // Act
        var functions = _tool.GetAIFunctions();

        // Assert
        functions.Should().Contain(f => f.Name == "SummarizeContent");
    }

    [Fact]
    public void GetAIFunctions_Should_ContainUpsertToVectorFunction()
    {
        // Act
        var functions = _tool.GetAIFunctions();

        // Assert
        functions.Should().Contain(f => f.Name == "UpsertToVector");
    }

    #endregion

    #region Logging Tests

    [Fact]
    public async Task SearchWebAsync_Should_LogInformation()
    {
        // Arrange
        var query = "test query";
        _searchProviderMock
            .Setup(s => s.SearchAsync(query, It.IsAny<int>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WebSearchResult>());

        // Act
        await _tool.SearchWebAsync(query);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                Microsoft.Extensions.Logging.LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("SearchWeb")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task FetchContentAsync_Should_LogError_When_UrlIsInvalid()
    {
        // Arrange
        var invalidUrl = "invalid-url";

        // Act
        await _tool.FetchContentAsync(invalidUrl);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                Microsoft.Extensions.Logging.LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Invalid URL")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion
}
