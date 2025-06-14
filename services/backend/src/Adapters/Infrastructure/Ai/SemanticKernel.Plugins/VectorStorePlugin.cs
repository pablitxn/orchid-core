using System.Text.Json;
using Application.Interfaces;
using Application.UseCases.VectorStore.AddDocument;
using Application.UseCases.VectorStore.SemanticSearch;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace Infrastructure.Ai.SemanticKernel.Plugins;

public sealed class VectorStorePlugin(
    ILogger<VectorStorePlugin> logger,
    IMediator mediator,
    IActivityPublisher activityPublisher)
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IActivityPublisher _activity =
        activityPublisher ?? throw new ArgumentNullException(nameof(activityPublisher));

    private readonly ILogger<VectorStorePlugin> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IMediator _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));

    [KernelFunction("add_document")]
    public async Task<string> AddDocumentAsync(
        string documentPath,
        string? metadata = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("AddDocument → {Document}", documentPath);

        var command = new AddDocumentCommand(documentPath, metadata);
        var resultObj = await _mediator.Send(command, ct);
        var json = JsonSerializer.Serialize(resultObj, _jsonOptions);
        await _activity.PublishAsync("tool_invocation",
            new { tool = "add_document", parameters = new { documentPath, metadata }, result = json }, ct);
        return json;
    }

    [KernelFunction("semantic_search")]
    public async Task<string> SearchAsync(
        string query,
        int k = 8,
        CancellationToken ct = default)
    {
        _logger.LogInformation("SemanticSearch → {Query} (k={K})", query, k);

        var hits = await _mediator.Send(new SemanticSearchQuery(query, k), ct);

        var results = hits.Select(h => new
        {
            id = h.Chunk.ChunkId,
            fileId = h.Chunk.FileId,
            source = h.Chunk.SheetName,
            chunkIndex = h.Chunk.StartRow,
            text = h.Chunk.Text,
            score = h.Score
        });

        var json = JsonSerializer.Serialize(results, _jsonOptions);
        await _activity.PublishAsync("tool_invocation",
            new { tool = "semantic_search", parameters = new { query, k }, result = json }, ct);
        return json;
    }
}