using System.Text.Json;
using Application.Interfaces;
using Application.UseCases.VectorStore.AddDocument;
using Application.UseCases.VectorStore.SemanticSearch;
using FluentAssertions;
using Infrastructure.Ai.AgentFramework.Tools;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Infrastructure.Tests.Ai;

public class VectorStoreToolTests
{
    private readonly Mock<ILogger<VectorStoreTool>> _loggerMock;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<IActivityPublisher> _activityPublisherMock;
    private readonly VectorStoreTool _tool;

    public VectorStoreToolTests()
    {
        _loggerMock = new Mock<ILogger<VectorStoreTool>>();
        _mediatorMock = new Mock<IMediator>();
        _activityPublisherMock = new Mock<IActivityPublisher>();

        _tool = new VectorStoreTool(
            _loggerMock.Object,
            _mediatorMock.Object,
            _activityPublisherMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_LoggerIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new VectorStoreTool(null!, _mediatorMock.Object, _activityPublisherMock.Object));
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_MediatorIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new VectorStoreTool(_loggerMock.Object, null!, _activityPublisherMock.Object));
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_ActivityPublisherIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new VectorStoreTool(_loggerMock.Object, _mediatorMock.Object, null!));
    }

    #endregion

    #region ITool Properties Tests

    [Fact]
    public void SystemName_Should_Return_VectorStore()
    {
        // Assert
        _tool.SystemName.Should().Be("vector_store");
    }

    [Fact]
    public void DisplayName_Should_Return_VectorStore()
    {
        // Assert
        _tool.DisplayName.Should().Be("Vector Store");
    }

    [Fact]
    public void Description_Should_Return_ExpectedDescription()
    {
        // Assert
        _tool.Description.Should().Be("Provides semantic search and document storage capabilities.");
    }

    [Fact]
    public void CreditCost_Should_Return_One()
    {
        // Assert
        _tool.CreditCost.Should().Be(1);
    }

    [Fact]
    public void RequiresSubscription_Should_Return_False()
    {
        // Assert
        _tool.RequiresSubscription.Should().BeFalse();
    }

    #endregion

    #region AddDocumentAsync Tests

    [Fact]
    public async Task AddDocumentAsync_Should_SendCommandToMediator()
    {
        // Arrange
        var documentPath = "/path/to/document.pdf";
        var metadata = "test metadata";
        var expectedResult = new AddDocumentResult(documentPath, 5, metadata);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AddDocumentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        await _tool.AddDocumentAsync(documentPath, metadata);

        // Assert
        _mediatorMock.Verify(
            m => m.Send(
                It.Is<AddDocumentCommand>(c => c.DocumentPath == documentPath && c.Metadata == metadata),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AddDocumentAsync_Should_PublishActivityEvent()
    {
        // Arrange
        var documentPath = "/path/to/document.pdf";
        var metadata = "test metadata";
        var expectedResult = new AddDocumentResult(documentPath, 5, metadata);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AddDocumentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        await _tool.AddDocumentAsync(documentPath, metadata);

        // Assert
        _activityPublisherMock.Verify(
            a => a.PublishAsync(
                "tool_invocation",
                It.Is<object>(o => o.ToString()!.Contains("add_document")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AddDocumentAsync_Should_ReturnJsonSerializedResult()
    {
        // Arrange
        var documentPath = "/path/to/document.pdf";
        var expectedResult = new AddDocumentResult(documentPath, 5, null);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AddDocumentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _tool.AddDocumentAsync(documentPath);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var parsed = JsonSerializer.Deserialize<JsonElement>(result);
        parsed.GetProperty("documentPath").GetString().Should().Be(documentPath);
        parsed.GetProperty("chunks").GetInt32().Should().Be(5);
    }

    [Fact]
    public async Task AddDocumentAsync_Should_AcceptNullMetadata()
    {
        // Arrange
        var documentPath = "/path/to/document.pdf";
        var expectedResult = new AddDocumentResult(documentPath, 3, null);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AddDocumentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _tool.AddDocumentAsync(documentPath);

        // Assert
        _mediatorMock.Verify(
            m => m.Send(
                It.Is<AddDocumentCommand>(c => c.Metadata == null),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AddDocumentAsync_Should_RespectCancellationToken()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var documentPath = "/path/to/document.pdf";

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AddDocumentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AddDocumentResult(documentPath, 1, null));

        // Act
        await _tool.AddDocumentAsync(documentPath, ct: cts.Token);

        // Assert
        _mediatorMock.Verify(
            m => m.Send(It.IsAny<AddDocumentCommand>(), cts.Token),
            Times.Once);
    }

    #endregion

    #region SearchAsync Tests

    [Fact]
    public async Task SearchAsync_Should_SendQueryToMediator()
    {
        // Arrange
        var query = "test search query";
        var k = 5;

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<SemanticSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SearchHit>());

        // Act
        await _tool.SearchAsync(query, k);

        // Assert
        _mediatorMock.Verify(
            m => m.Send(
                It.Is<SemanticSearchQuery>(q => q.Query == query && q.K == k),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SearchAsync_Should_UseDefaultK_When_NotSpecified()
    {
        // Arrange
        var query = "test search query";

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<SemanticSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SearchHit>());

        // Act
        await _tool.SearchAsync(query);

        // Assert
        _mediatorMock.Verify(
            m => m.Send(
                It.Is<SemanticSearchQuery>(q => q.K == 8),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SearchAsync_Should_ReturnMappedResults()
    {
        // Arrange
        var query = "test search query";
        var chunkId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var hits = new List<SearchHit>
        {
            new(
                new VectorChunk(chunkId, fileId, "Sheet1", 1, 10, "Test text content", new float[] { 0.1f, 0.2f }),
                0.95
            )
        };

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<SemanticSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(hits);

        // Act
        var result = await _tool.SearchAsync(query);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var parsed = JsonSerializer.Deserialize<JsonElement>(result);
        parsed.GetArrayLength().Should().Be(1);

        var firstResult = parsed[0];
        firstResult.GetProperty("id").GetGuid().Should().Be(chunkId);
        firstResult.GetProperty("fileId").GetGuid().Should().Be(fileId);
        firstResult.GetProperty("source").GetString().Should().Be("Sheet1");
        firstResult.GetProperty("chunkIndex").GetInt32().Should().Be(1);
        firstResult.GetProperty("text").GetString().Should().Be("Test text content");
        firstResult.GetProperty("score").GetDouble().Should().BeApproximately(0.95, 0.001);
    }

    [Fact]
    public async Task SearchAsync_Should_ReturnEmptyArray_When_NoResults()
    {
        // Arrange
        var query = "no results query";

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<SemanticSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SearchHit>());

        // Act
        var result = await _tool.SearchAsync(query);

        // Assert
        result.Should().Be("[]");
    }

    [Fact]
    public async Task SearchAsync_Should_PublishActivityEvent()
    {
        // Arrange
        var query = "test search query";
        var k = 5;

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<SemanticSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SearchHit>());

        // Act
        await _tool.SearchAsync(query, k);

        // Assert
        _activityPublisherMock.Verify(
            a => a.PublishAsync(
                "tool_invocation",
                It.Is<object>(o => o.ToString()!.Contains("semantic_search")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SearchAsync_Should_HandleMultipleResults()
    {
        // Arrange
        var query = "test search query";
        var hits = new List<SearchHit>
        {
            new(new VectorChunk(Guid.NewGuid(), Guid.NewGuid(), "Sheet1", 1, 10, "First result", new float[] { 0.1f }), 0.95),
            new(new VectorChunk(Guid.NewGuid(), Guid.NewGuid(), "Sheet2", 11, 20, "Second result", new float[] { 0.2f }), 0.85),
            new(new VectorChunk(Guid.NewGuid(), Guid.NewGuid(), "Sheet3", 21, 30, "Third result", new float[] { 0.3f }), 0.75)
        };

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<SemanticSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(hits);

        // Act
        var result = await _tool.SearchAsync(query);

        // Assert
        var parsed = JsonSerializer.Deserialize<JsonElement>(result);
        parsed.GetArrayLength().Should().Be(3);
    }

    [Fact]
    public async Task SearchAsync_Should_RespectCancellationToken()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var query = "test search query";

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<SemanticSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SearchHit>());

        // Act
        await _tool.SearchAsync(query, ct: cts.Token);

        // Assert
        _mediatorMock.Verify(
            m => m.Send(It.IsAny<SemanticSearchQuery>(), cts.Token),
            Times.Once);
    }

    #endregion

    #region GetAIFunctions Tests

    [Fact]
    public void GetAIFunctions_Should_ReturnTwoFunctions()
    {
        // Act
        var functions = _tool.GetAIFunctions();

        // Assert
        functions.Should().HaveCount(2);
    }

    [Fact]
    public void GetAIFunctions_Should_ContainAddDocumentFunction()
    {
        // Act
        var functions = _tool.GetAIFunctions();

        // Assert
        functions.Should().Contain(f => f.Name == "AddDocument");
    }

    [Fact]
    public void GetAIFunctions_Should_ContainSearchDocumentsFunction()
    {
        // Act
        var functions = _tool.GetAIFunctions();

        // Assert
        functions.Should().Contain(f => f.Name == "SearchDocuments");
    }

    [Fact]
    public void GetAIFunctions_Should_HaveCorrectDescriptions()
    {
        // Act
        var functions = _tool.GetAIFunctions();

        // Assert
        var addDocFunc = functions.First(f => f.Name == "AddDocument");
        addDocFunc.Description.Should().Contain("Add a document");

        var searchFunc = functions.First(f => f.Name == "SearchDocuments");
        searchFunc.Description.Should().Contain("Search documents");
    }

    #endregion

    #region Logging Tests

    [Fact]
    public async Task AddDocumentAsync_Should_LogInformation()
    {
        // Arrange
        var documentPath = "/path/to/document.pdf";

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AddDocumentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AddDocumentResult(documentPath, 1, null));

        // Act
        await _tool.AddDocumentAsync(documentPath);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                Microsoft.Extensions.Logging.LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("AddDocument")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SearchAsync_Should_LogInformation()
    {
        // Arrange
        var query = "test query";

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<SemanticSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SearchHit>());

        // Act
        await _tool.SearchAsync(query);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                Microsoft.Extensions.Logging.LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("SemanticSearch")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion
}
