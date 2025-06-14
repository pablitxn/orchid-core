using Application.Interfaces;
using MediatR;

namespace Application.UseCases.VectorStore.SemanticSearch;

/// <summary>
///     Handles <see cref="SemanticSearchQuery" /> by delegating to the vector store.
/// </summary>
public class SemanticSearchHandler(IVectorStorePort vectorStore)
    : IRequestHandler<SemanticSearchQuery, IReadOnlyList<SearchHit>>
{
    private readonly IVectorStorePort _vectorStore = vectorStore;

    public Task<IReadOnlyList<SearchHit>> Handle(SemanticSearchQuery request, CancellationToken cancellationToken)
    {
        return _vectorStore.SearchAsync(request.Query, request.K, cancellationToken);
    }
}