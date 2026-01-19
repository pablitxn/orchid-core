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
    private readonly Mock<IActivityPublisher> _activityMock;
    private readonly VectorStoreTool _tool;

    public VectorStoreToolTests()
    {
        _loggerMock = new Mock<ILogger<VectorStoreTool>>();
        _mediatorMock = new Mock<IMediator>();
        _activityMock = new Mock<IActivityPublisher>();
        _tool = new VectorStoreTool(_loggerMock.Object, _mediatorMock.Object, _activityMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_LoggerIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new VectorStoreTool(null!, _mediatorMock.Object, _activityMock.Object));
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_MediatorIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new VectorStoreTool(_loggerMock.Object, null!, _activityMock.Object));
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_ActivityPublisherIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new VectorStoreTool(_loggerMock.Object, _mediatorMock.Object, null!));
    }

    #endregion

    #region ITool Implementation Tests

    [Fact]
    public void SystemName_Should_Return_VectorStore()
    {
        _tool.SystemName.Should().Be("vector_store");
    }

    [Fact]
    public void DisplayName_Should_Return_VectorStore()
    {
        _tool.DisplayName.Should().Be("Vector Store");
    }

    [Fact]
    public void Description_Should_NotBeEmpty()
    {
        _tool.Description.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void CreditCost_Should_Return_ExpectedValue()
    {
        _tool.CreditCost.Should().Be(1);
    }

    [Fact]
    public void RequiresSubscription_Should_ReturnFalse()
    {
        _tool.RequiresSubscription.Should().BeFalse();
    }

    [Fact]
    public void GetAIFunctions_Should_Return_TwoFunctions()
    {
        var functions = _tool.GetAIFunctions();
        
        functions.Should().HaveCount(2);
        functions.Select(f => f.Name).Should().Contain("AddDocument");
        functions.Select(f => f.Name).Should().Contain("SearchDocuments");
    }

    #endregion

    #region AddDocumentAsync Tests

    [Fact]
    public async Task AddDocumentAsync_Should_SendCommand_ToMediator()
    {
        // Arrange
        var documentPath = "test/document.txt";
        var metadata = "test metadata";
        var expectedResult = new AddDocumentResult(documentPath, 1, metadata);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AddDocumentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _tool.AddDocumentAsync(documentPath, metadata, CancellationToken.None);

        // Assert
        _mediatorMock.Verify(
            m => m.Send(
                It.Is<AddDocumentCommand>(c => c.DocumentPath == documentPath && c.Metadata == metadata),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AddDocumentAsync_Should_PublishActivity()
    {
        // Arrange
        var documentPath = "test/document.txt";
        var expectedResult = new AddDocumentResult(documentPath, 1, null);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AddDocumentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        await _tool.AddDocumentAsync(documentPath, null, CancellationToken.None);

        // Assert
        _activityMock.Verify(
            a => a.PublishAsync(
                "tool_invocation",
                It.Is<object>(o => o.ToString()!.Contains("add_document")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AddDocumentAsync_Should_ReturnJsonResult()
    {
        // Arrange
        var documentPath = "test/document.txt";
        var expectedResult = new AddDocumentResult(documentPath, 5, null);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AddDocumentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _tool.AddDocumentAsync(documentPath, null, CancellationToken.None);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var parsed = JsonSerializer.Deserialize<JsonElement>(result);
        parsed.GetProperty("documentPath").GetString().Should().Be(documentPath);
    }

    [Fact]
    public async Task AddDocumentAsync_Should_WorkWithNullMetadata()
    {
        // Arrange
        var documentPath = "test/document.txt";
        var expectedResult = new AddDocumentResult(documentPath, 1, null);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AddDocumentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _tool.AddDocumentAsync(documentPath, null, CancellationToken.None);

        // Assert
        result.Should().NotBeNullOrEmpty();
        _mediatorMock.Verify(
            m => m.Send(
                It.Is<AddDocumentCommand>(c => c.Metadata == null),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region SearchAsync Tests

    [Fact]
    public async Task SearchAsync_Should_SendQuery_ToMediator()
    {
        // Arrange
        var query = "test search query";
        var k = 5;
        IReadOnlyList<SearchHit> searchResults = new List<SearchHit>();

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<SemanticSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        // Act
        await _tool.SearchAsync(query, k, CancellationToken.None);

        // Assert
        _mediatorMock.Verify(
            m => m.Send(
                It.Is<SemanticSearchQuery>(q => q.Query == query && q.K == k),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SearchAsync_Should_UseDefaultK_WhenNotProvided()
    {
        // Arrange
        var query = "test search query";
        IReadOnlyList<SearchHit> searchResults = new List<SearchHit>();

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<SemanticSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        // Act
        await _tool.SearchAsync(query, ct: CancellationToken.None);

        // Assert
        _mediatorMock.Verify(
            m => m.Send(
                It.Is<SemanticSearchQuery>(q => q.K == 8),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SearchAsync_Should_PublishActivity()
    {
        // Arrange
        var query = "test search";
        IReadOnlyList<SearchHit> searchResults = new List<SearchHit>();

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<SemanticSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        // Act
        await _tool.SearchAsync(query, 5, CancellationToken.None);

        // Assert
        _activityMock.Verify(
            a => a.PublishAsync(
                "tool_invocation",
                It.Is<object>(o => o.ToString()!.Contains("semantic_search")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SearchAsync_Should_ReturnJsonArray_WithResults()
    {
        // Arrange
        var query = "test search";
        IReadOnlyList<SearchHit> searchResults = new List<SearchHit>
        {
            new(new VectorChunk(Guid.NewGuid(), Guid.NewGuid(), "Sheet1", 0, 100, "Sample text", new float[] { 0.1f }), 0.95),
            new(new VectorChunk(Guid.NewGuid(), Guid.NewGuid(), "Sheet2", 0, 50, "Another text", new float[] { 0.2f }), 0.85)
        };

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<SemanticSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        // Act
        var result = await _tool.SearchAsync(query, 5, CancellationToken.None);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var parsed = JsonSerializer.Deserialize<JsonElement>(result);
        parsed.GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task SearchAsync_Should_ReturnEmptyArray_WhenNoResults()
    {
        // Arrange
        var query = "no results query";
        IReadOnlyList<SearchHit> searchResults = new List<SearchHit>();

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<SemanticSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        // Act
        var result = await _tool.SearchAsync(query, 5, CancellationToken.None);

        // Assert
        result.Should().Be("[]");
    }

    #endregion
}
