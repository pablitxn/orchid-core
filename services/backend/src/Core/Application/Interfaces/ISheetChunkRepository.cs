using Domain.Entities;

namespace Application.Interfaces;

/// <summary>
///     Repository for persisting sheet chunks of a document.
/// </summary>
public interface ISheetChunkRepository
{
    /// <summary>
    ///     Adds a new sheet chunk record.
    /// </summary>
    Task AddAsync(SheetChunkEntity sheetChunk, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Retrieves all sheet chunks for a given document.
    /// </summary>
    Task<IReadOnlyList<SheetChunkEntity>> GetByDocumentIdAsync(Guid documentId,
        CancellationToken cancellationToken = default);
}