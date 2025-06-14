using Application.Interfaces;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Infrastructure.VectorStore;
using Npgsql;
using Xunit;

namespace VectorStore.IntegrationTests;

/// <summary>
///     Integration tests for <see cref="PgVectorStoreAdapter" /> backed by a
///     disposable PostgreSQL+pgvector container.  Tests run in parallel inside the
///     same class instance; the container is created once per class (IAsyncLifetime).
/// </summary>
public sealed class PgVectorStoreAdapterTests : IAsyncLifetime, IDisposable
{
    private const int Dimension = 3;

    private readonly TestcontainersContainer _postgresContainer = new TestcontainersBuilder<TestcontainersContainer>()
        .WithImage("pgvector/pgvector:pg17")
        .WithEnvironment("POSTGRES_USER", "postgres")
        .WithEnvironment("POSTGRES_PASSWORD", "postgres")
        .WithEnvironment("POSTGRES_DB", "testdb")
        .WithPortBinding(5432, true) // random free host port
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
        .Build();

    private IVectorStorePort _adapter = null!;

    private NpgsqlDataSource _dataSource = null!;

    #region IDisposable

    public void Dispose()
    {
        _dataSource?.Dispose();
        // Rider/IDEA “Dispose” inspection happy
    }

    #endregion

    /* ------------------------------------------------------------ *
     *                            TESTS                             *
     * ------------------------------------------------------------ */

    [Fact(DisplayName = "Upsert then search returns the same chunk")]
    public async Task SearchAsync_ReturnsInsertedChunk()
    {
        var chunk = RandomChunk();
        await _adapter.UpsertChunkAsync(chunk);

        var results = await _adapter.SearchAsync("any");
        Assert.Single(results);

        var result = results[0];
        Assert.Equal(chunk.ChunkId, result.Chunk.ChunkId);
        Assert.Equal(chunk.FileId, result.Chunk.FileId);
        Assert.Equal(chunk.SheetName, result.Chunk.SheetName);
        Assert.Equal(chunk.StartRow, result.Chunk.StartRow);
        Assert.Equal(chunk.EndRow, result.Chunk.EndRow);
        Assert.Equal(chunk.Text, result.Chunk.Text);
        Assert.Equal(0d, result.Score, 6);
    }

    [Fact(DisplayName = "Search ranks by distance")]
    public async Task SearchAsync_RanksByDistance()
    {
        var idClosest = Guid.NewGuid();
        var idFurther = Guid.NewGuid();
        var fileId = Guid.NewGuid();

        await _adapter.UpsertChunkAsync(
            new VectorChunk(idClosest, fileId, "S", 1, 1, "origin", new float[Dimension]));

        await _adapter.UpsertChunkAsync(
            new VectorChunk(idFurther, fileId, "S", 1, 1, "one-away", new[] { 1f, 0f, 0f }));

        var results = await _adapter.SearchAsync("any", k: 2);

        Assert.Collection(results,
            first => Assert.Equal(idClosest, first.Chunk.ChunkId),
            second => Assert.Equal(idFurther, second.Chunk.ChunkId));

        Assert.True(results[0].Score < results[1].Score);
    }

    [Fact(DisplayName = "Upsert updates existing row (idempotent)")]
    public async Task UpsertChunkAsync_UpdatesExistingRow()
    {
        var chunkId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var chunkV1 = new VectorChunk(chunkId, fileId, "S", 1, 1, "v1", new float[Dimension]);
        await _adapter.UpsertChunkAsync(chunkV1);

        var chunkV2 = chunkV1 with { Text = "v2" };
        await _adapter.UpsertChunkAsync(chunkV2);

        var fetched = (await _adapter.SearchAsync("any")).Single();
        Assert.Equal("v2", fetched.Chunk.Text);
    }

    [Theory(DisplayName = "Search respects k-limit")]
    [InlineData(1)]
    [InlineData(2)]
    public async Task SearchAsync_RespectsKLimit(int k)
    {
        var fileId = Guid.NewGuid();

        // 3 vectors at various distances
        await _adapter.UpsertChunkAsync(
            new VectorChunk(Guid.NewGuid(), fileId, "S", 1, 1, "v0", new float[Dimension]));
        await _adapter.UpsertChunkAsync(
            new VectorChunk(Guid.NewGuid(), fileId, "S", 1, 1, "v1", new[] { 1f, 0f, 0f }));
        await _adapter.UpsertChunkAsync(
            new VectorChunk(Guid.NewGuid(), fileId, "S", 1, 1, "v2", new[] { 2f, 0f, 0f }));

        var results = await _adapter.SearchAsync("any", k);
        Assert.Equal(k, results.Count);
    }

    [Fact(DisplayName = "Search returns empty when table empty")]
    public async Task SearchAsync_ReturnsEmptyWhenNoData()
    {
        var results = await _adapter.SearchAsync("query");
        Assert.Empty(results);
    }

    /* ------------------------------------------------------------ *
     *                      Helpers & Fakes                         *
     * ------------------------------------------------------------ */

    private static VectorChunk RandomChunk()
    {
        var chunkId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var embedding = new float[Dimension]; // origin vector
        return new VectorChunk(chunkId, fileId, "Sheet1", 1, 2, "test text", embedding);
    }

    /// <summary>
    ///     Embeds any input as the zero vector – makes ranking deterministic.
    /// </summary>
    private sealed class FakeEmbedder : IEmbeddingGeneratorPort
    {
        public Task<float[]> EmbedAsync(string input, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new float[Dimension]);
        }
    }

    // Use the official pgvector image built on Postgres 17
    // random free host port

    #region IAsyncLifetime

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();

        var hostPort = _postgresContainer.GetMappedPublicPort(5432);
        var connectionString =
            $"Host=localhost;Port={hostPort};Database=testdb;Username=postgres;Password=postgres";

        // 1️⃣  Install pgvector (CREATE EXTENSION)
        await using (var initConn = new NpgsqlConnection(connectionString))
        {
            await initConn.OpenAsync();
            await using var cmd = initConn.CreateCommand();
            cmd.CommandText = "CREATE EXTENSION IF NOT EXISTS vector;";
            await cmd.ExecuteNonQueryAsync();
        }

        // 2️⃣  Build an NpgsqlDataSource *after* pgvector has been installed
        var dsBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dsBuilder.UseVector(); // maps <vector>
        _dataSource = dsBuilder.Build();

        // 3️⃣  Create table for the tests
        await using (var conn = await _dataSource.OpenConnectionAsync())
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"""
                               CREATE TABLE SheetChunks(
                                   chunk_id   uuid PRIMARY KEY,
                                   file_id    uuid,
                                   sheet      text,
                                   start_row  int,
                                   end_row    int,
                                   txt        text,
                                   embedding  vector({Dimension})
                               );
                               """;
            await cmd.ExecuteNonQueryAsync();
        }

        _adapter = new PgVectorStoreAdapter(_dataSource, new FakeEmbedder());
    }

    public async Task DisposeAsync()
    {
        await _postgresContainer.StopAsync();
        await _dataSource.DisposeAsync();
    }

    #endregion
}