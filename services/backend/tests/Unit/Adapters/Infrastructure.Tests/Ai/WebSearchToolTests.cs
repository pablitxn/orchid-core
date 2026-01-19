using System.Text.Json;
using Application.Interfaces;
using FluentAssertions;
using Infrastructure.Ai.AgentFramework.Tools;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using AppChatMessage = Application.Interfaces.ChatMessage;

namespace Infrastructure.Tests.Ai;

public class WebSearchToolTests
{
    private readonly Mock<ILogger<WebSearchTool>> _loggerMock;
    private readonly Mock<ISearchProvider> _searchMock;
    private readonly Mock<IHttpClientFactory> _httpFactoryMock;
    private readonly Mock<IChatCompletionPort> _chatMock;
    private readonly Mock<IEmbeddingGeneratorPort> _embedderMock;
    private readonly Mock<IVectorStorePort> _vectorStoreMock;
    private readonly Mock<IActivityPublisher> _activityMock;
    private readonly WebSearchTool _tool;

    public WebSearchToolTests()
    {
        _loggerMock = new Mock<ILogger<WebSearchTool>>();
        _searchMock = new Mock<ISearchProvider>();
        _httpFactoryMock = new Mock<IHttpClientFactory>();
        _chatMock = new Mock<IChatCompletionPort>();
        _embedderMock = new Mock<IEmbeddingGeneratorPort>();
        _vectorStoreMock = new Mock<IVectorStorePort>();
        _activityMock = new Mock<IActivityPublisher>();

        _tool = new WebSearchTool(
            _loggerMock.Object,
            _searchMock.Object,
            _httpFactoryMock.Object,
            _chatMock.Object,
            _embedderMock.Object,
            _vectorStoreMock.Object,
            _activityMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_LoggerIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new WebSearchTool(null!, _searchMock.Object, _httpFactoryMock.Object,
                _chatMock.Object, _embedderMock.Object, _vectorStoreMock.Object, _activityMock.Object));
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_SearchProviderIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new WebSearchTool(_loggerMock.Object, null!, _httpFactoryMock.Object,
                _chatMock.Object, _embedderMock.Object, _vectorStoreMock.Object, _activityMock.Object));
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_HttpClientFactoryIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new WebSearchTool(_loggerMock.Object, _searchMock.Object, null!,
                _chatMock.Object, _embedderMock.Object, _vectorStoreMock.Object, _activityMock.Object));
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_ChatIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new WebSearchTool(_loggerMock.Object, _searchMock.Object, _httpFactoryMock.Object,
                null!, _embedderMock.Object, _vectorStoreMock.Object, _activityMock.Object));
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_EmbedderIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new WebSearchTool(_loggerMock.Object, _searchMock.Object, _httpFactoryMock.Object,
                _chatMock.Object, null!, _vectorStoreMock.Object, _activityMock.Object));
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_VectorStoreIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new WebSearchTool(_loggerMock.Object, _searchMock.Object, _httpFactoryMock.Object,
                _chatMock.Object, _embedderMock.Object, null!, _activityMock.Object));
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_ActivityPublisherIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new WebSearchTool(_loggerMock.Object, _searchMock.Object, _httpFactoryMock.Object,
                _chatMock.Object, _embedderMock.Object, _vectorStoreMock.Object, null!));
    }

    #endregion

    #region ITool Implementation Tests

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
    public void Description_Should_NotBeEmpty()
    {
        _tool.Description.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void CreditCost_Should_Return_ExpectedValue()
    {
        _tool.CreditCost.Should().Be(2);
    }

    [Fact]
    public void RequiresSubscription_Should_ReturnFalse()
    {
        _tool.RequiresSubscription.Should().BeFalse();
    }

    [Fact]
    public void GetAIFunctions_Should_Return_FiveFunctions()
    {
        var functions = _tool.GetAIFunctions();

        functions.Should().HaveCount(5);
        functions.Select(f => f.Name).Should().Contain("SearchWeb");
        functions.Select(f => f.Name).Should().Contain("FetchContent");
        functions.Select(f => f.Name).Should().Contain("CleanContent");
        functions.Select(f => f.Name).Should().Contain("SummarizeContent");
        functions.Select(f => f.Name).Should().Contain("UpsertToVector");
    }

    #endregion

    #region SearchWebAsync Tests

    [Fact]
    public async Task SearchWebAsync_Should_CallSearchProvider()
    {
        // Arrange
        var query = "test query";
        var topK = 5;
        IReadOnlyList<WebSearchResult> searchResults = new List<WebSearchResult>
        {
            new("Title 1", "https://example.com/1", "Snippet 1"),
            new("Title 2", "https://example.com/2", "Snippet 2")
        };

        _searchMock
            .Setup(s => s.SearchAsync(query, topK, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        // Act
        var result = await _tool.SearchWebAsync(query, topK, null, CancellationToken.None);

        // Assert
        _searchMock.Verify(
            s => s.SearchAsync(query, topK, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SearchWebAsync_Should_ReturnJsonArray()
    {
        // Arrange
        var query = "test query";
        IReadOnlyList<WebSearchResult> searchResults = new List<WebSearchResult>
        {
            new("Title 1", "https://example.com/1", "Snippet 1")
        };

        _searchMock
            .Setup(s => s.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        // Act
        var result = await _tool.SearchWebAsync(query, 8, null, CancellationToken.None);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var parsed = JsonSerializer.Deserialize<JsonElement>(result);
        parsed.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task SearchWebAsync_Should_PublishActivity()
    {
        // Arrange
        var query = "test query";
        IReadOnlyList<WebSearchResult> searchResults = new List<WebSearchResult>();
        _searchMock
            .Setup(s => s.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        // Act
        await _tool.SearchWebAsync(query, 8, null, CancellationToken.None);

        // Assert
        _activityMock.Verify(
            a => a.PublishAsync(
                "tool_invocation",
                It.Is<object>(o => o.ToString()!.Contains("search_web")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SearchWebAsync_Should_PassSiteFilter()
    {
        // Arrange
        var query = "test query";
        var siteFilter = "example.com";
        IReadOnlyList<WebSearchResult> searchResults = new List<WebSearchResult>();
        _searchMock
            .Setup(s => s.SearchAsync(query, 8, siteFilter, It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        // Act
        await _tool.SearchWebAsync(query, 8, siteFilter, CancellationToken.None);

        // Assert
        _searchMock.Verify(
            s => s.SearchAsync(query, 8, siteFilter, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region FetchContentAsync Tests - SSRF Validation

    [Fact]
    public async Task FetchContentAsync_Should_ReturnEmpty_ForInvalidUrl()
    {
        // Arrange
        var invalidUrl = "not-a-valid-url";

        // Act
        var result = await _tool.FetchContentAsync(invalidUrl, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchContentAsync_Should_ReturnEmpty_ForNonHttpUrl()
    {
        // Arrange - SSRF protection: block non-HTTP schemes
        var ftpUrl = "ftp://example.com/file.txt";

        // Act
        var result = await _tool.FetchContentAsync(ftpUrl, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchContentAsync_Should_ReturnEmpty_ForFileUrl()
    {
        // Arrange - SSRF protection: block file:// scheme
        var fileUrl = "file:///etc/passwd";

        // Act
        var result = await _tool.FetchContentAsync(fileUrl, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchContentAsync_Should_ReturnEmpty_ForLocalhost()
    {
        // Arrange - SSRF protection: block IP-based URLs
        var localhostIp = "http://127.0.0.1/admin";

        // Act
        var result = await _tool.FetchContentAsync(localhostIp, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchContentAsync_Should_ReturnEmpty_ForPrivateIp()
    {
        // Arrange - SSRF protection: block private IP ranges
        var privateIp = "http://192.168.1.1/admin";

        // Act
        var result = await _tool.FetchContentAsync(privateIp, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchContentAsync_Should_ReturnEmpty_ForDataUrl()
    {
        // Arrange - SSRF protection: block data: URLs
        var dataUrl = "data:text/html,<script>alert(1)</script>";

        // Act
        var result = await _tool.FetchContentAsync(dataUrl, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchContentAsync_Should_ReturnEmpty_ForJavascriptUrl()
    {
        // Arrange - SSRF protection: block javascript: URLs
        var jsUrl = "javascript:alert(1)";

        // Act
        var result = await _tool.FetchContentAsync(jsUrl, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region CleanContent Tests

    [Fact]
    public void CleanContent_Should_RemoveHtmlTags()
    {
        // Arrange
        var html = "<html><body><p>Hello World</p></body></html>";

        // Act
        var result = _tool.CleanContent(html);

        // Assert
        result.Should().Contain("Hello World");
        result.Should().NotContain("<");
        result.Should().NotContain(">");
    }

    [Fact]
    public void CleanContent_Should_DecodeHtmlEntities()
    {
        // Arrange
        var html = "<p>Test &amp; more &lt;content&gt;</p>";

        // Act
        var result = _tool.CleanContent(html);

        // Assert
        result.Should().Contain("Test & more <content>");
    }

    [Fact]
    public void CleanContent_Should_NormalizeWhitespace()
    {
        // Arrange
        var html = "<p>Multiple    spaces    and\n\nnewlines</p>";

        // Act
        var result = _tool.CleanContent(html);

        // Assert
        result.Should().NotContain("  "); // No double spaces
        result.Should().NotContain("\n"); // No newlines
    }

    [Fact]
    public void CleanContent_Should_TrimResult()
    {
        // Arrange
        var html = "  <p>Content with spaces</p>  ";

        // Act
        var result = _tool.CleanContent(html);

        // Assert
        result.Should().NotStartWith(" ");
        result.Should().NotEndWith(" ");
    }

    #endregion

    #region SummarizeContentAsync Tests

    [Fact]
    public async Task SummarizeContentAsync_Should_CallChatCompletion()
    {
        // Arrange
        var content = "Long content that needs to be summarized.";
        var expectedSummary = "This is a summary.";

        _chatMock
            .Setup(c => c.CompleteAsync(It.IsAny<IEnumerable<AppChatMessage>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedSummary);

        // Act
        var result = await _tool.SummarizeContentAsync(content, CancellationToken.None);

        // Assert
        result.Should().Be(expectedSummary);
        _chatMock.Verify(
            c => c.CompleteAsync(
                It.Is<IEnumerable<AppChatMessage>>(msgs => msgs.Any(m => m.Role == "user" && m.Content == content)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SummarizeContentAsync_Should_PublishActivity()
    {
        // Arrange
        var content = "Content to summarize.";
        _chatMock
            .Setup(c => c.CompleteAsync(It.IsAny<IEnumerable<AppChatMessage>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Summary");

        // Act
        await _tool.SummarizeContentAsync(content, CancellationToken.None);

        // Assert
        _activityMock.Verify(
            a => a.PublishAsync(
                "tool_invocation",
                It.Is<object>(o => o.ToString()!.Contains("summarize_content")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region UpsertToVectorAsync Tests

    [Fact]
    public async Task UpsertToVectorAsync_Should_ChunkContent()
    {
        // Arrange
        var url = "https://example.com/page";
        var text = new string('a', 2500); // More than one chunk
        var embedding = new float[] { 0.1f, 0.2f };

        _embedderMock
            .Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);

        // Act
        var result = await _tool.UpsertToVectorAsync(url, text, CancellationToken.None);

        // Assert
        // With 2500 chars and 1000 chunk size, should have 3 chunks
        _vectorStoreMock.Verify(
            v => v.UpsertChunkAsync(It.IsAny<VectorChunk>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task UpsertToVectorAsync_Should_ReturnJsonWithChunkIds()
    {
        // Arrange
        var url = "https://example.com/page";
        var text = "Short text content";
        var embedding = new float[] { 0.1f, 0.2f };

        _embedderMock
            .Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);

        // Act
        var result = await _tool.UpsertToVectorAsync(url, text, CancellationToken.None);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var parsed = JsonSerializer.Deserialize<JsonElement>(result);
        parsed.TryGetProperty("chunkIds", out _).Should().BeTrue();
        parsed.TryGetProperty("url", out _).Should().BeTrue();
    }

    [Fact]
    public async Task UpsertToVectorAsync_Should_SkipEmpty_Text()
    {
        // Arrange
        var url = "https://example.com/page";
        var text = "";

        // Act
        var result = await _tool.UpsertToVectorAsync(url, text, CancellationToken.None);

        // Assert
        _embedderMock.Verify(
            e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _vectorStoreMock.Verify(
            v => v.UpsertChunkAsync(It.IsAny<VectorChunk>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpsertToVectorAsync_Should_PublishActivity()
    {
        // Arrange
        var url = "https://example.com/page";
        var text = "Content";
        var embedding = new float[] { 0.1f, 0.2f };

        _embedderMock
            .Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);

        // Act
        await _tool.UpsertToVectorAsync(url, text, CancellationToken.None);

        // Assert
        _activityMock.Verify(
            a => a.PublishAsync(
                "tool_invocation",
                It.Is<object>(o => o.ToString()!.Contains("upsert_to_vector")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion
}
