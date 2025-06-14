namespace Application.Interfaces;

/// <summary>
///     Port for vector store operations: upsert chunks and hybrid search.
/// </summary>
public interface IVectorStorePort
{
    /// <summary>
    ///     Inserts or updates a vector chunk.
    /// </summary>
    Task UpsertChunkAsync(VectorChunk chunk, CancellationToken ct = default);

    /// <summary>
    ///     Searches for similar vector chunks based on the query string.
    /// </summary>
    Task<IReadOnlyList<SearchHit>> SearchAsync(string query, int k = 8, CancellationToken ct = default);
}

/// <summary>
///     Represents a chunk of text with its associated embedding.
/// </summary>
public sealed record VectorChunk(
    Guid ChunkId,
    Guid FileId,
    string SheetName,
    int StartRow,
    int EndRow,
    string Text,
    float[] Embedding);

/// <summary>
///     Search result hit with a vector chunk and its similarity score.
/// </summary>
public sealed record SearchHit(
    VectorChunk Chunk,
    double Score,
    string SourceType = "vector");