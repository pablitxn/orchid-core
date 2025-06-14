using Application.Interfaces;
using Npgsql;
using Pgvector;

namespace Infrastructure.VectorStore;

/// <summary>
///     PostgresSQL pgvector adapter.
/// </summary>
public sealed class PgVectorStoreAdapter : IVectorStorePort
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly IEmbeddingGeneratorPort _embedder;

    public PgVectorStoreAdapter(NpgsqlDataSource dataSource, IEmbeddingGeneratorPort embedder)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _embedder = embedder ?? throw new ArgumentNullException(nameof(embedder));
    }

    public async Task UpsertChunkAsync(VectorChunk chunk, CancellationToken cancellationToken = default)
    {
        const string sql = """
                           INSERT INTO "SheetChunks" (
                               "Id", "DocumentId", "SheetName", "StartRow", "EndRow", "Text", "Embedding"
                           )
                           VALUES (
                               @cid, @fid, @sheetName, @startRow, @endRow, @text, @emb
                           )
                           ON CONFLICT ("Id") DO UPDATE
                             SET "Text" = EXCLUDED."Text",
                                 "Embedding" = EXCLUDED."Embedding";
                           """;

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue("cid", chunk.ChunkId);
        cmd.Parameters.AddWithValue("fid", chunk.FileId);
        cmd.Parameters.AddWithValue("sheetName", chunk.SheetName);
        cmd.Parameters.AddWithValue("startRow", chunk.StartRow);
        cmd.Parameters.AddWithValue("endRow", chunk.EndRow);
        // Sanitize text to remove null characters, which cause invalid UTF8 sequences in Postgres
        var sanitizedText = chunk.Text?.Replace("\0", string.Empty);
        cmd.Parameters.AddWithValue("text", sanitizedText);

        // Add embedding parameter; include typmod for pgvector dimension
        var embParam = cmd.Parameters.AddWithValue("emb", new Vector(chunk.Embedding));
        embParam.DataTypeName = $"vector({chunk.Embedding.Length})";

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(
        string query,
        int k = 8,
        CancellationToken cancellationToken = default)
    {
        // Generate embedding for the query
        var qvec = await _embedder.EmbedAsync(query, cancellationToken);

        // Perform k-NN search in Postgres using pgvector operator
        var sql = $"""
                   SELECT
                       "Id",
                       "DocumentId",
                       "SheetName",
                       "StartRow",
                       "EndRow",
                       "Text",
                       "Embedding"::real[] AS "Embedding",
                       "Embedding" <-> @qvec AS score
                   FROM "SheetChunks"
                   ORDER BY score
                   LIMIT {k};
                   """;

        await using var cmd = _dataSource.CreateCommand(sql);
        var qParam = cmd.Parameters.AddWithValue("qvec", new Vector(qvec));
        qParam.DataTypeName = $"vector({qvec.Length})";

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        var hits = new List<SearchHit>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var embedding = reader.GetFieldValue<float[]>(reader.GetOrdinal("Embedding"));
            var chunk = new VectorChunk(
                reader.GetGuid(reader.GetOrdinal("Id")),
                reader.GetGuid(reader.GetOrdinal("DocumentId")),
                reader.GetString(reader.GetOrdinal("SheetName")),
                reader.GetInt32(reader.GetOrdinal("StartRow")),
                reader.GetInt32(reader.GetOrdinal("EndRow")),
                reader.GetString(reader.GetOrdinal("Text")),
                embedding);
            var score = reader.GetDouble(reader.GetOrdinal("score"));
            hits.Add(new SearchHit(chunk, score));
        }

        return hits
            .OrderBy(h => h.Score)
            .Take(k)
            .ToList();
    }
}