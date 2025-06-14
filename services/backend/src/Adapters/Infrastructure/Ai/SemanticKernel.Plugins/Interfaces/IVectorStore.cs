namespace Infrastructure.Ai.SemanticKernel.Plugins.Interfaces;

public abstract record Chunk(string Id, string Text, float[]? Embedding = null);

public interface IVectorStore
{
    ValueTask UpsertAsync(IEnumerable<Chunk> chunks, CancellationToken ct = default);

    ValueTask<IReadOnlyList<(Chunk, double score)>> SearchAsync(
        string query, int k, CancellationToken ct = default);
}