using Domain.Entities;

namespace Application.Interfaces;

/// <summary>
///     Repository for persisting uploaded document metadata.
/// </summary>
public interface IDocumentRepository
{
    /// <summary>
    ///     Adds a new document record.
    /// </summary>
    Task AddAsync(DocumentEntity document, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Updates an existing document record with indexing information.
    /// </summary>
    Task UpdateIndexingAsync(Guid documentId, int chunkCount, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Updates the embedding vector for a document.
    /// </summary>
    Task UpdateEmbeddingAsync(Guid documentId, float[] embedding, CancellationToken cancellationToken = default);
}