using Application.Interfaces;
using MediatR;

namespace Application.UseCases.VectorStore.SemanticSearch;

/// <summary>
///     Query to perform semantic search over stored vectors.
/// </summary>
public sealed record SemanticSearchQuery(string Query, int K) : IRequest<IReadOnlyList<SearchHit>>;