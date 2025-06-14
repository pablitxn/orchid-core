using Application.Interfaces;
using Domain.Entities;
using Pgvector;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
///     EF Core implementation of <see cref="IDocumentRepository" />.
/// </summary>
public class DocumentRepository(ApplicationDbContext dbContext) : IDocumentRepository
{
    private readonly ApplicationDbContext _dbContext = dbContext;

    public async Task AddAsync(DocumentEntity document, CancellationToken cancellationToken = default)
    {
        _dbContext.Documents.Add(document);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateIndexingAsync(Guid documentId, int chunkCount,
        CancellationToken cancellationToken = default)
    {
        var document = await _dbContext.Documents.FindAsync(new object[] { documentId }, cancellationToken);
        if (document == null)
            return;
        document.IsIndexed = true;
        document.ChunkCount = chunkCount;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateEmbeddingAsync(Guid documentId, float[] embedding,
        CancellationToken cancellationToken = default)
    {
        var document = await _dbContext.Documents.FindAsync(new object[] { documentId }, cancellationToken);
        if (document == null)
            return;
        document.Embedding = new Vector(embedding);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}