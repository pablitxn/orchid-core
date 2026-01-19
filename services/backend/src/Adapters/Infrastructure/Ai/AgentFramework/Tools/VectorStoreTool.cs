using System.ComponentModel;
using System.Text.Json;
using Application.Interfaces;
using Application.UseCases.VectorStore.AddDocument;
using Application.UseCases.VectorStore.SemanticSearch;
using MediatR;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Ai.AgentFramework.Tools;

/// <summary>
/// Vector store tool for Agent Framework.
/// Provides semantic search and document storage capabilities.
/// </summary>
public sealed class VectorStoreTool
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IActivityPublisher _activity;
    private readonly ILogger<VectorStoreTool> _logger;
    private readonly IMediator _mediator;

    public VectorStoreTool(
        ILogger<VectorStoreTool> logger,
        IMediator mediator,
        IActivityPublisher activityPublisher)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _activity = activityPublisher ?? throw new ArgumentNullException(nameof(activityPublisher));
    }

    /// <summary>
    /// Add a document to the vector store for semantic search.
    /// </summary>
    /// <param name="documentPath">The path or identifier of the document to add.</param>
    /// <param name="metadata">Optional metadata associated with the document.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON result of the add operation.</returns>
    [Description("Add a document to the vector store for semantic search. Use this when the user wants to index or store a document for later retrieval.")]
    public async Task<string> AddDocumentAsync(
        [Description("The path or identifier of the document to add")] string documentPath,
        [Description("Optional metadata associated with the document")] string? metadata = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("AddDocument → {Document}", documentPath);

        var command = new AddDocumentCommand(documentPath, metadata);
        var resultObj = await _mediator.Send(command, ct);
        var json = JsonSerializer.Serialize(resultObj, JsonOptions);

        await _activity.PublishAsync("tool_invocation",
            new { tool = "add_document", parameters = new { documentPath, metadata }, result = json }, ct);

        return json;
    }

    /// <summary>
    /// Search documents using semantic similarity.
    /// </summary>
    /// <param name="query">The search query to find relevant documents.</param>
    /// <param name="k">Number of top results to return (default: 8).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON array of search results with relevance scores.</returns>
    [Description("Search documents using semantic similarity. Use this to find relevant information from previously indexed documents based on a natural language query.")]
    public async Task<string> SearchAsync(
        [Description("The search query to find relevant documents")] string query,
        [Description("Number of top results to return")] int k = 8,
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

        var json = JsonSerializer.Serialize(results, JsonOptions);

        await _activity.PublishAsync("tool_invocation",
            new { tool = "semantic_search", parameters = new { query, k }, result = json }, ct);

        return json;
    }

    /// <summary>
    /// Creates AIFunction instances from this tool's methods.
    /// </summary>
    /// <returns>List of AIFunction tools for use with Agent Framework.</returns>
    public IReadOnlyList<AIFunction> GetAIFunctions()
    {
        return new List<AIFunction>
        {
            AIFunctionFactory.Create(AddDocumentAsync, this),
            AIFunctionFactory.Create(SearchAsync, this)
        };
    }
}
