using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
///     EF Core implementation of <see cref="ISheetChunkRepository" />.
/// </summary>
public class SheetChunkRepository(ApplicationDbContext dbContext) : ISheetChunkRepository
{
    private readonly ApplicationDbContext _dbContext = dbContext;

    /// <inheritdoc />
    public async Task AddAsync(SheetChunkEntity sheetChunk, CancellationToken cancellationToken = default)
    {
        _dbContext.SheetChunks.Add(sheetChunk);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SheetChunkEntity>> GetByDocumentIdAsync(Guid documentId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.SheetChunks
            .Where(c => c.DocumentId == documentId)
            .ToListAsync(cancellationToken);
    }
}