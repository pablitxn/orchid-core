using Application.Interfaces;
using Application.UseCases.VectorStore.SemanticSearch;
using Moq;

namespace Application.Tests.UseCases.VectorStore.SemanticSearch;

public class SemanticSearchHandlerTests
{
    private const string QueryText = "q";
    private const int TopK = 5;
    private static readonly float[] SampleEmbedding = new[] { 0.1f, 0.2f, 0.3f };

    [Fact]
    public async Task Handle_ReturnsHits()
    {
        var hits = new List<SearchHit>
        {
            new(new VectorChunk(Guid.NewGuid(), Guid.NewGuid(), "s", 0, 0, "txt", SampleEmbedding), 0.1)
        };

        var store = new Mock<IVectorStorePort>();
        store.Setup(s => s.SearchAsync(QueryText, TopK, It.IsAny<CancellationToken>())).ReturnsAsync(hits);

        var handler = new SemanticSearchHandler(store.Object);
        var result = await handler.Handle(new SemanticSearchQuery(QueryText, TopK), CancellationToken.None);

        Assert.Equal(hits, result);
    }

    [Fact]
    public async Task Handle_ReturnsEmptyWhenNoHits()
    {
        var emptyHits = new List<SearchHit>();
        var store = new Mock<IVectorStorePort>();
        store.Setup(s => s.SearchAsync(QueryText, TopK, It.IsAny<CancellationToken>())).ReturnsAsync(emptyHits);

        var handler = new SemanticSearchHandler(store.Object);
        var result = await handler.Handle(new SemanticSearchQuery(QueryText, TopK), CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_WithLargeK_ReturnsHits()
    {
        const int largeK = 1000000;
        var hits = new List<SearchHit>
        {
            new(new VectorChunk(Guid.NewGuid(), Guid.NewGuid(), "s", 0, 0, "txt", SampleEmbedding), 0.5)
        };

        var store = new Mock<IVectorStorePort>();
        store.Setup(s => s.SearchAsync(QueryText, largeK, It.IsAny<CancellationToken>())).ReturnsAsync(hits);

        var handler = new SemanticSearchHandler(store.Object);
        var result = await handler.Handle(new SemanticSearchQuery(QueryText, largeK), CancellationToken.None);

        Assert.Equal(hits, result);
        store.Verify(s => s.SearchAsync(QueryText, largeK, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithEmptyQuery_PassesEmptyQuery()
    {
        var emptyQuery = string.Empty;
        var hits = new List<SearchHit>();

        var store = new Mock<IVectorStorePort>();
        store.Setup(s => s.SearchAsync(emptyQuery, TopK, It.IsAny<CancellationToken>())).ReturnsAsync(hits);

        var handler = new SemanticSearchHandler(store.Object);
        var result = await handler.Handle(new SemanticSearchQuery(emptyQuery, TopK), CancellationToken.None);

        Assert.Empty(result);
        store.Verify(s => s.SearchAsync(emptyQuery, TopK, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToStore()
    {
        var cts = new CancellationTokenSource();
        var token = cts.Token;
        var hits = new List<SearchHit>();

        var store = new Mock<IVectorStorePort>();
        store.Setup(s => s.SearchAsync(QueryText, TopK, token)).ReturnsAsync(hits);

        var handler = new SemanticSearchHandler(store.Object);
        var result = await handler.Handle(new SemanticSearchQuery(QueryText, TopK), token);

        Assert.Equal(hits, result);
        store.Verify(s => s.SearchAsync(QueryText, TopK, token), Times.Once);
    }
}