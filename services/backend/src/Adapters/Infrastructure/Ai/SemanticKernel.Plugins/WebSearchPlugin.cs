using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace Infrastructure.Ai.SemanticKernel.Plugins;

public sealed class WebSearchPlugin(
    ILogger<WebSearchPlugin> logger,
    ISearchProvider searchProvider,
    IHttpClientFactory httpClientFactory,
    IChatCompletionPort chat,
    IEmbeddingGeneratorPort embedder,
    IVectorStorePort vectorStore,
    IActivityPublisher activityPublisher)
{
    private readonly IActivityPublisher _activity =
        activityPublisher ?? throw new ArgumentNullException(nameof(activityPublisher));

    private readonly IChatCompletionPort _chat = chat ?? throw new ArgumentNullException(nameof(chat));
    private readonly IEmbeddingGeneratorPort _embedder = embedder ?? throw new ArgumentNullException(nameof(embedder));

    private readonly IHttpClientFactory _httpFactory =
        httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));

    private readonly ILogger<WebSearchPlugin> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    private readonly ISearchProvider
        _search = searchProvider ?? throw new ArgumentNullException(nameof(searchProvider));

    private readonly IVectorStorePort
        _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));

    private async Task PublishToolAsync(string tool, object parameters, object result)
    {
        await _activity.PublishAsync("tool_invocation", new { tool, parameters, result });
    }

    private void PublishTool(string tool, object parameters, object result)
    {
        try
        {
            _activity.PublishAsync("tool_invocation", new { tool, parameters, result }).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish tool invocation for {Tool}", tool);
        }
    }

    [KernelFunction("search_web")]
    public async Task<string> SearchWebAsync(
        string query,
        int topK = 8,
        string? siteFilter = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("SearchWeb → {Query}", query);
        var results = await _search.SearchAsync(query, topK, siteFilter, ct);
        var json = JsonSerializer.Serialize(results);
        await PublishToolAsync("search_web", new { query, topK, siteFilter }, json);
        return json;
    }

    [KernelFunction("fetch_content")]
    public async Task<string> FetchContentAsync(string url, CancellationToken ct = default)
    {
        _logger.LogInformation("FetchContent → {Url}", url);

        // Validate and sanitize URL to prevent SSRF
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            _logger.LogError("Invalid URL: {Url}", url);
            return string.Empty;
        }

        if (uri.HostNameType != UriHostNameType.Dns)
        {
            _logger.LogError("Blocked non-DNS URL: {Url}", url);
            return string.Empty;
        }

        var client = _httpFactory.CreateClient();
        try
        {
            var content = await client.GetStringAsync(uri, ct);
            await PublishToolAsync("fetch_content", new { url }, content);
            return content;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed for URL {Url}", url);
        }
        catch (TaskCanceledException ex) when (ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "Fetch content canceled for URL {Url}", url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching content from URL {Url}", url);
        }

        return string.Empty;
    }

    [KernelFunction("clean_content")]
    public string CleanContent(string html)
    {
        _logger.LogInformation("CleanContent (len={Length})", html.Length);
        var text = Regex.Replace(html, "<.*?>", " ");
        text = WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, "\\s+", " ").Trim();
        PublishTool("clean_content", new { length = html.Length }, text);
        return text;
    }

    [KernelFunction("summarize_content")]
    public async Task<string> SummarizeContentAsync(string content, CancellationToken ct = default)
    {
        _logger.LogInformation("SummarizeContent (len={Length})", content.Length);
        var messages = new[]
        {
            new ChatMessage("system", "Summarize the following text in 3 sentences:"),
            new ChatMessage("user", content)
        };
        var summary = await _chat.CompleteAsync(messages, ct);
        await PublishToolAsync("summarize_content", new { length = content.Length }, summary);
        return summary;
    }

    [KernelFunction("upsert_to_vector")]
    public async Task<string> UpsertToVectorAsync(string url, string text, CancellationToken ct = default)
    {
        _logger.LogInformation("UpsertToVector → {Url}", url);

        // Generate a stable FileId based on URL
        var urlBytes = Encoding.UTF8.GetBytes(url);
        using var md5 = MD5.Create();
        var fileId = new Guid(md5.ComputeHash(urlBytes));

        // Chunk text into segments if necessary
        const int MaxChunkSize = 1000;
        var chunkIds = new List<Guid>();

        if (!string.IsNullOrEmpty(text))
            for (var offset = 0; offset < text.Length;)
            {
                var length = Math.Min(MaxChunkSize, text.Length - offset);
                var chunkText = text.Substring(offset, length);
                var embedding = await _embedder.EmbedAsync(chunkText, ct);
                var chunkId = Guid.NewGuid();
                var startOffset = offset;
                var endOffset = offset + length;
                var chunk = new VectorChunk(chunkId, fileId, url, startOffset, endOffset, chunkText, embedding);
                await _vectorStore.UpsertChunkAsync(chunk, ct);
                chunkIds.Add(chunkId);
                offset += length;
            }
        else
            _logger.LogWarning("Empty text for URL {Url}, skipping vector upsert.", url);

        var result = JsonSerializer.Serialize(new { chunkIds, url });
        await PublishToolAsync("upsert_to_vector", new { url, chunkCount = chunkIds.Count }, result);
        return result;
    }
}